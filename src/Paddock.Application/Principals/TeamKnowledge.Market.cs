using Paddock.Application.Access;
using Paddock.Application.Contracts;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Principals;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;

namespace Paddock.Application.Principals;

/// <summary>The market input of one review, with the typed ids the actions will need to become commands.</summary>
internal sealed record MarketBundle(
    MarketInput Input,
    IReadOnlyDictionary<string, Contract> Contracts,
    IReadOnlyDictionary<string, PersonId> Persons,
    IReadOnlyList<DateOnly> Events,
    IReadOnlyList<string> StaffRoles);

internal sealed partial class TeamKnowledge
{
    /// <summary>The drivers the team runs: one per car (T41: two cars, two drivers).</summary>
    private const int DriverSeats = Paddock.Domain.Cars.CarEstimates.CarsPerTeam;

    private static readonly NegotiationStatus[] LiveStatuses =
    [
        NegotiationStatus.Open,
        NegotiationStatus.AwaitingResponse,
        NegotiationStatus.Countered,
        NegotiationStatus.PersonAgreed,
        NegotiationStatus.Considering,
    ];

    /// <summary>
    /// Reads the market: the team's own people and what it has already signed, the people with no contract, its open talks, and what is
    /// missing. Returns null when the host did not give the contract module.
    /// </summary>
    public MarketBundle? Market(AiPrincipalRecord? record)
    {
        var engine = _env.Contracts;
        if (engine is null)
        {
            return null;
        }

        var book = engine.Book;
        var section = book.Section;
        var active = section.ActiveOf(Organization);
        var contractsById = new Dictionary<string, Contract>(StringComparer.Ordinal);
        var persons = new Dictionary<string, PersonId>(StringComparer.Ordinal);
        var events = new List<DateOnly>();
        var funds = Funds();

        // The manager's own negotiations, without building the player view (history, reasons, belief bands) the decider does not read.
        var allTalks = section.OfManager(Manager.Value)
            .Where(item => item.Proposer == Organization)
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var talksView = allTalks.Where(item => LiveStatuses.Contains(item.Status)).ToArray();

        // A person who turned the team down, or whose talk the team ended or let lapse, is not approached again for a while.
        var failed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in allTalks)
        {
            if (item.Status is NegotiationStatus.Refused or NegotiationStatus.WalkedAway or NegotiationStatus.Lapsed
                && InboxBookDates.ToDateOnly(item.Opened) >= Day.AddDays(-AiEstimates.RetryBlockDays))
            {
                failed.Add(item.Counterparty.Value);
            }
        }

        // ---- own people under contract (drivers in a race seat, and key staff but the principal)
        var held = new List<Contract>();
        foreach (var contract in Contracts)
        {
            if (contract.End < Today || !contract.Exclusive || !IsMarketRole(contract))
            {
                continue;
            }

            held.Add(contract);
        }

        var incumbents = new List<Incumbent>();
        var heldSeats = new List<string>();
        var heldDrivers = new HashSet<string>(StringComparer.Ordinal);
        var heldSubjects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contract in held)
        {
            var subject = ContractEngine.SubjectOf(contract).Key;
            heldSubjects.Add(subject);
            events.Add(InboxBookDates.ToDateOnly(contract.End.AddDays(1)));
            events.Add(InboxBookDates.ToDateOnly(contract.End.AddDays(-AiEstimates.RenewalLeadDays)));
            if (subject == MarketSubjects.Driver && heldDrivers.Add(contract.PersonId.Value))
            {
                heldSeats.Add(contract.Role.Seat.ToString());
            }

            if (!contract.IsActiveOn(Today))
            {
                continue;
            }

            var person = World.GetPerson(contract.PersonId);
            persons[person.Id.Value] = person.Id;
            contractsById[contract.Id.Value] = contract;
            var renewed = held.Any(other => other.PersonId == contract.PersonId && other.Start > contract.Start && other.End > contract.End);
            var renewalOpen = active.Any(negotiation => negotiation.RenewalOf == contract.Id);
            var ask = book.ReferenceSalary(Organization, person.Id, ContractEngine.SubjectOf(contract), Today);
            incumbents.Add(new Incumbent(
                contract.Id.Value,
                ViewOf(person, QualityKeys(subject)),
                subject,
                contract.Role.IsDriver ? contract.Role.Seat.ToString() : null,
                InboxBookDates.ToDateOnly(contract.End),
                contract.Salary,
                ask,
                renewalOpen,
                renewed));
        }

        // ---- the talks
        var talks = new List<Talk>();
        var pendingFills = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var talk in talksView)
        {
            var subject = talk.Subject.Key;
            var incumbent = incumbents.FirstOrDefault(item => item.Person.Id == talk.Counterparty.Value && item.Subject == subject);
            var person = World.GetPerson(talk.Counterparty);
            persons[person.Id.Value] = person.Id;
            var state = talk.Status switch
            {
                NegotiationStatus.Open => TalkState.NeedsOffer,
                NegotiationStatus.AwaitingResponse => TalkState.Awaiting,
                NegotiationStatus.Countered => TalkState.Countered,
                NegotiationStatus.PersonAgreed => TalkState.PersonAgreed,
                _ => TalkState.Considering,
            };
            var deadline = InboxBookDates.ToDateOnly(talk.Deadline);
            talks.Add(new Talk(
                talk.Id,
                ViewOf(person, QualityKeys(subject)),
                subject,
                state,
                talk.CurrentOffer?.Salary ?? 0,
                talk.CurrentOffer?.Years ?? 0,
                talk.Counter?.Salary ?? 0,
                talk.Counter?.Years ?? 0,
                talk.Counter?.Seat?.ToString(),
                Math.Max(0, talk.MaxRounds - talk.RoundsUsed),
                deadline,
                incumbent is not null,
                book.ReferenceSalary(Organization, person.Id, talk.Subject, Today),
                incumbent?.SalaryDollars ?? 0));
            if (incumbent is null)
            {
                pendingFills[subject] = pendingFills.GetValueOrDefault(subject) + 1;
            }

            if (state is TalkState.Awaiting or TalkState.Considering)
            {
                var respond = talk.RespondOn is GameDate on ? InboxBookDates.ToDateOnly(on) : Day;
                events.Add(respond.AddDays(1));
            }

            events.Add(deadline.AddDays(1));
        }

        // ---- what is missing
        var roles = StaffRoleSet(record);
        var vacancies = new List<Vacancy>();
        var missingDrivers = DriverSeats - heldDrivers.Count - pendingFills.GetValueOrDefault(MarketSubjects.Driver);
        if (missingDrivers > 0)
        {
            vacancies.Add(new Vacancy(MarketSubjects.Driver, missingDrivers, DaysEmpty(MarketSubjects.Driver)));
        }

        foreach (var role in roles)
        {
            var subject = NegotiationSubject.Staff(role).Key;
            if (!heldSubjects.Contains(subject) && pendingFills.GetValueOrDefault(subject) == 0 && StaffCatalogue.AvailableFrom(role) <= Today.Year)
            {
                vacancies.Add(new Vacancy(subject, 1, DaysEmpty(subject)));
            }
        }

        var slots = Math.Max(0, book.Capacity(Organization) - active.Count);

        // ---- the people with no contract who could fill it
        var candidates = new List<Candidate>();
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var vacancy in vacancies)
        {
            wanted.Add(vacancy.Subject);
        }

        foreach (var incumbent in incumbents)
        {
            if (!incumbent.RenewalOpen && !incumbent.RenewedAlready && Today.DaysUntil(InboxBookDates.ToGameDate(incumbent.End)) <= AiEstimates.RenewalLeadDays)
            {
                wanted.Add(incumbent.Subject);
            }
        }

        if (wanted.Count > 0 && slots > 0 && book.Environment.Control.Controls(Manager, Organization))
        {
            // The free-agent list already excluded anyone under an exclusive contract, and the slot count already applied
            // the parallel-talk limit. ValidateOpen would repeat both by scanning every contract and every past negotiation
            // once per person. The only check still open is a live talk with that person.
            var negotiating = new HashSet<string>(StringComparer.Ordinal);
            foreach (var negotiation in active)
            {
                negotiating.Add(negotiation.Counterparty.Value);
            }

            var subjects = wanted.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            foreach (var view in new FreeAgentQuery(book).List(Access, Organization, Today))
            {
                if (failed.Contains(view.Person.Value) || negotiating.Contains(view.Person.Value))
                {
                    continue;
                }

                var person = World.GetPerson(view.Person);
                if (person.IsRetired)
                {
                    continue;
                }

                foreach (var subject in subjects)
                {
                    if (!Qualifies(view, subject))
                    {
                        continue;
                    }

                    var negotiationSubject = NegotiationSubject.Parse(subject);
                    if (!negotiationSubject.IsHeldBy(person.Roles))
                    {
                        continue;
                    }
                    persons[person.Id.Value] = person.Id;
                    candidates.Add(new Candidate(
                        ViewOf(person, QualityKeys(subject)),
                        subject,
                        book.ReferenceSalary(Organization, person.Id, negotiationSubject, Today)));
                }
            }
        }

        var input = new MarketInput(Day, funds, incumbents, candidates, talks, vacancies, heldSeats, slots);
        return new MarketBundle(input, contractsById, persons, events, roles.Select(role => role.ToString()).ToArray());
    }

    private static bool Qualifies(FreeAgentView view, string subject) =>
        MarketSubjects.IsDriver(subject)
            ? view.IsDriver
            : view.StaffRoles.Contains(NegotiationSubject.Parse(subject).StaffRole);

    /// <summary>A contract the market decides about: a driver in a race seat, or key staff other than the principal (the board appoints him).</summary>
    private static bool IsMarketRole(Contract contract) =>
        contract.Role.IsDriver
            ? contract.Role.Seat != SeatStatus.Reserve
            : contract.Role.StaffRole != StaffRole.TeamPrincipal;

    /// <summary>The key roles the principal treats as part of the team: those it has under contract or has ever had, and those it remembered.</summary>
    private IReadOnlyList<StaffRole> StaffRoleSet(AiPrincipalRecord? record)
    {
        var roles = new SortedSet<StaffRole>();
        foreach (var contract in Contracts)
        {
            if (contract.Role.IsStaff && contract.Role.StaffRole != StaffRole.TeamPrincipal)
            {
                roles.Add(contract.Role.StaffRole);
            }
        }

        if (record is not null && record.StaffRoles.Length > 0)
        {
            foreach (var text in record.StaffRoles.Split(','))
            {
                if (Enum.TryParse<StaffRole>(text, out var role) && role != StaffRole.TeamPrincipal)
                {
                    roles.Add(role);
                }
            }
        }

        return roles.ToArray();
    }

    /// <summary>How long a place has stood empty as far as the world shows it: since the last contract for it ended, but not before the season began.</summary>
    private int DaysEmpty(string subject)
    {
        var since = GameDate.SeasonStart(Today.Year);
        foreach (var contract in Contracts)
        {
            var matches = subject == MarketSubjects.Driver
                ? contract.Role.IsDriver && contract.Role.Seat != SeatStatus.Reserve
                : contract.Role.IsStaff && NegotiationSubject.Staff(contract.Role.StaffRole).Key == subject;
            if (matches && contract.End < Today && contract.End.AddDays(1) > since)
            {
                since = contract.End.AddDays(1);
            }
        }

        return Math.Max(0, since.DaysUntil(Today));
    }
}

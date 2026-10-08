using System.Globalization;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Inbox;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Contracts;

/// <summary>
/// What happens to a contract as time passes, once per lived day:
/// <list type="bullet">
/// <item><b>Renewal prompt.</b> When six months are left (<see cref="NegotiationEstimates.RenewalPromptDays"/>) the employer's human
/// manager gets a decision item with a deadline and a default (<see cref="NegotiationEstimates.RenewalDecisionDays"/>, the default is to let it end): negotiate a
/// renewal, extend on current terms, or let it run out (the default, so doing nothing never keeps anyone on old terms). Drivers and key staff each get one; other staff are one
/// grouped item; the principal's own contract is extended by the board. Such an item never holds the clock. Sent once per contract.</item>
/// <item><b>Person's option.</b> On the option's deadline a person who holds the option extends the contract when staying is
/// worth at least what a new offer would have to be.</item>
/// <item><b>Free agent at expiry.</b> The day after a contract ends, a person with no contract is a free agent. A public listing
/// (<see cref="FreeAgentQuery"/>, attributes only as bands) is derived from the world, so this handler only emits the fact.</item>
/// <item><b>Exit clause.</b> After each race the constructors' position is read. When it triggers an exit clause the person decides
/// to leave or stay, and the decision is traced.</item>
/// </list>
/// A team's option is used by the manager's <c>RenewContract</c> command and simply lapses when the deadline passes unused.
/// No RNG is drawn here, so this handler leaves every stream where it was (INV-004).
/// </summary>
public sealed class ContractLifecycleHandler : IDayHandler
{
    /// <summary>ESTIMATE: right after the negotiation handler.</summary>
    public const int DefaultOrder = 710;

    private readonly ContractEngine _engine;

    public ContractLifecycleHandler(ContractEngine engine, int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var book = _engine.Book;
        var today = context.Today;
        var contracts = book.World.Contracts;
        if (contracts.Count == 0)
        {
            return;
        }

        var grouped = new SortedDictionary<string, List<Contract>>(StringComparer.Ordinal);
        foreach (var contract in contracts)
        {
            if (contract.IsActiveOn(today))
            {
                PromptRenewal(contract, context, grouped);
                if (contract.Option is ContractOption option && option.Deadline == today)
                {
                    ExercisePersonOption(contract, context);
                }
            }
            else if (contract.End.AddDays(1) == today)
            {
                ListFreeAgent(contract, context);
            }
        }

        PostGroupedRenewals(grouped, today);

        if (context.DueEvents.Any(due => due.TypeId == ScheduledEventType.Race))
        {
            CheckExitClauses(context);
        }

        if (today.IsSeasonStart)
        {
            var existing = book.World.Contracts.Select(contract => contract.Id.Value).ToHashSet(StringComparer.Ordinal);
            book.Update(book.Section.PruneTo(id => existing.Contains(id.Value)));
        }

        var synced = CarSeatSync.Apply(book.World, today);
        if (!ReferenceEquals(synced, book.World))
        {
            book.Update(synced);
        }
    }

    /// <summary>
    /// Who is asked what when a contract is six months from its end (<see cref="NegotiationEstimates.RenewalPromptDays"/>):
    /// drivers and key staff get a prompt each, other staff one grouped item per organization, and the principal's own contract is
    /// the board's matter and is extended without a question. Every prompt has a deadline and a default, and none holds the clock.
    /// </summary>
    private void PromptRenewal(Contract contract, DayContext context, SortedDictionary<string, List<Contract>> grouped)
    {
        var book = _engine.Book;
        var today = context.Today;
        var days = today.DaysUntil(contract.End);
        if (days > NegotiationEstimates.RenewalPromptDays || book.Section.WasPrompted(contract.Id))
        {
            return;
        }

        book.Update(book.Section.WithRenewalPrompt(contract.Id));
        var renewed = book.LiveContractsOf(contract.PersonId, today).Any(other => other.Start > today && other.OrganizationId == contract.OrganizationId);
        if (renewed)
        {
            return;
        }

        context.Emit(ContractEventTypes.RenewalPrompt, new MarkerPayload(contract.Id.Value));
        var managers = _engine.HumanManagersOf(contract.OrganizationId);
        if (managers.Count == 0)
        {
            return;
        }

        if (contract.Role.IsStaff && contract.Role.StaffRole == StaffRole.TeamPrincipal)
        {
            ExtendPrincipal(contract, managers[0], today);
            return;
        }

        if (contract.Role.IsStaff && !StaffCatalogue.IsTeamRoster(contract.Role.StaffRole))
        {
            // A hidden role (nothing reads it, #265) lapses quietly: the player never saw this person.
            return;
        }

        if (contract.Role.IsStaff && !NegotiationEstimates.IsKeyStaff(contract.Role.StaffRole))
        {
            if (!grouped.TryGetValue(contract.OrganizationId.Value, out var list))
            {
                grouped[contract.OrganizationId.Value] = list = [];
            }

            list.Add(contract);
            return;
        }

        var person = book.World.GetPerson(contract.PersonId);
        foreach (var manager in managers)
        {
            var draft = new InboxItemDraft(
                ContractEngine.RenewalKind,
                ContractKeys.RenewalSubject,
                [
                    new(ContractEngine.ContractArgument, contract.Id.Value),
                    new("person", person.Name),
                    new("end", contract.End.ToString()),
                    new("years", ContractEngine.DefaultRenewalYears.ToString(CultureInfo.InvariantCulture)),
                    new("days", NegotiationEstimates.RenewalDecisionDays.ToString(CultureInfo.InvariantCulture)),
                    new(InboxItemDraft.FreeClockArgument, InboxItemDraft.FreeClockValue),
                ],
                [
                    new InboxOption(ContractEngine.OptionRenew, ContractKeys.RenewalRenewLabel, ContractKeys.RenewalRenewConsequence),
                    new InboxOption(ContractEngine.OptionExtend, ContractKeys.RenewalExtendLabel, ContractKeys.RenewalExtendConsequence),
                    new InboxOption(ContractEngine.OptionRelease, ContractKeys.RenewalReleaseLabel(person.IsFemale), ContractKeys.RenewalReleaseConsequence),
                ],
                today.AddDays(NegotiationEstimates.RenewalDecisionDays),
                ContractEngine.OptionRelease);
            _engine.PostRenewalPrompt(manager, draft, today);
        }
    }

    /// <summary>
    /// The principal's own contract is out of scope for now: the player is simply in the team. It is quietly extended on its
    /// current terms (no prompt, no inbox item), so it never expires and ends the player's job.
    /// </summary>
    private void ExtendPrincipal(Contract contract, ManagerId manager, GameDate today)
    {
        if (_engine.ValidateExtend(manager, contract.Id, today, boardDecision: true) is not null)
        {
            return;
        }

        _engine.Extend(manager, contract.Id, today);
    }

    private void PostGroupedRenewals(SortedDictionary<string, List<Contract>> grouped, GameDate today)
    {
        var book = _engine.Book;
        foreach (var (_, list) in grouped)
        {
            var names = string.Join(", ", list.Select(contract => book.World.GetPerson(contract.PersonId).Name));
            var ids = string.Join(",", list.Select(contract => contract.Id.Value));
            foreach (var manager in _engine.HumanManagersOf(list[0].OrganizationId))
            {
                var draft = new InboxItemDraft(
                    ContractEngine.RenewalGroupKind,
                    ContractKeys.RenewalGroupSubject,
                    [
                        new(ContractEngine.ContractsArgument, ids),
                        new("count", list.Count.ToString(CultureInfo.InvariantCulture)),
                        new("names", names),
                        new("end", list.Min(contract => contract.End).ToString()),
                        new("years", ContractEngine.DefaultRenewalYears.ToString(CultureInfo.InvariantCulture)),
                        new("days", NegotiationEstimates.RenewalDecisionDays.ToString(CultureInfo.InvariantCulture)),
                        new(InboxItemDraft.FreeClockArgument, InboxItemDraft.FreeClockValue),
                    ],
                    [
                        new InboxOption(ContractEngine.OptionExtend, ContractKeys.RenewalGroupExtendLabel, ContractKeys.RenewalGroupExtendConsequence),
                        new InboxOption(ContractEngine.OptionRelease, ContractKeys.RenewalGroupReleaseLabel, ContractKeys.RenewalGroupReleaseConsequence),
                    ],
                    today.AddDays(NegotiationEstimates.RenewalDecisionDays),
                    ContractEngine.OptionRelease);
                _engine.PostRenewalPrompt(manager, draft, today);
            }
        }
    }

    private void ExercisePersonOption(Contract contract, DayContext context)
    {
        var book = _engine.Book;
        var today = context.Today;
        var terms = book.Section.TermsOf(contract.Id);
        if (terms?.OptionHolder != OptionHolder.Person)
        {
            return;
        }

        if (book.LiveContractsOf(contract.PersonId, today).Any(other => other.Id != contract.Id && other.Start > today))
        {
            return;
        }

        var stay = book.UtilityOfStaying(contract, today);
        var subject = ContractEngine.SubjectOf(contract);
        var reference = book.ReferenceSalary(contract.OrganizationId, contract.PersonId, subject, today);
        var evaluation = book.ContextFor(contract.OrganizationId, contract.PersonId, subject, today, reference);
        var needed = CounterpartyEvaluator.Threshold(CounterpartyEvaluator.Floor(evaluation.Age, null), NegotiationEstimates.InterestStart);
        var exercise = stay >= needed;
        if (book.Environment.Trace.IsEnabled)
        {
            book.Environment.Trace.Record(PersonTraces.ForStayOrLeave(
                PersonTraces.TriggerOption, contract, stay, needed, exercise, "exercise", "decline", evaluation, today));
        }

        if (!exercise)
        {
            return;
        }

        var newEnd = GameDate.SeasonEnd(contract.End.Year + contract.Option!.Value.ExtraYears);
        book.Update(
            book.World.WithContractTerm(contract.Id, newEnd, null),
            book.Section.WithTerms(new ContractTerms(contract.Id, terms.PointsBonus, terms.WinBonus, terms.TitleBonus, null, terms.Exit)));
        context.Emit(ContractEventTypes.OptionExercised, new MarkerPayload(contract.Id.Value));
        foreach (var manager in _engine.HumanManagersOf(contract.OrganizationId))
        {
            _engine.PostContractNotice(manager, book.World.Contracts.First(c => c.Id == contract.Id), ContractKeys.NoticeOptionExercised, today);
        }
    }

    private void ListFreeAgent(Contract contract, DayContext context)
    {
        var book = _engine.Book;
        var today = context.Today;
        if (book.LiveContractsOf(contract.PersonId, today).Count > 0)
        {
            return;
        }

        context.Emit(ContractEventTypes.FreeAgent, new MarkerPayload(contract.PersonId.Value));
        foreach (var manager in _engine.HumanManagersOf(contract.OrganizationId))
        {
            _engine.PostContractNotice(manager, contract, ContractKeys.NoticeExpired, today);
        }
    }

    private void CheckExitClauses(DayContext context)
    {
        var book = _engine.Book;
        var today = context.Today;
        foreach (var terms in book.Section.Terms)
        {
            if (terms.Exit is not ExitClause clause)
            {
                continue;
            }

            var contract = _engine.FindContract(terms.ContractId);
            if (contract is null || !contract.IsActiveOn(today))
            {
                continue;
            }

            if (book.Environment.Standings.Position(contract.OrganizationId, today.Season) is not int position
                || !clause.IsTriggeredBy(position))
            {
                continue;
            }

            var subject = ContractEngine.SubjectOf(contract);
            var reference = book.ReferenceSalary(contract.OrganizationId, contract.PersonId, subject, today);
            var evaluation = book.ContextFor(contract.OrganizationId, contract.PersonId, subject, today, reference);
            var stay = book.UtilityOfStaying(contract, today);
            var needed = CounterpartyEvaluator.Threshold(CounterpartyEvaluator.Floor(evaluation.Age, null), NegotiationEstimates.InterestStart);
            var leave = stay < needed;
            if (book.Environment.Trace.IsEnabled)
            {
                book.Environment.Trace.Record(PersonTraces.ForStayOrLeave(
                    PersonTraces.TriggerExit, contract, stay, needed, leave, "exit", "stay", evaluation, today));
            }

            if (!leave)
            {
                continue;
            }

            book.Update(book.World.WithContractTerm(contract.Id, today, null));
            context.Emit(ContractEventTypes.ExitExercised, new MarkerPayload(contract.Id.Value));
            var ended = _engine.FindContract(contract.Id)!;
            foreach (var manager in _engine.HumanManagersOf(contract.OrganizationId))
            {
                _engine.PostContractNotice(manager, ended, ContractKeys.NoticeExitExercised, today);
            }
        }
    }
}

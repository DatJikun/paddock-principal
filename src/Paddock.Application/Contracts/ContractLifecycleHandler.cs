using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Contracts;

/// <summary>
/// What happens to a contract as time passes, once per lived day:
/// <list type="bullet">
/// <item><b>Renewal prompt.</b> When six months are left (<see cref="NegotiationEstimates.RenewalPromptDays"/>) the employer's human
/// manager gets a decision item: renew or let the contract run out. Sent once per contract (the section remembers).</item>
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

        foreach (var contract in contracts)
        {
            if (contract.IsActiveOn(today))
            {
                PromptRenewal(contract, context);
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

    private void PromptRenewal(Contract contract, DayContext context)
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

        var person = book.World.GetPerson(contract.PersonId);
        foreach (var manager in _engine.HumanManagersOf(contract.OrganizationId))
        {
            var draft = new InboxItemDraft(
                ContractEngine.RenewalKind,
                ContractKeys.RenewalSubject,
                [
                    new(ContractEngine.ContractArgument, contract.Id.Value),
                    new("person", person.Name),
                    new("end", contract.End.ToString()),
                ],
                [
                    new InboxOption(ContractEngine.OptionRenew, ContractKeys.RenewalRenewLabel, ContractKeys.RenewalRenewConsequence),
                    new InboxOption(ContractEngine.OptionRelease, ContractKeys.RenewalReleaseLabel, ContractKeys.RenewalReleaseConsequence),
                ],
                null,
                null);
            _engine.PostRenewalPrompt(manager, draft, today);
        }

        context.Emit(ContractEventTypes.RenewalPrompt, new MarkerPayload(contract.Id.Value));
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

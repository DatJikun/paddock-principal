using System.Globalization;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Contracts;

/// <summary>
/// The person's side of every negotiation, once per lived day (TECH section 5, the day tick):
/// <list type="number">
/// <item>An offer with no answer day yet gets one, a few days away, scheduled on the clock's queue
/// (<see cref="ContractEventTypes.Respond"/>); the jitter comes from the <c>Market</c> stream.</item>
/// <item>An answer that falls due is given: accept, counter or refuse, with the reasons the person is willing to state, and a decision
/// trace. A human proposer gets an inbox decision (it holds the clock); an AI proposer reads the negotiation itself.</item>
/// <item>A person who accepts one offer while rivals have offers on the table waits and decides at the earliest deadline among the
/// acceptable ones: best utility, then trust, then a Market draw. The losers are told.</item>
/// <item>A negotiation past its deadline lapses.</item>
/// </list>
/// All randomness is a child of the <c>Market</c> stream keyed by the negotiation id, so an unrelated negotiation never shifts
/// another one's draws (INV-004). The handler changes the contract book, which is the host's world state for contracts, and
/// uses the inbox of the game; it never reads the person's truth beyond the personality the person acts on (INV-003).
/// </summary>
public sealed class NegotiationDayHandler : IDayHandler
{
    /// <summary>ESTIMATE: before the handlers that read contracts (objectives, finance) and after the ones that move people.</summary>
    public const int DefaultOrder = 700;

    private readonly ContractEngine _engine;

    public NegotiationDayHandler(ContractEngine engine, int order = DefaultOrder)
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
        if (book.Section.Active().Count == 0)
        {
            return;
        }

        var today = context.Today;
        var market = new Lazy<RngStream>(() => new RngStream(RngStreamName.Market, context.Stream(RngStreamName.Market).State));

        var answerNow = new List<string>();
        ScheduleResponses(context, market, today, answerNow);
        foreach (var id in DueResponses(context, answerNow))
        {
            if (book.Section.Find(id) is { Status: NegotiationStatus.AwaitingResponse } negotiation)
            {
                Respond(negotiation, context);
            }
        }

        Decide(context, market);
        Lapse(context);
    }

    private void ScheduleResponses(DayContext context, Lazy<RngStream> market, GameDate today, List<string> answerNow)
    {
        var book = _engine.Book;
        foreach (var negotiation in book.Section.Active())
        {
            if (negotiation.Status != NegotiationStatus.AwaitingResponse || negotiation.RespondOn is not null || negotiation.Deadline < today)
            {
                continue;
            }

            if (negotiation.Deadline == today)
            {
                book.Update(book.Section.Replace(negotiation.WithResponseScheduled(today)));
                answerNow.Add(negotiation.Id);
                continue;
            }

            var tag = string.Create(CultureInfo.InvariantCulture, $"delay:{negotiation.Id}:{negotiation.RoundsUsed}");
            var jitter = market.Value.DeriveChild(tag).NextInt(0, NegotiationEstimates.ResponseDelayJitterDays + 1);
            var respondOn = today.AddDays(NegotiationEstimates.ResponseDelayMinDays + jitter);
            if (respondOn > negotiation.Deadline)
            {
                respondOn = negotiation.Deadline;
            }

            context.Schedule(respondOn, ContractEventTypes.Respond, new MarkerPayload(MarkerOf(negotiation)));
            book.Update(book.Section.Replace(negotiation.WithResponseScheduled(respondOn)));
        }
    }

    private IReadOnlyList<string> DueResponses(DayContext context, List<string> answerNow)
    {
        var book = _engine.Book;
        var due = new SortedSet<long>();
        foreach (var id in answerNow)
        {
            if (book.Section.Find(id) is { } negotiation)
            {
                due.Add(negotiation.Number);
            }
        }

        foreach (var scheduled in context.DueEvents)
        {
            if (scheduled.TypeId != ContractEventTypes.Respond || scheduled.Payload is not MarkerPayload marker)
            {
                continue;
            }

            var split = marker.Marker.IndexOf('#', StringComparison.Ordinal);
            if (split < 0
                || !int.TryParse(marker.Marker.AsSpan(split + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var round)
                || book.Section.Find(marker.Marker[..split]) is not { } negotiation
                || negotiation.Status != NegotiationStatus.AwaitingResponse
                || negotiation.RoundsUsed != round
                || negotiation.RespondOn != context.Today)
            {
                continue;
            }

            due.Add(negotiation.Number);
        }

        return due.Select(Negotiation.IdOf).ToArray();
    }

    private void Respond(Negotiation negotiation, DayContext context)
    {
        var book = _engine.Book;
        var today = context.Today;
        var person = _engine.FindPerson(negotiation.Counterparty);
        if (person is null || person.IsRetired)
        {
            Emit(context, ContractEventTypes.Lost, negotiation.Id);
            _engine.Lose(negotiation, [NegotiationReasons.BetterProspects], today);
            return;
        }

        var reference = book.ReferenceSalary(negotiation.Proposer, negotiation.Counterparty, negotiation.Subject, today);
        var evaluation = book.ContextFor(negotiation.Proposer, negotiation.Counterparty, negotiation.Subject, today, reference);
        var floor = CounterpartyEvaluator.Floor(
            evaluation.Age,
            book.CurrentContractUtility(negotiation.Counterparty, negotiation.Proposer, today));
        var response = NegotiationCore.Respond(negotiation, evaluation, floor);
        if (book.Environment.Trace.IsEnabled)
        {
            book.Environment.Trace.Record(PersonTraces.ForResponse(negotiation, response, evaluation, today));
        }

        switch (response.Kind)
        {
            case ResponseKind.Accept:
                var rivals = book.Section.ActiveFor(negotiation.Counterparty)
                    .Any(other => other.Id != negotiation.Id && other.HasOfferOnTable);
                if (rivals)
                {
                    var micro = (long)Math.Round(response.Evaluation.Utility * 1_000_000.0, MidpointRounding.AwayFromZero);
                    book.Update(book.Section.Replace(negotiation.WithConsideration(micro)));
                    Emit(context, ContractEventTypes.Considering, negotiation.Id);
                }
                else
                {
                    Agree(negotiation, context);
                }

                break;
            case ResponseKind.Counter:
                var countered = negotiation.WithCounter(response.Counter!, response.Reasons, today);
                book.Update(book.Section.Replace(countered));
                Emit(context, ContractEventTypes.Countered, negotiation.Id);
                _engine.PostResponse(countered, today);
                break;
            default:
                var refused = negotiation.WithRefusal(response.Reasons, today);
                book.Update(book.Section.Replace(refused));
                Emit(context, ContractEventTypes.Refused, negotiation.Id);
                _engine.PostNotice(refused, ContractKeys.InboxRefusedSubject, today);
                break;
        }
    }

    /// <summary>The person would sign the offer as it stands. An AI proposer signs at once; a human is asked to confirm.</summary>
    private void Agree(Negotiation negotiation, DayContext context)
    {
        var book = _engine.Book;
        var today = context.Today;
        var agreed = negotiation.WithAgreement([], today);
        book.Update(book.Section.Replace(agreed));
        Emit(context, ContractEventTypes.PersonAgreed, negotiation.Id);
        if (_engine.IsHuman(agreed.ManagerId))
        {
            _engine.PostResponse(agreed, today);
            return;
        }

        if (_engine.SigningProblem(agreed, today) is not null)
        {
            return;
        }

        foreach (var domainEvent in _engine.Sign(agreed, today))
        {
            if (domainEvent is ContractSigned signed)
            {
                Emit(context, ContractEventTypes.Signed, signed.ContractId);
            }
        }
    }

    private void Decide(DayContext context, Lazy<RngStream> market)
    {
        var book = _engine.Book;
        var today = context.Today;
        var groups = book.Section.Active()
            .Where(negotiation => negotiation.Status == NegotiationStatus.Considering)
            .GroupBy(negotiation => negotiation.Counterparty.Value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.ToArray())
            .ToArray();
        foreach (var group in groups)
        {
            var person = group[0].Counterparty;
            var candidates = group
                .Select(negotiation => new AcceptableOffer(negotiation, negotiation.ConsideredUtilityMicro / 1_000_000.0))
                .ToArray();
            var pending = book.Section.ActiveFor(person).Any(other => other.Status == NegotiationStatus.AwaitingResponse);
            var deadlineReached = today >= NegotiationCore.DecisionDay(candidates);
            if (!deadlineReached && (candidates.Length > 1 || pending))
            {
                continue;
            }

            var winner = NegotiationCore.ChooseWinner(
                candidates,
                person,
                book.Environment.Trust,
                id => market.Value.DeriveChild(string.Create(CultureInfo.InvariantCulture, $"tiebreak:{id}:{today}")).NextDouble());
            if (book.Environment.Trace.IsEnabled)
            {
                book.Environment.Trace.Record(PersonTraces.ForChoice(person, candidates, winner, today));
            }

            foreach (var candidate in candidates)
            {
                if (candidate.Negotiation.Id == winner.Negotiation.Id)
                {
                    continue;
                }

                _engine.Lose(candidate.Negotiation, [NegotiationReasons.BetterOffer], today);
                Emit(context, ContractEventTypes.Lost, candidate.Negotiation.Id);
            }

            Agree(book.Section.Find(winner.Negotiation.Id)!, context);
        }
    }

    private void Lapse(DayContext context)
    {
        var book = _engine.Book;
        var today = context.Today;
        foreach (var negotiation in book.Section.Active())
        {
            if (negotiation.Deadline >= today)
            {
                continue;
            }

            var lapsed = negotiation.WithLapse([NegotiationReasons.DeadlinePassed], today);
            book.Update(book.Section.Replace(lapsed));
            _engine.CloseResponseItems(negotiation.Id, ContractEngine.OptionWalk, today);
            _engine.PostNotice(lapsed, ContractKeys.InboxLapsedSubject, today);
            Emit(context, ContractEventTypes.Lapsed, negotiation.Id);
        }
    }

    private static string MarkerOf(Negotiation negotiation) =>
        string.Create(CultureInfo.InvariantCulture, $"{negotiation.Id}#{negotiation.RoundsUsed}");

    private static void Emit(DayContext context, string typeId, string marker) =>
        context.Emit(typeId, new MarkerPayload(marker));
}

using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.Inbox;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Application.Contracts;

/// <summary>
/// Once on 1 July and again on 31 December, a contracted driver may ask for a raise. At most one ask a season.
/// The roll is a child of <c>Market</c> keyed by the driver and the day, and the stream is not touched when nobody
/// is due (INV-004). A human gets an inbox decision that refuses by default. An AI team answers at once and leaves a trace.
/// </summary>
public sealed class RaiseDemandHandler : IDayHandler
{
    public const int DefaultOrder = 705;

    public const string Kind = "contract.raise";

    public const string OptionAccept = "accept";

    public const string OptionRefuse = "refuse";

    public const string OptionPartial = "partial";

    private readonly ContractEngine _engine;
    private readonly IDriverMorale _morale;

    public RaiseDemandHandler(ContractEngine engine, IDriverMorale? morale = null, int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        _morale = morale ?? new NeutralDriverMorale();
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var today = context.Today;
        if (today != new GameDate(today.Year, 7, 1) && today != GameDate.SeasonEnd(today.Year))
        {
            return;
        }

        var book = _engine.Book;
        var due = book.World.Contracts.Where(contract => contract.Role.IsDriver && contract.IsActiveOn(today)).ToArray();
        if (due.Length == 0)
        {
            return;
        }

        var raises = book.World.Section<RaisesSection>(RaisesSection.SectionName) ?? RaisesSection.Empty;
        var pending = due.Where(contract => !raises.Considered(contract.PersonId, contract.OrganizationId, today.Year)).ToArray();
        if (pending.Length == 0)
        {
            return;
        }

        var market = new RngStream(RngStreamName.Market, context.Stream(RngStreamName.Market).State);
        foreach (var contract in pending.OrderBy(contract => contract.PersonId.Value, StringComparer.Ordinal))
        {
            raises = Consider(contract, today, market, raises);
        }

        book.Update(book.World.WithSection(raises));
    }

    private RaisesSection Consider(Contract contract, GameDate today, RngStream market, RaisesSection raises)
    {
        var book = _engine.Book;
        var loyalty = book.Environment.Personality.TraitsOf(contract.PersonId).Loyalty;
        var morale = _morale.Happiness(book.World, contract.PersonId, contract.OrganizationId, today);
        var roll = market.DeriveChild(contract.PersonId.Value + "|" + today.ToString()).NextDouble();
        if (roll >= DriverRaise.AskChance(loyalty, morale))
        {
            return raises.Consider(contract.PersonId, contract.OrganizationId, today.Year, 0);
        }

        var subject = NegotiationSubject.DriverSeat;
        var knowledge = book.World.KnowledgeOf(contract.OrganizationId, contract.PersonId);
        var starsNow = ReferenceOffer.Stars(subject, knowledge);
        var starsReachable = DriverRaise.ReachableStars(knowledge, starsNow);
        var benchmarkNow = book.Environment.Pay.Reference(today.Year, subject, starsNow);
        var benchmarkReachable = book.Environment.Pay.Reference(today.Year, subject, starsReachable);
        var extra = DriverRaise.ExtraSalary(contract.Salary, benchmarkNow, benchmarkReachable);
        var asked = contract.Salary + extra;
        raises = raises.Consider(contract.PersonId, contract.OrganizationId, today.Year, asked);
        var humans = _engine.HumanManagersOf(contract.OrganizationId);
        if (humans.Count > 0)
        {
            Post(contract, asked, today, humans);
            return raises;
        }

        return DriverRaise.AiAccepts(contract.Salary, extra)
            ? Pay(book, contract, asked, raises, today, accept: true)
            : Refuse(book, contract, raises, today);
    }

    private void Post(Contract contract, long asked, GameDate today, IReadOnlyList<ManagerId> humans)
    {
        var person = _engine.Book.World.GetPerson(contract.PersonId);
        var partial = DriverRaise.PartialSalary(contract.Salary, asked);
        var draft = new InboxItemDraft(
            Kind,
            ContractKeys.RaiseSubject,
            [
                new(ContractEngine.ContractArgument, contract.Id.Value),
                new("person", person.Name),
                new("salary", asked.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("partial", partial.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ],
            [
                new InboxOption(OptionAccept, ContractKeys.RaiseAcceptLabel, ContractKeys.RaiseAcceptConsequence),
                new InboxOption(OptionPartial, ContractKeys.RaisePartialLabel, ContractKeys.RaisePartialConsequence),
                new InboxOption(OptionRefuse, ContractKeys.RaiseRefuseLabel, ContractKeys.RaiseRefuseConsequence),
            ],
            today.AddDays(NegotiationEstimates.RaiseDecisionDays),
            OptionRefuse);
        foreach (var manager in humans)
        {
            _engine.PostRenewalPrompt(manager, draft, today);
        }
    }

    internal static RaisesSection Pay(ContractBook book, Contract contract, long salary, RaisesSection raises, GameDate today, bool accept)
    {
        var next = raises.Accept(contract.PersonId, contract.OrganizationId);
        book.Update(book.World.WithContractSalary(contract.Id, salary).WithSection(next));
        Trace(book, contract, today, accept ? "accept" : "partial", accept);
        return book.World.Section<RaisesSection>(RaisesSection.SectionName) ?? next;
    }

    internal static RaisesSection Refuse(ContractBook book, Contract contract, RaisesSection raises, GameDate today)
    {
        var next = raises.Refuse(contract.PersonId, contract.OrganizationId);
        book.Update(book.World.WithSection(next));
        Trace(book, contract, today, "refuse", false);
        return book.World.Section<RaisesSection>(RaisesSection.SectionName) ?? next;
    }

    private static void Trace(ContractBook book, Contract contract, GameDate today, string chosen, bool paid)
    {
        if (!book.Environment.Trace.IsEnabled)
        {
            return;
        }

        book.Environment.Trace.Record(new DecisionTrace(
            new WeekendKey(today.Season, 0),
            contract.OrganizationId.Value,
            NegotiationEstimates.PersonDeciderLevel,
            "raise:" + contract.Id.Value,
            [
                new TraceOption("accept", 1, [], chosen == "accept"),
                new TraceOption("partial", 0.5, [], chosen == "partial"),
                new TraceOption("refuse", 0, [], chosen == "refuse"),
            ],
            chosen,
            paid ? "the team pays" : "the team refuses",
            null,
            paid,
            new Dictionary<string, string> { ["contract"] = contract.Id.Value }));
    }
}

/// <summary>The human answer to a raise: pay it, pay part of it, or refuse. Refusal is the default when the item expires.</summary>
public sealed class RaiseDemandResolver : IInboxResolver
{
    public string Kind => RaiseDemandHandler.Kind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (optionId is not (RaiseDemandHandler.OptionAccept or RaiseDemandHandler.OptionPartial or RaiseDemandHandler.OptionRefuse))
        {
            return TranslationMessage.Of(InboxKeys.OptionUnknown);
        }

        return Contract(context, item) is null
            ? TranslationMessage.Of(ContractKeys.UnknownContract)
            : null;
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var engine = ContractEngine.From(context);
        var contract = Contract(context, item) ?? throw new InvalidOperationException("Execute ran for a missing contract.");
        var book = engine.Book;
        var raises = book.World.Section<RaisesSection>(RaisesSection.SectionName) ?? RaisesSection.Empty;
        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        switch (optionId)
        {
            case RaiseDemandHandler.OptionAccept:
                RaiseDemandHandler.Pay(book, contract, Asked(raises, contract), raises, today, accept: true);
                break;
            case RaiseDemandHandler.OptionPartial:
                RaiseDemandHandler.Pay(book, contract, DriverRaise.PartialSalary(contract.Salary, Asked(raises, contract)), raises, today, accept: false);
                break;
            default:
                RaiseDemandHandler.Refuse(book, contract, raises, today);
                break;
        }

        return [];
    }

    private static Contract? Contract(CommandContext context, InboxItem item)
    {
        if (!item.Arguments.TryGetValue(ContractEngine.ContractArgument, out var text)
            || !text.StartsWith("con:", StringComparison.Ordinal)
            || !long.TryParse(text.AsSpan(4), out var sequence))
        {
            return null;
        }

        return ContractEngine.From(context).FindContract(ContractId.Generated(sequence));
    }

    private static long Asked(RaisesSection raises, Contract contract) =>
        raises.Records.Single(row => row.Person == contract.PersonId && row.Team == contract.OrganizationId).AskedSalary;
}

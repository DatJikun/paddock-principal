using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Domain.Board;
using Paddock.Application.Managers;
using Paddock.Domain.Inbox;
using Paddock.Simulation.Codec;

namespace Paddock.Application.Board;

/// <summary>A manager without a team takes the job of an offer item. Only the item's own manager can (INV-003).</summary>
public sealed record AcceptJobOfferCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string ItemId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>A manager without a team turns an offer down. The offer closes and nothing else changes.</summary>
public sealed record DeclineJobOfferCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string ItemId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>A manager leaves their team. The founder of a team cannot (post-MVP, PP-050).</summary>
public sealed record ResignFromTeamCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// Carries out the answer to a job offer (kind <see cref="BoardEngine.OfferKind"/>). It runs inside the inbox command, so the
/// change is that command's change (INV-001). Declining changes nothing but the item; accepting hands the team over.
/// </summary>
public sealed class JobOfferResolver : IInboxResolver
{
    public string Kind => BoardEngine.OfferKind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return optionId switch
        {
            BoardEngine.OptionDecline => null,
            BoardEngine.OptionAccept => BoardEngine.From(context).ValidateAcceptOffer(new ManagerId(item.ManagerId), item),
            _ => TranslationMessage.Of(InboxKeys.OptionUnknown),
        };
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (optionId != BoardEngine.OptionAccept)
        {
            return [];
        }

        var manager = new ManagerId(item.ManagerId);
        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        return BoardEvents.Wrap(manager, context.World.CurrentDate, BoardEngine.From(context).AcceptOffer(manager, item, today));
    }
}

/// <summary>Carries out the season-target decision. The default option is the expected finish, applied when the decision lapses.</summary>
public sealed class SeasonTargetResolver : IInboxResolver
{
    public string Kind => BoardEngine.SeasonTargetKind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return SeasonTarget.TryParse(optionId, out _) ? null : TranslationMessage.Of(InboxKeys.OptionUnknown);
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        BoardEngine.From(context).AcceptSeasonTarget(item, optionId, InboxBook.ToGameDate(context.World.CurrentDate));
        return [];
    }
}

internal static class BoardEvents
{
    public static IReadOnlyList<IDomainEvent> Wrap(ManagerId manager, DateOnly on, IEnumerable<BoardFact> facts) =>
        facts.Select(fact => (IDomainEvent)new BoardFactEvent(manager, on, fact.TypeId, fact.Payload)).ToArray();

    /// <summary>The offer item as its own manager's, or the reason it is not one.</summary>
    public static TranslationMessage? CheckOffer(ManagerId manager, string itemId, CommandContext context)
    {
        var item = InboxHandlers.OwnItem(manager, itemId, context);
        if (item is null)
        {
            return TranslationMessage.Of(InboxKeys.ItemUnknown);
        }

        return item.Kind == BoardEngine.OfferKind ? null : TranslationMessage.Of(BoardKeys.NotAnOffer);
    }
}

public sealed class AcceptJobOfferHandler : CommandHandler<AcceptJobOfferCommand>
{
    private readonly ResolveInboxItemHandler _inner = new();

    protected override TranslationMessage? ValidateTyped(AcceptJobOfferCommand command, CommandContext context) =>
        BoardEvents.CheckOffer(command.ManagerId, command.ItemId, context)
        ?? _inner.Validate(Resolve(command), context);

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(AcceptJobOfferCommand command, CommandContext context) =>
        _inner.Execute(Resolve(command), context);

    private static ResolveInboxItemCommand Resolve(AcceptJobOfferCommand command) => new()
    {
        ManagerId = command.ManagerId,
        IssuedOn = command.IssuedOn,
        ItemId = command.ItemId,
        OptionId = BoardEngine.OptionAccept,
    };
}

public sealed class DeclineJobOfferHandler : CommandHandler<DeclineJobOfferCommand>
{
    private readonly ResolveInboxItemHandler _inner = new();

    protected override TranslationMessage? ValidateTyped(DeclineJobOfferCommand command, CommandContext context) =>
        BoardEvents.CheckOffer(command.ManagerId, command.ItemId, context)
        ?? _inner.Validate(Resolve(command), context);

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(DeclineJobOfferCommand command, CommandContext context) =>
        _inner.Execute(Resolve(command), context);

    private static ResolveInboxItemCommand Resolve(DeclineJobOfferCommand command) => new()
    {
        ManagerId = command.ManagerId,
        IssuedOn = command.IssuedOn,
        ItemId = command.ItemId,
        OptionId = BoardEngine.OptionDecline,
    };
}

public sealed class ResignFromTeamHandler : CommandHandler<ResignFromTeamCommand>
{
    protected override TranslationMessage? ValidateTyped(ResignFromTeamCommand command, CommandContext context) =>
        BoardEngine.From(context).ValidateResign(command.ManagerId);

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ResignFromTeamCommand command, CommandContext context) =>
        BoardEvents.Wrap(
            command.ManagerId,
            command.IssuedOn,
            BoardEngine.From(context).Resign(command.ManagerId, InboxBook.ToGameDate(command.IssuedOn)));
}

/// <summary>
/// A human manager takes over an existing team at the start of a career (PP-050, path A). Founding a team is not this
/// command: <see cref="CareerStartPath"/> refuses that path and this handler never creates an organization.
/// </summary>
public sealed record TakeOverTeamCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string GivenName { get; init; }

    public required string FamilyName { get; init; }

    public required string Nationality { get; init; }

    /// <summary>A principal attribute key, or <see cref="PlayerEstimates.NoTilt"/>.</summary>
    public required string Tilt { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed class TakeOverTeamHandler : CommandHandler<TakeOverTeamCommand>
{
    protected override TranslationMessage? ValidateTyped(TakeOverTeamCommand command, CommandContext context) =>
        BoardEngine.From(context).ValidateTakeOver(
            command.ManagerId,
            command.OrganizationId,
            command.GivenName,
            command.FamilyName,
            command.Nationality,
            command.Tilt);

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(TakeOverTeamCommand command, CommandContext context) =>
        BoardEvents.Wrap(
            command.ManagerId,
            command.IssuedOn,
            BoardEngine.From(context).TakeOver(
                command.ManagerId,
                command.OrganizationId,
                command.GivenName,
                command.FamilyName,
                command.Nationality,
                command.Tilt,
                InboxBook.ToGameDate(command.IssuedOn)));
}

/// <summary>Registers the board handlers and the inbox resolver of a job offer.</summary>
public static class BoardRegistration
{
    public static void Register(CommandDispatcher dispatcher, InboxResolvers resolvers)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(resolvers);
        dispatcher.Register(new AcceptJobOfferHandler());
        dispatcher.Register(new DeclineJobOfferHandler());
        dispatcher.Register(new ResignFromTeamHandler());
        dispatcher.Register(new TakeOverTeamHandler());
        resolvers.Register(new JobOfferResolver());
        resolvers.Register(new SeasonTargetResolver());
    }
}

/// <summary>The save entries of the board commands (see <see cref="CommandCodec"/>).</summary>
public static class BoardCommandCodecs
{
    public static IReadOnlyList<CommandCodecEntry> Entries { get; } =
    [
        CommandCodecEntry.For<AcceptJobOfferCommand>(
            "board.acceptJobOffer/1",
            command => FlatJson.Write(("itemId", command.ItemId)),
            (body, manager, issued) => new AcceptJobOfferCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                ItemId = FlatJson.Read(body, "itemId").String("itemId"),
            }),
        CommandCodecEntry.For<DeclineJobOfferCommand>(
            "board.declineJobOffer/1",
            command => FlatJson.Write(("itemId", command.ItemId)),
            (body, manager, issued) => new DeclineJobOfferCommand
            {
                ManagerId = manager,
                IssuedOn = issued,
                ItemId = FlatJson.Read(body, "itemId").String("itemId"),
            }),
        CommandCodecEntry.For<ResignFromTeamCommand>(
            "board.resign/1",
            _ => FlatJson.Write(),
            (body, manager, issued) =>
            {
                FlatJson.Read(body);
                return new ResignFromTeamCommand { ManagerId = manager, IssuedOn = issued };
            }),
        CommandCodecEntry.For<TakeOverTeamCommand>(
            "board.takeOverTeam/1",
            command => FlatJson.Write(
                ("organization", command.OrganizationId),
                ("given", command.GivenName),
                ("family", command.FamilyName),
                ("nationality", command.Nationality),
                ("tilt", command.Tilt)),
            (body, manager, issued) =>
            {
                var fields = FlatJson.Read(body, "organization", "given", "family", "nationality", "tilt");
                return new TakeOverTeamCommand
                {
                    ManagerId = manager,
                    IssuedOn = issued,
                    OrganizationId = fields.String("organization"),
                    GivenName = fields.String("given"),
                    FamilyName = fields.String("family"),
                    Nationality = fields.String("nationality"),
                    Tilt = fields.String("tilt"),
                };
            }),
    ];
}

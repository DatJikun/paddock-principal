using System.Globalization;
using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Regulation;

/// <summary>
/// A team pays for a proposal to change one rule or one circuit of the calendar of its series from the next season on (#275,
/// owner decision 6). The fee is charged to the ledger, is not refunded, and starts the team's cooldown. Refusals name a reason key.
/// </summary>
public sealed record ProposeRuleChangeCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string SeriesId { get; init; }

    public required string TeamId { get; init; }

    public required string DimensionId { get; init; }

    public required string Value { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// A team casts its vote on a ballot item: for a variant, for the status quo, or an abstention (which goes into the bank in vote-bank
/// mode). <see cref="Spent"/> adds banked votes to it. A vote can be changed until the deadline.
/// </summary>
public sealed record CastVoteCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string SeriesId { get; init; }

    public required string TeamId { get; init; }

    public required string ItemId { get; init; }

    public required string Option { get; init; }

    public int Spent { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public static class RegulationEventTypes
{
    public const string ProposalFiled = "regulation.proposal_filed";

    public const string VoteCast = "regulation.vote_cast";
}

public sealed record ProposalFiled(
    ManagerId ManagerId,
    DateOnly OccurredOn,
    string SeriesId,
    string TeamId,
    string DimensionId,
    string Value,
    long FeeCents,
    int ProposeFromSeason) : IDomainEvent
{
    public string TypeId => RegulationEventTypes.ProposalFiled;
}

public sealed record VoteCast(
    ManagerId ManagerId,
    DateOnly OccurredOn,
    string SeriesId,
    string TeamId,
    string ItemId,
    string Option,
    int Spent) : IDomainEvent
{
    public string TypeId => RegulationEventTypes.VoteCast;
}

internal static class RegulationCommandSupport
{
    public static TranslationMessage? Control(RegulationEnvironment environment, ManagerId manager, string teamId)
    {
        if (!CarCommandSupport.TryOrganization(teamId, out var organization))
        {
            return TranslationMessage.Of(RegulationKeys.UnknownTeam);
        }

        return environment.Control.Controls(manager, organization) ? null : TranslationMessage.Of(RegulationKeys.NoControl);
    }

    public static GameDate Day(DateOnly date) => new(date.Year, date.Month, date.Day);
}

public sealed class ProposeRuleChangeHandler : CommandHandler<ProposeRuleChangeCommand>
{
    private readonly RegulationPolitics _politics;

    public ProposeRuleChangeHandler(RegulationPolitics politics)
    {
        ArgumentNullException.ThrowIfNull(politics);
        _politics = politics;
    }

    protected override TranslationMessage? ValidateTyped(ProposeRuleChangeCommand command, CommandContext context) =>
        RegulationCommandSupport.Control(_politics.Environment, command.ManagerId, command.TeamId)
        ?? _politics.CheckPropose(command.SeriesId, command.TeamId, command.DimensionId, command.Value, RegulationCommandSupport.Day(command.IssuedOn));

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ProposeRuleChangeCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null)
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = RegulationCommandSupport.Day(command.IssuedOn);
        var fee = _politics.Propose(command.SeriesId, command.TeamId, command.DimensionId, command.Value, today);
        var from = _politics.QuoteFor(command.SeriesId, command.TeamId, today.Year)?.ProposeFromSeason ?? today.Year;
        return [new ProposalFiled(command.ManagerId, command.IssuedOn, command.SeriesId, command.TeamId, command.DimensionId, command.Value, fee, from)];
    }
}

public sealed class CastVoteHandler : CommandHandler<CastVoteCommand>
{
    private readonly RegulationPolitics _politics;

    public CastVoteHandler(RegulationPolitics politics)
    {
        ArgumentNullException.ThrowIfNull(politics);
        _politics = politics;
    }

    protected override TranslationMessage? ValidateTyped(CastVoteCommand command, CommandContext context) =>
        RegulationCommandSupport.Control(_politics.Environment, command.ManagerId, command.TeamId)
        ?? _politics.CheckCast(command.SeriesId, command.TeamId, command.ItemId, command.Option, command.Spent, RegulationCommandSupport.Day(command.IssuedOn));

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(CastVoteCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null)
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        _politics.Cast(command.SeriesId, command.TeamId, command.ItemId, command.Option, command.Spent, RegulationCommandSupport.Day(command.IssuedOn));
        return [new VoteCast(command.ManagerId, command.IssuedOn, command.SeriesId, command.TeamId, command.ItemId, command.Option, command.Spent)];
    }
}

public static class RegulationRegistration
{
    public static void Register(CommandDispatcher dispatcher, RegulationPolitics politics)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(politics);
        dispatcher.Register(new ProposeRuleChangeHandler(politics));
        dispatcher.Register(new CastVoteHandler(politics));
    }
}

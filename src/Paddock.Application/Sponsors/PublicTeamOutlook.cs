using Paddock.Application.Board;
using Paddock.Domain.Board;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Sponsors;

/// <summary>
/// Expected position from the public facts the board uses: last season's place when a history supplies it, otherwise the
/// budget rank alone (the standings system is not wired yet, so this matches <see cref="NoBoardHistory"/>).
/// </summary>
public sealed class PublicTeamOutlook : ITeamOutlook
{
    private readonly Func<WorldState> _world;
    private readonly IBoardHistory _history;

    public PublicTeamOutlook(Func<WorldState> world, IBoardHistory? history = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _history = history ?? new NoBoardHistory();
    }

    public int FieldSize(GameDate on) => Math.Max(1, PublicStrength.ActiveTeams(_world(), on).Count);

    public int? ExpectedPosition(OrganizationId organization, GameDate on)
    {
        var world = _world();
        var teams = PublicStrength.ActiveTeams(world, on);
        if (!teams.Any(team => team.Id == organization))
        {
            return null;
        }

        return ReputationModel.ExpectedPosition(
            _history.FinalPosition(organization, on.Year - 1),
            PublicStrength.BudgetRank(world, organization, on),
            teams.Count);
    }
}

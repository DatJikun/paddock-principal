using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Domain.Board;
using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Board;

/// <summary>
/// Public facts about the past that the board reads and that no system of this task owns yet (the season results live in the
/// standings system). A pure query: no state change, no RNG (INV-005).
/// </summary>
public interface IBoardHistory
{
    /// <summary>The final constructors' position of the organization in the given season, or null when unknown.</summary>
    int? FinalPosition(OrganizationId organization, int season);
}

/// <summary>No history is known, so expectations come from the budget rank alone.</summary>
public sealed class NoBoardHistory : IBoardHistory
{
    public int? FinalPosition(OrganizationId organization, int season) => null;
}

/// <summary>Fact keys the board reads beyond those of <see cref="ObjectiveFactKeys"/>.</summary>
public static class BoardFactKeys
{
    /// <summary>Optional: a number from 0 up for how well the principal developed people this season. Unknown counts as zero.</summary>
    public const string PeopleDevelopment = "board.peopleDevelopment";
}

/// <summary>
/// The live board state a host holds between days: the <see cref="BoardSection"/> and the <see cref="ObjectivesSection"/> the
/// board grants into, next to the <see cref="ContractBook"/> whose world they act on. Like the contract and inbox books it is a
/// mutable holder around immutable values; commands and the day handler replace them. Truth lives here (the board's confidence,
/// the AI principal's archetype); a manager reads it only through <see cref="BoardQuery"/> (INV-003).
/// <para>
/// <see cref="World"/> is the contract book's world, so replacing a principal changes the one world the career runs on.
/// <see cref="Into(WorldState)"/> puts both sections in a world for hashing and saving. An empty section is left out, so a world that
/// never used the board keeps its hash.
/// </para>
/// </summary>
public sealed class BoardBook
{
    private BoardSection _section;
    private ObjectivesSection _objectives;

    public BoardBook(
        ContractBook contracts,
        IObjectiveFacts facts,
        ulong masterSeed,
        IBoardHistory? history = null,
        BoardSection? section = null,
        ObjectivesSection? objectives = null,
        IBoardSeverance? severance = null,
        PublicRankKeys? ranking = null)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(facts);
        Contracts = contracts;
        Facts = facts;
        MasterSeed = masterSeed;
        History = history ?? new NoBoardHistory();
        Severance = severance ?? new NoSeverance();
        Ranking = ranking;
        _section = section ?? contracts.World.Section<BoardSection>(BoardSection.SectionName) ?? BoardSection.Empty;
        _objectives = objectives ?? contracts.World.Section<ObjectivesSection>(ObjectivesSection.SectionName) ?? ObjectivesSection.Empty;
    }

    public ContractBook Contracts { get; }

    public IObjectiveFacts Facts { get; }

    public IBoardHistory History { get; }

    /// <summary>What orders two teams of one budget level: the previous place, then the car strength (#234). Null means the id decides.</summary>
    public PublicRankKeys? Ranking { get; }

    /// <summary>Where the severance of a dismissed manager is paid from (the finance ledger in a career). Nothing is paid by default.</summary>
    public IBoardSeverance Severance { get; }

    /// <summary>The master seed that keyed draws derive from (INV-004).</summary>
    public ulong MasterSeed { get; }

    /// <summary>
    /// The board. When the contract book is bound to a host's world (<see cref="ContractBook.IsLive"/>) the section is read from and
    /// written to that world, so the board, the contracts and every other system see one state; otherwise the book keeps its own copy.
    /// </summary>
    public BoardSection Section =>
        Contracts.IsLive ? Contracts.World.Section<BoardSection>(BoardSection.SectionName) ?? BoardSection.Empty : _section;

    /// <summary>The objectives. Read from and written to the host's world when the contract book is bound to one, like <see cref="Section"/>.</summary>
    public ObjectivesSection Objectives =>
        Contracts.IsLive ? Contracts.World.Section<ObjectivesSection>(ObjectivesSection.SectionName) ?? ObjectivesSection.Empty : _objectives;

    public WorldState World => Contracts.World;

    /// <summary>
    /// The given world with the board and its objectives in it. A section that holds nothing is left out. A book bound to a host's
    /// world has nothing to add: its sections already are in that world.
    /// </summary>
    public WorldState Into(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (Contracts.IsLive)
        {
            return world;
        }

        world = _section.IsEmpty ? world.WithoutSection(BoardSection.SectionName) : world.WithSection(_section);
        return _objectives.NextNumber == 1 ? world.WithoutSection(ObjectivesSection.SectionName) : world.WithSection(_objectives);
    }

    /// <summary>The contract book's world with the contract section, the board and its objectives in it.</summary>
    public WorldState Into() => Into(Contracts.Into());

    /// <summary>Which organization a manager runs, from the board. Managers the board does not track fall back to <paramref name="inner"/>.</summary>
    public TenureOrganizations Organizations(IManagerOrganizations? inner = null) => new(() => Section, inner);

    /// <summary>Who runs what, from the board. Managers the board does not track fall back to <paramref name="inner"/>.</summary>
    public TenureControl Control(IOrganizationControl? inner = null) => new(() => Section, inner);

    internal void Update(BoardSection section)
    {
        if (!Contracts.IsLive)
        {
            _section = section;
            return;
        }

        var world = Contracts.World;
        Contracts.UseWorld(section.IsEmpty ? world.WithoutSection(BoardSection.SectionName) : world.WithSection(section));
    }

    internal void Update(ObjectivesSection objectives)
    {
        if (!Contracts.IsLive)
        {
            _objectives = objectives;
            return;
        }

        var world = Contracts.World;
        Contracts.UseWorld(objectives.NextNumber == 1 ? world.WithoutSection(ObjectivesSection.SectionName) : world.WithSection(objectives));
    }
}

/// <summary>
/// Pays the severance of a manager the board dismissed. The finance ledger implements it in a career (T37); it is a port so the
/// board does not reference the ledger. Called once, inside the dismissal, for an amount above zero.
/// </summary>
public interface IBoardSeverance
{
    /// <summary>Records that <paramref name="payer"/> paid <paramref name="amount"/> (whole nominal dollars) to the manager.</summary>
    void Pay(OrganizationId payer, string managerId, long amount, GameDate on);
}

/// <summary>Nothing is paid or recorded beyond the board's own record of it. The default when there is no ledger.</summary>
public sealed class NoSeverance : IBoardSeverance
{
    public void Pay(OrganizationId payer, string managerId, long amount, GameDate on)
    {
    }
}

/// <summary>
/// The reputation of the principal of each team, read from the board section, for the negotiation hook of T39
/// (<see cref="IReputationSource"/>). The section is read through a delegate because the source has to exist before the board does
/// (the contract environment is built first): build it with <c>() =&gt; board.Section</c> over a variable you assign later.
/// A team with no board or no principal reads as the neutral reputation, which shifts nothing.
/// </summary>
public sealed class BoardReputationSource : IReputationSource
{
    private readonly Func<BoardSection> _section;

    public BoardReputationSource(Func<BoardSection> section)
    {
        ArgumentNullException.ThrowIfNull(section);
        _section = section;
    }

    public double Reputation(OrganizationId organization, GameDate on) =>
        (_section().ReputationOfPrincipal(organization) ?? BoardEstimates.NeutralReputationTenths) / 10.0;
}

/// <summary>Which organization a manager runs as team principal, read from the board (the neutral observer has none).</summary>
public sealed class TenureOrganizations : IManagerOrganizations
{
    private readonly Func<BoardSection> _section;
    private readonly IManagerOrganizations? _inner;

    public TenureOrganizations(Func<BoardSection> section, IManagerOrganizations? inner = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        _section = section;
        _inner = inner;
    }

    public OrganizationId? OrganizationOf(string managerId)
    {
        ArgumentNullException.ThrowIfNull(managerId);
        var section = _section();
        if (section.OrganizationOf(managerId) is OrganizationId own)
        {
            return own;
        }

        // A manager the board has dismissed or that resigned has no team, whatever a host table still says.
        return section.UnemployedManager(managerId) is not null ? null : _inner?.OrganizationOf(managerId);
    }
}

/// <summary>
/// Who runs what, from the board: a manager runs the team the board says. The host table (<paramref name="inner"/>) still answers
/// for the managers the board does not track (the AI managers), so one dismissed human can never keep ordering a team around.
/// </summary>
public sealed class TenureControl : IOrganizationControl
{
    private readonly Func<BoardSection> _section;
    private readonly IOrganizationControl? _inner;

    public TenureControl(Func<BoardSection> section, IOrganizationControl? inner = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        _section = section;
        _inner = inner;
    }

    public bool Controls(ManagerId manager, OrganizationId organization)
    {
        if (!manager.IsAssigned || !organization.IsAssigned)
        {
            return false;
        }

        var section = _section();
        if (section.OrganizationOf(manager.Value) is OrganizationId own)
        {
            return own == organization;
        }

        return section.UnemployedManager(manager.Value) is null && _inner?.Controls(manager, organization) == true
            && section.Board(organization)?.Principal is not { Kind: PrincipalKind.Human };
    }

    public IReadOnlyList<ManagerId> ManagersOf(OrganizationId organization)
    {
        if (!organization.IsAssigned)
        {
            return [];
        }

        var section = _section();
        if (section.Board(organization)?.Principal is { Kind: PrincipalKind.Human } principal)
        {
            return [new ManagerId(principal.Subject)];
        }

        return (_inner?.ManagersOf(organization) ?? [])
            .Where(manager => section.UnemployedManager(manager.Value) is null)
            .ToArray();
    }
}

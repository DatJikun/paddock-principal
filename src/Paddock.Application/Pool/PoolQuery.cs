using Paddock.Application.Access;
using Paddock.Application.Objectives;
using Paddock.Domain.Finance;
using Paddock.Domain.Pool;
using Paddock.Domain.World;

namespace Paddock.Application.Pool;

/// <summary>One band the viewer's organization believes, on the 1-20 scale (PP-013). Not the true number.</summary>
public sealed record PoolBandView(string Key, int Low, int High);

/// <summary>
/// One pool member as a manager reads him: name, nationality, age, and bands. There is no field for the true attributes, the
/// hidden potential, or whether he is a real driver (INV-003, PP-018). The handle is the pool's own number for him.
/// <paramref name="Attributes"/> and <paramref name="Potential"/> are empty/null until the viewer's organization has observed him.
/// <paramref name="YourFunding"/> is what the viewer's organization has paid for him this season, if anything.
/// <paramref name="InYourAcademy"/> is true for a junior the viewer's team recruited; <paramref name="SeasonsLeft"/> is then how many season
/// changes he can still spend in the pool before his career lapses without a contract (#268), and null for anyone else.
/// </summary>
public sealed record PoolItemView(
    string Handle,
    string GivenName,
    string FamilyName,
    string Nationality,
    int Age,
    IReadOnlyList<PoolBandView> Attributes,
    PoolBandView? Potential,
    JuniorProgramme? YourFunding,
    bool InYourAcademy,
    int? SeasonsLeft);

/// <summary>One programme a team can pay for a junior of its academy: what a season costs and how fast he then moves toward his potential.</summary>
public sealed record ProgrammeView(JuniorProgramme Programme, int SpeedPercent, long CostCents);

/// <summary>The pool as one viewer sees it. <paramref name="Focus"/> and <paramref name="FocusHandle"/> are the viewer's own scouting focus.</summary>
public sealed record PoolView(
    AccessContext Viewer,
    IReadOnlyList<PoolItemView> Items,
    ScoutFocusKind? Focus,
    string? FocusHandle,
    int AcademySlots,
    int AcademyUsed,
    int BaseSpeedPercent,
    IReadOnlyList<ProgrammeView> Programmes);

/// <summary>
/// The read side of the talent pool (INV-003, INV-005). A manager or an AI manager sees the members with the bands their own
/// organization has built by scouting; the developer sees the members and no bands, because the truth has its own explicit query
/// on the world. A team sees every junior on the market and the juniors of its own academy (#268), never a junior
/// recruited by another academy. It changes nothing and draws no RNG.
/// </summary>
public sealed class PoolQuery
{
    private readonly PoolBook _book;
    private readonly IManagerOrganizations _organizations;
    private readonly IJuniorFunding _funding;

    public PoolQuery(PoolBook book, IManagerOrganizations organizations, IJuniorFunding? funding = null)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(organizations);
        _book = book;
        _organizations = organizations;
        _funding = funding ?? UnmeteredJuniorFunding.Instance;
    }

    public PoolView View(AccessContext access)
    {
        ArgumentNullException.ThrowIfNull(access);
        var world = _book.World;
        var section = _book.Section;
        var organization = Organization(access);
        var items = new List<PoolItemView>();
        var season = world.CurrentDate.Year;
        foreach (var member in section.Members.OrderBy(member => member.Handle))
        {
            if (member.Academy is { } holder && organization is OrganizationId viewing && viewing != holder)
            {
                continue;
            }

            var person = world.GetPerson(member.Id);
            var attributes = new List<PoolBandView>();
            PoolBandView? potential = null;
            if (organization is OrganizationId own && world.KnowledgeOf(own, member.Id) is { } belief)
            {
                attributes.AddRange(belief.Attributes.Select(known => new PoolBandView(known.Key, known.Band.Low, known.Band.High)));
                potential = belief.Potential is { } band ? new PoolBandView("potential", band.Low, band.High) : null;
            }

            JuniorProgramme? funding = member.Funding is { } paid && organization is OrganizationId payer && paid.Funder == payer
                ? paid.Programme
                : null;
            var inAcademy = organization is OrganizationId owner && member.Academy == owner;
            items.Add(new PoolItemView(
                member.HandleText,
                person.GivenName,
                person.FamilyName,
                person.Nationality,
                person.AgeOn(world.CurrentDate),
                attributes,
                potential,
                funding,
                inAcademy,
                inAcademy ? SeasonsLeft(member, person.BirthDate.Year, season) : null));
        }

        var focus = organization is OrganizationId viewer ? section.FocusOf(viewer) : null;
        string? focusHandle = focus?.Person is PersonId target ? section.Find(target)?.HandleText : null;
        return new PoolView(
            access,
            items,
            focus?.Kind,
            focusHandle,
            PoolEstimates.AcademySlots,
            organization is OrganizationId team ? section.AcademyCount(team) : 0,
            100,
            Enum.GetValues<JuniorProgramme>()
                .Select(programme => new ProgrammeView(programme, PoolEstimates.SpeedPercent(programme), Money.FromDollars(_funding.Cost(programme)).Cents))
                .ToArray());
    }

    /// <summary>
    /// The person behind a pool handle, for the viewer who may see him, or null for any other text. A row of the academy opens the same
    /// profile as a row of the market through this (#326): the caller reads the person, the screen keeps the handle, so the id of a pool
    /// member never reaches the view (PP-018). A junior of another academy is no one's here, as in <see cref="View"/>.
    /// </summary>
    public PersonId? Resolve(AccessContext access, string handle)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(handle);
        var member = _book.Section.FindByHandle(handle);
        if (member is null)
        {
            return null;
        }

        return member.Academy is { } holder && Organization(access) is OrganizationId viewing && viewing != holder ? null : member.Id;
    }

    /// <summary>
    /// How many season changes a junior can still spend in the pool: his career lapses after <see cref="PoolEstimates.MaxSeasonsInPool"/>
    /// seasons, or once he is older than <see cref="PoolEstimates.MaxAge"/>, whichever comes first. The same rule the day handler applies.
    /// </summary>
    internal static int SeasonsLeft(PoolMember member, int birthYear, int season)
    {
        var byTime = member.EnteredOn.Year + PoolEstimates.MaxSeasonsInPool - season;
        var byAge = birthYear + PoolEstimates.MaxAge + 1 - season;
        return Math.Max(0, Math.Min(byTime, byAge));
    }

    private OrganizationId? Organization(AccessContext access)
    {
        if (access.Kind == AccessKind.Developer)
        {
            return null;
        }

        var manager = access.Manager ?? throw new InvalidOperationException("Non-developer context without a manager.");
        return _organizations.OrganizationOf(manager.Value);
    }
}

/// <summary>
/// What an AI manager is allowed to know about the pool, as facts (<see cref="IKnowledgeView"/>). A fact is
/// <c>pool.{handle}.{attribute}</c> or <c>pool.{handle}.potential</c> and reads as a band, never as an exact number, and it is
/// unknown until the viewer's organization has scouted the person. The viewer's own beliefs are the only source: nothing here
/// reaches the true attributes, and there is no fact that says whether a person is real (INV-003, PP-018).
/// </summary>
public sealed class PoolKnowledgeView : IKnowledgeView
{
    private readonly PoolBook _book;
    private readonly IManagerOrganizations _organizations;

    public PoolKnowledgeView(AccessContext viewer, PoolBook book, IManagerOrganizations organizations)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(organizations);
        Viewer = viewer;
        _book = book;
        _organizations = organizations;
    }

    public AccessContext Viewer { get; }

    public Known<double> Get(FactKey fact)
    {
        var parts = fact.Value.Split('.');
        if (parts.Length != 3 || parts[0] != "pool" || Viewer.Manager is not { } manager)
        {
            return Known<double>.Unknown;
        }

        if (_organizations.OrganizationOf(manager.Value) is not { } organization
            || _book.Section.FindByHandle(parts[1]) is not { } member
            || _book.World.KnowledgeOf(organization, member.Id) is not { } belief)
        {
            return Known<double>.Unknown;
        }

        if (parts[2] == "potential")
        {
            return belief.Potential is { } band ? Known<double>.Banded(band.Low, band.High) : Known<double>.Unknown;
        }

        foreach (var known in belief.Attributes)
        {
            if (known.Key == parts[2])
            {
                return Known<double>.Banded(known.Band.Low, known.Band.High);
            }
        }

        return Known<double>.Unknown;
    }
}

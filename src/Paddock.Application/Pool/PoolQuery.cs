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
/// </summary>
public sealed record PoolItemView(
    string Handle,
    string GivenName,
    string FamilyName,
    string Nationality,
    int Age,
    IReadOnlyList<PoolBandView> Attributes,
    PoolBandView? Potential,
    JuniorProgramme? YourFunding);

/// <summary>The pool as one viewer sees it. <paramref name="Focus"/> and <paramref name="FocusHandle"/> are the viewer's own scouting focus.</summary>
public sealed record PoolView(
    AccessContext Viewer,
    IReadOnlyList<PoolItemView> Items,
    ScoutFocusKind? Focus,
    string? FocusHandle,
    long CheapProgrammeCostCents,
    long FastProgrammeCostCents);

/// <summary>
/// The read side of the talent pool (INV-003, INV-005). A manager or an AI manager sees the members with the bands their own
/// organization has built by scouting; the developer sees the members and no bands, because the truth has its own explicit query
/// on the world. It changes nothing and draws no RNG.
/// </summary>
public sealed class PoolQuery
{
    private readonly PoolBook _book;
    private readonly IManagerOrganizations _organizations;

    public PoolQuery(PoolBook book, IManagerOrganizations organizations)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(organizations);
        _book = book;
        _organizations = organizations;
    }

    public PoolView View(AccessContext access)
    {
        ArgumentNullException.ThrowIfNull(access);
        var world = _book.World;
        var section = _book.Section;
        var organization = Organization(access);
        var items = new List<PoolItemView>();
        foreach (var member in section.Members.OrderBy(member => member.Handle))
        {
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
            items.Add(new PoolItemView(
                member.HandleText,
                person.GivenName,
                person.FamilyName,
                person.Nationality,
                world.CurrentDate.Year - person.BirthDate.Year,
                attributes,
                potential,
                funding));
        }

        var focus = organization is OrganizationId viewer ? section.FocusOf(viewer) : null;
        string? focusHandle = focus?.Person is PersonId target ? section.Find(target)?.HandleText : null;
        return new PoolView(
            access,
            items,
            focus?.Kind,
            focusHandle,
            Money.FromDollars(PoolEstimates.CostOf(JuniorProgramme.CheapSlow)).Cents,
            Money.FromDollars(PoolEstimates.CostOf(JuniorProgramme.ExpensiveFast)).Cents);
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

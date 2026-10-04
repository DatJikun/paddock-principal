using System.Globalization;
using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Simulation.Pool;

/// <summary>Machine identifiers of the pool's day events. Not player text.</summary>
public static class PoolEventTypes
{
    /// <summary>A person joined the pool (a scheduled real driver or a filler).</summary>
    public const string Entered = "pool.entered";

    /// <summary>A member aged out of the pool without a contract: the career that would have followed does not happen. For the chronicle.</summary>
    public const string CareerLapsed = "pool.career_lapsed";
}

/// <summary>
/// How many Grand Prix the organization shared with a person this season. The racing weekend is not part of T40, so the default
/// source says zero; the weekend task replaces it (shared races narrow scouting bands, DESIGN 10). A pure query (INV-005).
/// </summary>
public interface ISharedRaceSource
{
    int SharedRaces(OrganizationId organization, PersonId person, GameDate on);
}

/// <summary>No shared races. The pool has no racing of its own.</summary>
public sealed class NoSharedRaces : ISharedRaceSource
{
    public static NoSharedRaces Instance { get; } = new();

    public int SharedRaces(OrganizationId organization, PersonId person, GameDate on) => 0;
}

/// <summary>Settings of the pool handler. The defaults are the ESTIMATES in <see cref="PoolEstimates"/>.</summary>
public sealed class TalentPoolOptions
{
    /// <summary>Fillers top the pool up to this size each season. Zero turns the fillers off.</summary>
    public int TargetSize { get; init; } = PoolEstimates.TargetSize;

    public INameSource Names { get; init; } = new FixtureNameSource();

    public INameBlocklist Blocklist { get; init; } = EmptyNameBlocklist.Instance;

    public IReadOnlyList<(QualityBand Band, int Weight)> FillerQuality { get; init; } = PoolEstimates.FillerQualityWeights;

    public IReadOnlyList<NationalityWeight> Nationalities { get; init; } =
        PoolEstimates.FillerNationalities.Select(code => new NationalityWeight(code, 1)).ToArray();

    public ISharedRaceSource SharedRaces { get; init; } = NoSharedRaces.Instance;
}

/// <summary>
/// The talent pool of a career as a day handler (T40). It replaces the placeholder intake of T18 and does, in this order:
/// <list type="number">
/// <item>every day: members who now hold a contract leave the pool (they were signed);</item>
/// <item>on the pool-entry day of each season after the opening one: members develop one season toward their hidden potential
/// (a funded junior season speeds it up), then those who have aged out without a contract lapse and
/// <see cref="PoolEventTypes.CareerLapsed"/> is emitted;</item>
/// <item>every day: scheduled real drivers whose day it is enter the pool; on the pool-entry day fictional fillers top it up to the
/// target size;</item>
/// <item>on the first of each month: the scouts of every organization with a focus observe, and what the organization believes
/// narrows (<see cref="ScoutingModel"/>).</item>
/// </list>
/// Randomness: development and fillers draw from children of the <c>People</c> stream, observation from children of the
/// <c>Scouting</c> stream, each child tagged by season and person, so neither a new filler nor extra scouting moves the other's
/// draws (INV-004). The handler holds no state of its own: the pool is the world's <see cref="TalentPoolSection"/>.
/// </summary>
public sealed class TalentPoolDayHandler : IDayHandler
{
    /// <summary>The place the T18 intake handler had. Ageing (20) must see the pool as left by this handler.</summary>
    public const int DefaultOrder = 10;

    private readonly Func<WorldState> _world;
    private readonly Action<WorldState> _setWorld;
    private readonly IReadOnlyDictionary<GameDate, ScheduledArrival[]> _arrivals;
    private readonly Action<Person, GameDate> _personAdded;
    private readonly TalentPoolOptions _options;
    private readonly int _openedYear;

    public TalentPoolDayHandler(
        Func<WorldState> world,
        Action<WorldState> setWorld,
        IReadOnlyDictionary<GameDate, ScheduledArrival[]> arrivals,
        Action<Person, GameDate> personAdded,
        TalentPoolOptions options,
        int openedYear)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(setWorld);
        ArgumentNullException.ThrowIfNull(arrivals);
        ArgumentNullException.ThrowIfNull(personAdded);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(options.TargetSize);
        if (options.TargetSize > 0 && (options.FillerQuality.Count == 0 || options.Nationalities.Count == 0))
        {
            throw new ArgumentException("Fillers need quality weights and nationalities.", nameof(options));
        }

        _world = world;
        _setWorld = setWorld;
        _arrivals = arrivals;
        _personAdded = personAdded;
        _options = options;
        _openedYear = openedYear;
    }

    public int Order => DefaultOrder;

    /// <summary>How many people have entered the pool through this handler (real arrivals and fillers).</summary>
    public int Entries { get; internal set; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = _world();
        var section = world.Section<TalentPoolSection>(TalentPoolSection.SectionName) ?? TalentPoolSection.Empty;
        var today = context.Today;
        var entryDay = today.Month == CareerDayEstimates.PoolEntryMonth
            && today.Day == CareerDayEstimates.PoolEntryDay
            && today.Year > _openedYear;

        section = LeaveSigned(world, section, today);
        if (entryDay && section.Count > 0)
        {
            (world, section) = Develop(world, section, context);
            section = Lapse(world, section, context);
        }

        var entrants = new List<PersonId>();
        if (_arrivals.TryGetValue(today, out var due))
        {
            foreach (var arrival in due)
            {
                var realId = arrival.Spec.RealId;
                if (realId is not null && world.Ids.WasIssued(realId))
                {
                    throw new InvalidOperationException("Scheduled person '" + realId + "' is already in the world.");
                }

                (world, var id) = world.AddPerson(arrival.Spec);
                _personAdded(world.GetPerson(id), today);
                entrants.Add(id);
            }
        }

        if (entryDay && _options.TargetSize > 0)
        {
            var people = PeopleStream(context);
            var missing = _options.TargetSize - (section.Count + entrants.Count);
            for (var index = 1; index <= missing; index++)
            {
                var filler = PoolFillers.Create(
                    people,
                    today.Year,
                    index,
                    world.Ids.NextPerson,
                    _options.Names,
                    _options.Blocklist,
                    _options.FillerQuality,
                    _options.Nationalities);
                (world, var id) = world.AddPerson(filler.Spec);
                if (!string.Equals(id.Value, filler.ExpectedId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Generated person id " + filler.ExpectedId + " does not match the world id " + id.Value + ".");
                }

                _personAdded(world.GetPerson(id), today);
                entrants.Add(id);
            }
        }

        if (entrants.Count > 0)
        {
            section = section.EnterAll(entrants, today);
            foreach (var id in entrants)
            {
                Entries++;
                context.Emit(PoolEventTypes.Entered, new MarkerPayload(id.Value));
            }
        }

        if (today.Day == PoolEstimates.ScoutingDayOfMonth && section.Focuses.Count > 0)
        {
            (world, section) = Scout(world, section, context);
        }

        _setWorld(world.WithSection(section));
    }

    private static TalentPoolSection LeaveSigned(WorldState world, TalentPoolSection section, GameDate today)
    {
        if (section.Count == 0)
        {
            return section;
        }

        var contracted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contract in world.Contracts)
        {
            if (contract.IsActiveOn(today))
            {
                contracted.Add(contract.PersonId.Value);
            }
        }

        foreach (var member in section.Members)
        {
            if (contracted.Contains(member.Id.Value))
            {
                section = section.Leave(member.Id);
            }
        }

        return section;
    }

    private static (WorldState World, TalentPoolSection Section) Develop(WorldState world, TalentPoolSection section, DayContext context)
    {
        var people = PeopleStream(context);
        var year = context.Today.Year;
        foreach (var member in section.Members)
        {
            // A season that was paid for in an earlier year acts now. Anything older than that has already acted or lapsed.
            var speed = member.Funding is { } funding && funding.Season < year
                ? PoolEstimates.SpeedPercent(funding.Programme)
                : 100;
            var rng = people.DeriveChild("pool-dev:v1:" + year.ToString(CultureInfo.InvariantCulture) + ":" + member.Id.Value);
            var truth = PoolDevelopment.Develop(world.TruthOf(member.Id), speed, rng);
            world = world.WithPersonTruth(member.Id, truth);
            foreach (var belief in world.Knowledge.Where(belief => belief.SubjectId == member.Id).ToArray())
            {
                world = world.SetKnowledge(ScoutingModel.Stale(belief));
            }

            if (member.Funding is { } paid && paid.Season < year)
            {
                section = section.ClearFunding(member.Id);
            }
        }

        return (world, section);
    }

    private static TalentPoolSection Lapse(WorldState world, TalentPoolSection section, DayContext context)
    {
        var today = context.Today;
        foreach (var member in section.Members)
        {
            var seasons = today.Year - member.EnteredOn.Year;
            var age = today.Year - world.GetPerson(member.Id).BirthDate.Year;
            var agedOut = age > PoolEstimates.MaxAge && seasons >= PoolEstimates.MinSeasonsBeforeAgeLapse;
            if (seasons < PoolEstimates.MaxSeasonsInPool && !agedOut)
            {
                continue;
            }

            section = section.Lapse(member.Id, today);
            context.Emit(PoolEventTypes.CareerLapsed, new MarkerPayload(member.Id.Value));
        }

        return section;
    }

    private (WorldState World, TalentPoolSection Section) Scout(WorldState world, TalentPoolSection section, DayContext context)
    {
        var today = context.Today;
        var organizations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var organization in world.Organizations)
        {
            organizations.Add(organization.Id.Value);
        }

        var scouting = new RngStream(RngStreamName.Scouting, context.Stream(RngStreamName.Scouting).State);
        foreach (var focus in section.Focuses)
        {
            if (!organizations.Contains(focus.Organization.Value))
            {
                continue;
            }

            var scout = ScoutOf(world, focus.Organization, today);
            var targets = focus.Kind == ScoutFocusKind.Person
                ? [section.Find(focus.Person!.Value)!]
                : section.Members;
            foreach (var member in targets)
            {
                var shared = _options.SharedRaces.SharedRaces(focus.Organization, member.Id, today);
                section = section.AddObservation(focus.Organization, member.Id, ScoutingModel.MonthlyMilli(focus.Kind, scout, shared));
                var rng = scouting.DeriveChild(
                    "bias:v1:" + today.Year.ToString(CultureInfo.InvariantCulture) + ":" + focus.Organization.Value + ":" + member.Id.Value);
                var belief = ScoutingModel.Observe(
                    focus.Organization,
                    member.Id,
                    world.TruthOf(member.Id),
                    scout,
                    section.ObservedMilli(focus.Organization, member.Id),
                    rng);
                world = world.SetKnowledge(belief);
            }
        }

        return (world, section);
    }

    /// <summary>The best scout the organization has under contract today, or the default for one without a scout.</summary>
    private static ScoutProfile ScoutOf(WorldState world, OrganizationId organization, GameDate today)
    {
        ScoutProfile? best = null;
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != organization
                || !contract.IsActiveOn(today)
                || !contract.Role.IsStaff
                || contract.Role.StaffRole != StaffRole.Scout)
            {
                continue;
            }

            if (ScoutProfile.From(world.TruthOf(contract.PersonId)) is { } profile
                && (best is null || profile.TalentJudgement > best.Value.TalentJudgement))
            {
                best = profile;
            }
        }

        return best ?? ScoutProfile.Unscouted;
    }

    private static RngStream PeopleStream(DayContext context) =>
        new(RngStreamName.People, context.Stream(RngStreamName.People).State);
}

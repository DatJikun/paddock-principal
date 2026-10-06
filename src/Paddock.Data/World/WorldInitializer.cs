using System.Globalization;
using System.Text;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Domain.Cars;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Data.World;

/// <summary>
/// Optional inputs of <see cref="WorldInitializer.Create"/>. Without names the fixture name table is used
/// (R12 names are not wired yet). Without constructor names an organization is named after its id.
/// </summary>
public sealed record WorldInitOptions(
    INameSource? Names = null,
    INameBlocklist? Blocklist = null,
    IReadOnlyDictionary<string, string>? ConstructorNames = null,
    ICarStrengthSource? CarStrength = null,
    ITeamTierSource? Tiers = null);

/// <summary>
/// Builds the <see cref="WorldState"/> of a career start (T20): organizations with lineage and engine
/// suppliers, key staff, drivers and two cars per team by <see cref="PeopleSource"/>. Pure: no I/O and no clock.
/// People and driver-fit rows use the People stream. Generated car tiers use the Development stream inside
/// <see cref="InitialCarFactory"/>, which this file calls. Same seed, config, data and provider give the same
/// <see cref="WorldState.StateHash"/>. Numbers that are guesses are in <see cref="WorldInitEstimates"/>.
/// </summary>
public static class WorldInitializer
{
    public static WorldInitResult Create(
        CareerConfig config,
        AuthoredData data,
        IPeopleProvider people,
        ulong masterSeed,
        WorldInitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(people);
        var validation = config.Validate();
        if (!validation.IsValid)
        {
            throw new WorldInitException(
                WorldInitErrorCodes.InvalidConfig,
                validation.Errors.Select(error => error.Code).ToArray());
        }

        return new Builder(config, data, people, masterSeed, options ?? new WorldInitOptions()).Build();
    }

    /// <summary>
    /// Teams a new career can take over in <paramref name="year"/>, from public information only: the authored id and the
    /// name the world would show. Indianapolis-only entries are left out, the same way <see cref="Create"/> leaves them out.
    /// This does not build a world and does not draw RNG.
    /// </summary>
    public static IReadOnlyList<PublicTeam> PublicTeams(AuthoredData data, int year)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (year < CareerConfig.MinStartYear)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "The career cannot start before 1950.");
        }

        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(year);
        var builder = new Builder(config, data, EmptyPeopleProvider.Instance, 1UL, new WorldInitOptions());
        return builder.Roster();
    }

    /// <summary>One existing team a player may take over. <see cref="Name"/> is the public name, not a hidden rating.</summary>
    public sealed record PublicTeam(string Id, string Name);

    private sealed record TeamPlan(
        string Id,
        int FromYear,
        int? ToYear,
        int FoundedYear,
        string? Country,
        bool FoundedFromData);

    private sealed record StaffSlot(string PersonId, StaffMember Member, OrganizationId Organization, StaffRole Role);

    private sealed class Builder
    {
        private static readonly StringComparer Ordinal = StringComparer.Ordinal;

        private readonly CareerConfig _config;
        private readonly AuthoredData _data;
        private readonly IPeopleProvider _provider;
        private readonly INameSource _names;
        private readonly INameBlocklist _blocklist;
        private readonly IReadOnlyDictionary<string, string>? _constructorNames;
        private readonly ICarStrengthSource? _carStrength;
        private readonly ITeamTierSource? _tiers;
        private readonly ulong _masterSeed;
        private readonly RngStream _people;
        private readonly int _start;
        private readonly int _reference;
        private readonly SortedDictionary<string, SortedSet<string>> _gaps = new(Ordinal);
        private readonly Dictionary<string, TeamPlan> _currentTeams = new(Ordinal);
        private readonly Dictionary<string, OrganizationId> _teamIds = new(Ordinal);
        private readonly Dictionary<string, OrganizationId> _supplierIds = new(Ordinal);
        private readonly HashSet<string> _staffedOrganizations = new(Ordinal);
        private readonly HashSet<string> _realIds = new(Ordinal);
        private readonly List<PersonId> _pool = [];
        private readonly List<EngineSupplyLink> _supplies = [];
        private WorldState _world;
        private int _dissolvedTeams;
        private int _links;
        private int _racingDrivers;
        private int _staffPeople;

        public Builder(CareerConfig config, AuthoredData data, IPeopleProvider provider, ulong masterSeed, WorldInitOptions options)
        {
            _config = config;
            _data = data;
            _provider = provider;
            _names = options.Names ?? new FixtureNameSource();
            _blocklist = options.Blocklist ?? EmptyNameBlocklist.Instance;
            _constructorNames = options.ConstructorNames;
            _carStrength = options.CarStrength ?? data.CarStrength;
            _tiers = options.Tiers ?? data.TeamTiers;
            _masterSeed = masterSeed;
            _start = config.StartYear;
            var lastAuthored = data.Engines.Entries.Count == 0 ? _start : data.Engines.Entries.Max(entry => entry.Year);
            _reference = Math.Min(_start, lastAuthored);
            _people = RngStream.Derive(masterSeed, RngStreamName.People, _start);
            _world = WorldState.At(GameDate.SeasonStart(_start));
        }

        public WorldInitResult Build()
        {
            if (_reference != _start)
            {
                Gap(WorldInitGapCodes.ReferenceSeasonClamped, Number(_start));
            }

            PlanCurrentTeams();
            var player = ValidatePlayerTeam();
            AddCurrentTeams();
            AddLineage();
            AddSuppliers();
            player = AddPlayerTeam(player);

            switch (_config.PeopleSource)
            {
                case PeopleSource.FullyGenerated:
                    AddGeneratedDrivers();
                    break;
                default:
                    AddKnownDrivers();
                    break;
            }

            var slots = StaffSlots();
            if (_config.PeopleSource != PeopleSource.FullyGenerated)
            {
                AddKnownStaff(slots);
            }

            FillStaff();
            ReportTeamsWithoutStaff();
            AddCars();
            var pairings = StaffRoster.Pair(_world, GameDate.SeasonStart(_start));
            if (!pairings.IsEmpty)
            {
                _world = _world.WithSection(pairings);
            }

            return new WorldInitResult(_world, Report(), _pool.ToArray(), player, _supplies.ToArray());
        }

        // ---------------------------------------------------------------- organizations

        public IReadOnlyList<PublicTeam> Roster()
        {
            PlanCurrentTeams();
            return _currentTeams.Values
                .OrderBy(plan => plan.Id, Ordinal)
                .Select(plan => new PublicTeam(plan.Id, NameOf(plan.Id)))
                .ToArray();
        }

        private void PlanCurrentTeams()
        {
            var ids = _data.Engines.Entries
                .Where(entry => entry.Year == _reference)
                .Select(entry => entry.ConstructorId)
                .Distinct(Ordinal)
                .OrderBy(id => id, Ordinal);
            foreach (var id in ids)
            {
                if (OnlyIndianapolis500(id))
                {
                    Gap(WorldInitGapCodes.IndianapolisOnly, id);
                    continue;
                }

                var (from, to) = SpanOf(id, _reference);
                var founded = FoundedYear(id, from);
                _currentTeams[id] = new TeamPlan(id, from, to, founded.Year, CountryOf(id, from), !founded.FromFounders);
            }
        }

        private OrganizationId ValidatePlayerTeam()
        {
            if (string.Equals(_config.PlayerTeam, CareerConfig.NewTeam, StringComparison.Ordinal))
            {
                return default;
            }

            if (!_currentTeams.ContainsKey(_config.PlayerTeam))
            {
                throw new WorldInitException(
                    WorldInitErrorCodes.UnknownPlayerTeam,
                    [_config.PlayerTeam, Number(_reference)]);
            }

            return OrganizationId.Real(_config.PlayerTeam);
        }

        private void AddCurrentTeams()
        {
            foreach (var plan in _currentTeams.Values.OrderBy(team => team.Id, Ordinal))
            {
                AddTeam(plan, dissolvedYear: null);
                if (plan.FoundedFromData)
                {
                    Gap(WorldInitGapCodes.OrganizationFoundedFromData, plan.Id);
                }
            }
        }

        private void AddTeam(TeamPlan plan, int? dissolvedYear)
        {
            var founded = GameDate.SeasonStart(plan.FoundedYear);
            GameDate? dissolved = dissolvedYear is int year ? GameDate.SeasonEnd(year) : null;
            var name = NameOf(plan.Id);
            var spec = new OrganizationSpec(
                OrganizationKind.Team,
                true,
                plan.Id,
                founded,
                dissolved,
                OpeningBudget(OrganizationId.Real(plan.Id)),
                [new OrganizationNameSpan(name, founded, dissolved)]);
            (_world, var id) = _world.AddOrganization(spec);
            _teamIds[plan.Id] = id;
        }

        private void AddLineage()
        {
            var lineageIds = _data.LineageSpans
                .Select(span => span.LineageId)
                .Distinct(Ordinal)
                .OrderBy(id => id, Ordinal);
            foreach (var lineageId in lineageIds)
            {
                var chain = TeamLineage.Chain(lineageId, _data.LineageSpans);
                var currentIndexes = new List<int>();
                for (var i = 0; i < chain.Count; i++)
                {
                    if (Covers(chain[i], _reference) && _currentTeams.ContainsKey(chain[i].ConstructorId))
                    {
                        currentIndexes.Add(i);
                    }
                }

                if (currentIndexes.Count == 0)
                {
                    continue;
                }

                var members = new List<(LineageSpan Span, bool Current)>();
                for (var j = currentIndexes[0] - 1; j >= 0; j--)
                {
                    var earlier = chain[j];
                    if (_teamIds.ContainsKey(earlier.ConstructorId) || earlier.ToYear is not int last || last >= _reference)
                    {
                        Gap(WorldInitGapCodes.LineageTruncated, lineageId + ":" + earlier.ConstructorId);
                        break;
                    }

                    var founded = FoundedYear(earlier.ConstructorId, earlier.FromYear);
                    AddTeam(
                        new TeamPlan(earlier.ConstructorId, earlier.FromYear, last, founded.Year, CountryOf(earlier.ConstructorId, earlier.FromYear), !founded.FromFounders),
                        last);
                    _dissolvedTeams++;
                    members.Insert(0, (earlier, false));
                }

                foreach (var index in currentIndexes)
                {
                    members.Add((chain[index], true));
                }

                for (var k = 1; k < members.Count; k++)
                {
                    var (before, _) = members[k - 1];
                    var (after, afterCurrent) = members[k];
                    GameDate? to = afterCurrent ? null : GameDate.SeasonEnd(after.ToYear!.Value);
                    _world = _world.LinkLineage(
                        _teamIds[before.ConstructorId],
                        _teamIds[after.ConstructorId],
                        GameDate.SeasonStart(after.FromYear),
                        to);
                    _links++;
                }
            }
        }

        private void AddSuppliers()
        {
            var firstYear = new Dictionary<string, int>(Ordinal);
            foreach (var entry in _data.Engines.Entries)
            {
                if (entry.Year > _reference)
                {
                    continue;
                }

                var slug = Slug(entry.Supplier);
                if (slug.Length == 0)
                {
                    continue;
                }

                firstYear[slug] = firstYear.TryGetValue(slug, out var known) ? Math.Min(known, entry.Year) : entry.Year;
            }

            var today = _data.Engines.Entries
                .Where(entry => entry.Year == _reference)
                .OrderBy(entry => entry.ConstructorId, Ordinal)
                .ThenBy(entry => entry.Supplier, Ordinal)
                .ThenBy(entry => entry.EngineName, Ordinal)
                .ToArray();
            foreach (var entry in today)
            {
                if (!_teamIds.ContainsKey(entry.ConstructorId))
                {
                    continue;
                }

                var slug = Slug(entry.Supplier);
                if (slug.Length == 0 || string.Equals(slug, "unknown", StringComparison.Ordinal))
                {
                    Gap(WorldInitGapCodes.SupplierUnknown, entry.ConstructorId);
                    continue;
                }

                if (!_supplierIds.TryGetValue(slug, out var supplierId))
                {
                    var founded = GameDate.SeasonStart(firstYear[slug]);
                    var spec = new OrganizationSpec(
                        OrganizationKind.EngineSupplier,
                        true,
                        SupplierIdText(slug),
                        founded,
                        null,
                        WorldInitEstimates.PlaceholderBudget,
                        [new OrganizationNameSpan(entry.Supplier.Trim(), founded, null)]);
                    (_world, supplierId) = _world.AddOrganization(spec);
                    _supplierIds[slug] = supplierId;
                    Gap(WorldInitGapCodes.SupplierFoundedFromData, supplierId.Value);
                }

                _supplies.Add(new EngineSupplyLink(
                    _teamIds[entry.ConstructorId],
                    supplierId,
                    entry.EngineName,
                    entry.Type));
            }
        }

        private OrganizationId AddPlayerTeam(OrganizationId existing)
        {
            if (existing.IsAssigned)
            {
                return existing;
            }

            var founded = GameDate.SeasonStart(_start);
            var spec = new OrganizationSpec(
                OrganizationKind.Team,
                false,
                null,
                founded,
                null,
                OpeningBudget(null),
                [new OrganizationNameSpan(CareerConfig.NewTeam, founded, null)]);
            (_world, var id) = _world.AddOrganization(spec);
            return id;
        }

        /// <summary>
        /// The opening budget of a team in whole dollars: the era budget of its tier, which is the capital finance opens its books
        /// with (<c>OpenBooksHandler</c>). A team the tier source does not know, and a new team of the player (null), is a typical
        /// one. Without era budgets the money is not modelled and every team keeps the placeholder (#234).
        /// </summary>
        private long OpeningBudget(OrganizationId? team)
        {
            if (!EraFinance.Covers(_data.EraPeriods, _start))
            {
                return WorldInitEstimates.PlaceholderBudget;
            }

            var tier = team is OrganizationId known && _tiers is not null ? _tiers.TierOf(known, _start) : TeamTier.Typical;
            return EraFinance.ForYear(_data.EraPeriods, _start).Dollars(tier);
        }

        private (int From, int? To) SpanOf(string constructorId, int year)
        {
            foreach (var span in _data.LineageSpans)
            {
                if (string.Equals(span.ConstructorId, constructorId, StringComparison.Ordinal) && Covers(span, year))
                {
                    return (span.FromYear, span.ToYear);
                }
            }

            foreach (var organization in _data.Founders.Organizations)
            {
                foreach (var entry in organization.ConstructorEntries)
                {
                    if (string.Equals(entry.ConstructorId, constructorId, StringComparison.Ordinal)
                        && entry.From <= year && year <= entry.To)
                    {
                        return (entry.From, entry.To);
                    }
                }
            }

            var seasons = _data.Engines.Entries
                .Where(entry => string.Equals(entry.ConstructorId, constructorId, StringComparison.Ordinal))
                .Select(entry => entry.Year)
                .ToHashSet();
            var from = year;
            while (seasons.Contains(from - 1))
            {
                from--;
            }

            return (from, null);
        }

        private (int Year, bool FromFounders) FoundedYear(string constructorId, int spanFrom)
        {
            foreach (var organization in _data.Founders.Organizations)
            {
                foreach (var entry in organization.ConstructorEntries)
                {
                    if (!string.Equals(entry.ConstructorId, constructorId, StringComparison.Ordinal)
                        || entry.From > spanFrom || spanFrom > entry.To)
                    {
                        continue;
                    }

                    var year = organization.From == spanFrom && organization.Founded is int founded
                        ? Math.Min(founded, spanFrom)
                        : spanFrom;
                    return (year, true);
                }
            }

            return (spanFrom, false);
        }

        private string? CountryOf(string constructorId, int spanFrom)
        {
            foreach (var organization in _data.Founders.Organizations)
            {
                foreach (var entry in organization.ConstructorEntries)
                {
                    if (string.Equals(entry.ConstructorId, constructorId, StringComparison.Ordinal)
                        && entry.From <= spanFrom && spanFrom <= entry.To)
                    {
                        return organization.Country;
                    }
                }
            }

            return null;
        }

        private string NameOf(string constructorId)
        {
            if (_constructorNames is not null
                && _constructorNames.TryGetValue(constructorId, out var name)
                && !string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            Gap(WorldInitGapCodes.OrganizationNameFromId, constructorId);
            return PrettifyId(constructorId);
        }

        // ---------------------------------------------------------------- drivers

        private readonly SortedSet<string> _unrated = new(Ordinal);

        private void AddKnownDrivers()
        {
            var candidates = new List<SeatCandidate>();
            foreach (var record in _provider.Drivers.OrderBy(driver => driver.DriverId, Ordinal))
            {
                var racingSeat = SeatOf(record);
                var racing = racingSeat is not null;
                var inPool = !racing
                    && record.PoolEntryYear is int entry && entry <= _start
                    && record.FirstSeason is int debut && debut > _start;
                if (!racing && !inPool)
                {
                    continue;
                }

                if (AddKnownPerson(record, racing) is not PersonId id)
                {
                    continue;
                }

                if (racingSeat is not null)
                {
                    if (HomeOf(record) is DriverSeat home)
                    {
                        candidates.Add(new SeatCandidate(id, record, home.ConstructorId, IsSubstitute(home), home.Starts));
                    }
                    else
                    {
                        Gap(WorldInitGapCodes.DriverConstructorAbsent, record.DriverId + "@" + racingSeat.ConstructorId);
                    }
                }
                else
                {
                    _pool.Add(id);
                }
            }

            SeatKnownDrivers(candidates);

            foreach (var id in _unrated)
            {
                Gap(WorldInitGapCodes.DriversWithoutRatings, id);
            }
        }

        /// <summary>Adds a real driver as a person without a contract; null (with a gap) when the driver has no usable birth or id.</summary>
        private PersonId? AddKnownPerson(RealDriverRecord record, bool racing)
        {
            if (!TryBirth(record.BirthDate, record.BornYear, record.DriverId, WorldInitGapCodes.DriverBirthEstimated, out var birth))
            {
                Gap(WorldInitGapCodes.DriverSkippedNoBirth, record.DriverId);
                return null;
            }

            if (_realIds.Contains(record.DriverId))
            {
                Gap(WorldInitGapCodes.IdCollision, record.DriverId);
                return null;
            }

            var nationality = NationalityOrUnknown(record.Nationality, record.DriverId);
            var band = racing ? WorldInitEstimates.RacingKnownQuality : WorldInitEstimates.PoolKnownQuality;
            var identity = new KnownPersonIdentity(record.DriverId, record.GivenName, record.FamilyName, birth, nationality);
            var truth = KnownTruth(identity, record.DriverId, band, _unrated);
            var spec = new PersonSpec(
                record.GivenName,
                record.FamilyName,
                ToGameDate(birth),
                nationality,
                true,
                record.DriverId,
                [PersonRole.Driver],
                truth);
            (_world, var id) = _world.AddPerson(spec);
            _realIds.Add(record.DriverId);
            return id;
        }

        private sealed record SeatCandidate(PersonId Id, RealDriverRecord Record, string TeamId, bool Substitute, int Starts)
        {
            public string DriverId => Record.DriverId;
        }

        /// <summary>
        /// The seat a driver is placed by: the team of the start season where the driver started the most races
        /// (a race stint beats a substitute one), then the earliest round, then the constructor id. Null when no
        /// stint of the season belongs to a team of the world.
        /// </summary>
        private DriverSeat? HomeOf(RealDriverRecord record)
        {
            DriverSeat? best = null;
            foreach (var seat in record.Seats)
            {
                if (seat.Season != _start || !_teamIds.ContainsKey(seat.ConstructorId) || !_currentTeams.ContainsKey(seat.ConstructorId))
                {
                    continue;
                }

                if (best is null || CompareHomes(seat, best) < 0)
                {
                    best = seat;
                }
            }

            return best;
        }

        private static int CompareHomes(DriverSeat left, DriverSeat right)
        {
            var substitute = IsSubstitute(left).CompareTo(IsSubstitute(right));
            if (substitute != 0)
            {
                return substitute;
            }

            var starts = right.Starts.CompareTo(left.Starts);
            if (starts != 0)
            {
                return starts;
            }

            var round = left.FirstRound.CompareTo(right.FirstRound);
            return round != 0 ? round : string.CompareOrdinal(left.ConstructorId, right.ConstructorId);
        }

        private static int CompareCandidates(SeatCandidate left, SeatCandidate right)
        {
            var substitute = left.Substitute.CompareTo(right.Substitute);
            if (substitute != 0)
            {
                return substitute;
            }

            var starts = right.Starts.CompareTo(left.Starts);
            return starts != 0 ? starts : string.CompareOrdinal(left.DriverId, right.DriverId);
        }

        /// <summary>
        /// Every team gets exactly <see cref="WorldInitEstimates.RaceSeatsPerTeam"/> race seats (#235, PP-064): the drivers who started
        /// the most races for it that season. The substitutes of the team are its reserves, at most
        /// <see cref="WorldInitEstimates.MaxReservesPerTeam"/>. A team short of drivers fills each seat by the first rule that has a
        /// driver: (1) a leftover driver with a stint at that team in the start season, (2) a driver with a stint at that team in the
        /// previous season, (3) a generated driver. Leftover real drivers then become reserves at the team where they started the most
        /// races if it has room; the rest stay without a contract: free agents.
        /// </summary>
        private void SeatKnownDrivers(List<SeatCandidate> candidates)
        {
            var free = new List<SeatCandidate>();
            var reserves = new Dictionary<string, int>(Ordinal);
            var shortTeams = new List<(string Team, int Missing)>();
            foreach (var team in _currentTeams.Keys.OrderBy(id => id, Ordinal))
            {
                var mine = candidates.Where(candidate => candidate.TeamId == team).ToList();
                mine.Sort(CompareCandidates);
                var race = mine.Where(candidate => !candidate.Substitute).ToList();
                var subs = mine.Where(candidate => candidate.Substitute).ToList();
                foreach (var driver in race.Take(WorldInitEstimates.RaceSeatsPerTeam))
                {
                    AddDriverContract(driver.Id, _teamIds[team], WorldInitEstimates.DefaultSeatStatus);
                }

                foreach (var driver in subs.Take(WorldInitEstimates.MaxReservesPerTeam))
                {
                    AddDriverContract(driver.Id, _teamIds[team], SeatStatus.Reserve);
                }

                reserves[team] = Math.Min(subs.Count, WorldInitEstimates.MaxReservesPerTeam);
                free.AddRange(race.Skip(WorldInitEstimates.RaceSeatsPerTeam));
                free.AddRange(subs.Skip(WorldInitEstimates.MaxReservesPerTeam));
                if (race.Count < WorldInitEstimates.RaceSeatsPerTeam)
                {
                    shortTeams.Add((team, WorldInitEstimates.RaceSeatsPerTeam - race.Count));
                }
            }

            free.Sort(CompareCandidates);
            foreach (var (team, missing) in shortTeams)
            {
                for (var i = 0; i < missing; i++)
                {
                    FillSeat(team, i, free);
                }
            }

            foreach (var driver in free)
            {
                if (reserves[driver.TeamId] < WorldInitEstimates.MaxReservesPerTeam)
                {
                    reserves[driver.TeamId]++;
                    AddDriverContract(driver.Id, _teamIds[driver.TeamId], SeatStatus.Reserve);
                }
            }
        }

        private void FillSeat(string team, int index, List<SeatCandidate> free)
        {
            var id = _teamIds[team];

            // Rule 1: a leftover driver who had a stint at this team in the start season.
            var same = free
                .Select(candidate => (Candidate: candidate, Starts: StartsAt(candidate.Record, team, _start)))
                .Where(entry => entry.Starts is not null)
                .OrderByDescending(entry => entry.Starts)
                .ThenBy(entry => entry.Candidate.DriverId, Ordinal)
                .FirstOrDefault();
            if (same.Candidate is not null)
            {
                free.Remove(same.Candidate);
                AddDriverContract(same.Candidate.Id, id, WorldInitEstimates.DefaultSeatStatus);
                Gap(WorldInitGapCodes.SeatFilledFromSameSeasonStint, same.Candidate.DriverId + "@" + team);
                return;
            }

            // Rule 2: a driver who had a stint at this team the season before (leftover, or not yet in the world).
            var placed = free.Select(candidate => candidate.DriverId).ToHashSet(Ordinal);
            var previous = _provider.Drivers
                .Where(record => !_realIds.Contains(record.DriverId) || placed.Contains(record.DriverId))
                .Select(record => (Record: record, Starts: StartsAt(record, team, _start - 1)))
                .Where(entry => entry.Starts is not null)
                .OrderByDescending(entry => entry.Starts)
                .ThenBy(entry => entry.Record.DriverId, Ordinal)
                .ToList();
            foreach (var entry in previous)
            {
                var leftover = free.FirstOrDefault(candidate => candidate.DriverId == entry.Record.DriverId);
                PersonId? person = leftover?.Id;
                if (leftover is not null)
                {
                    free.Remove(leftover);
                }
                else
                {
                    person = AddKnownPerson(entry.Record, racing: true);
                }

                if (person is PersonId known)
                {
                    AddDriverContract(known, id, WorldInitEstimates.DefaultSeatStatus);
                    Gap(WorldInitGapCodes.SeatFilledFromPreviousSeasonStint, entry.Record.DriverId + "@" + team);
                    return;
                }
            }

            // Rule 3: a generated driver.
            var band = PickBand("init:grid:" + team + ":fill:" + Number(index), WorldInitEstimates.GridQualityWeights);
            AddDriverContract(AddGeneratedDriver(band, _currentTeams[team].Country), id, WorldInitEstimates.DefaultSeatStatus);
            Gap(WorldInitGapCodes.SeatFilledByGeneratedDriver, team);
        }

        /// <summary>Starts of a driver for a team in a season; null when the driver has no stint there.</summary>
        private static int? StartsAt(RealDriverRecord record, string team, int season)
        {
            int? total = null;
            foreach (var seat in record.Seats)
            {
                if (seat.Season == season && string.Equals(seat.ConstructorId, team, StringComparison.Ordinal))
                {
                    total = (total ?? 0) + seat.Starts;
                }
            }

            return total;
        }

        private const string ScheduleRolesSubstitute = Paddock.Data.Historical.ScheduleRoles.Substitute;

        private DriverSeat? SeatOf(RealDriverRecord record)
        {
            DriverSeat? best = null;
            foreach (var seat in record.Seats)
            {
                if (seat.Season != _start)
                {
                    continue;
                }

                if (best is null || CompareSeats(seat, best) < 0)
                {
                    best = seat;
                }
            }

            return best;
        }

        private static int CompareSeats(DriverSeat left, DriverSeat right)
        {
            var substitute = IsSubstitute(left).CompareTo(IsSubstitute(right));
            if (substitute != 0)
            {
                return substitute;
            }

            var round = left.FirstRound.CompareTo(right.FirstRound);
            return round != 0 ? round : string.CompareOrdinal(left.ConstructorId, right.ConstructorId);
        }

        private static bool IsSubstitute(DriverSeat seat) =>
            string.Equals(seat.Role, ScheduleRolesSubstitute, StringComparison.Ordinal);

        private PersonTruth KnownTruth(KnownPersonIdentity identity, string driverId, QualityBand band, SortedSet<string> unrated)
        {
            if (_config.PeopleSource != PeopleSource.RealNamesRandomSkills)
            {
                var rating = _provider.RatingFor(driverId, _start);
                if (rating is not null)
                {
                    return PersonTruth.FromDriver(rating.Current, rating.Potential);
                }

                unrated.Add(driverId);
                return Randomized(identity, WorldInitEstimates.UnratedFallbackStrength, band);
            }

            return Randomized(identity, WorldInitEstimates.RandomizedSkillStrength, band);
        }

        private PersonTruth Randomized(KnownPersonIdentity identity, int strength, QualityBand band)
        {
            var generator = new DriverGenerator(new StableIdAllocator(_world.Ids.NextPerson), _names, _blocklist);
            var driver = generator.RandomizeKnownPerson(_people, _start, identity, strength, band);
            return PersonTruth.FromDriver(driver.Attributes, driver.PotentialAttributes);
        }

        private void AddGeneratedDrivers()
        {
            var grid = 0;
            foreach (var team in _currentTeams.Values.OrderBy(plan => plan.Id, Ordinal))
            {
                for (var seat = 1; seat <= WorldInitEstimates.RaceSeatsPerTeam; seat++)
                {
                    var band = PickBand("init:grid:" + team.Id + ":" + Number(seat), WorldInitEstimates.GridQualityWeights);
                    var id = AddGeneratedDriver(band, team.Country);
                    AddDriverContract(id, _teamIds[team.Id], WorldInitEstimates.DefaultSeatStatus);
                    grid++;
                }
            }

            var poolSize = (grid * WorldInitEstimates.PoolPercentOfGrid + 99) / 100;
            for (var i = 1; i <= poolSize; i++)
            {
                var band = PickBand("init:pool:" + Number(i), WorldInitEstimates.PoolQualityWeights);
                _pool.Add(AddGeneratedDriver(band, null));
            }
        }

        private PersonId AddGeneratedDriver(QualityBand band, string? homeCountry)
        {
            var generator = new DriverGenerator(new StableIdAllocator(_world.Ids.NextPerson), _names, _blocklist);
            var request = GenerationRequest.ForNew(band, NationalityWeights(homeCountry), _start);
            var driver = generator.Generate(_people, _start, request);
            var spec = new PersonSpec(
                driver.GivenName,
                driver.FamilyName,
                ToGameDate(driver.BirthDate),
                driver.Nationality,
                false,
                null,
                [PersonRole.Driver],
                PersonTruth.FromDriver(driver.Attributes, driver.PotentialAttributes));
            (_world, var id) = _world.AddPerson(spec);
            if (!string.Equals(id.Value, driver.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Generated person id " + driver.Id + " does not match the world id " + id.Value + ".");
            }

            return id;
        }

        private void AddDriverContract(PersonId person, OrganizationId team, SeatStatus status)
        {
            var spec = new ContractSpec(
                person,
                team,
                ContractRole.Driver(status),
                GameDate.SeasonStart(_start),
                GameDate.SeasonEnd(_start + WorldInitEstimates.InitialContractSeasons - 1),
                WorldInitEstimates.PlaceholderSalary,
                true,
                null,
                null);
            (_world, _) = _world.AddContract(spec);
            _racingDrivers++;
        }

        private QualityBand PickBand(string tag, (QualityBand Band, int Weight)[] weights)
        {
            var total = weights.Sum(weight => weight.Weight);
            var roll = _people.DeriveChild(tag).NextInt(0, total);
            var cursor = 0;
            foreach (var (band, weight) in weights)
            {
                cursor += weight;
                if (roll < cursor)
                {
                    return band;
                }
            }

            throw new InvalidOperationException("Quality weights did not cover the roll.");
        }

        private static NationalityWeight[] NationalityWeights(string? homeCountry)
        {
            var home = string.IsNullOrWhiteSpace(homeCountry) ? null : homeCountry.Trim().ToUpperInvariant();
            var weights = new List<NationalityWeight>();
            foreach (var code in WorldInitEstimates.GeneratedNationalities)
            {
                weights.Add(new NationalityWeight(
                    code,
                    string.Equals(code, home, StringComparison.Ordinal) ? WorldInitEstimates.HomeCountryWeight : 1));
            }

            if (home is not null && !WorldInitEstimates.GeneratedNationalities.Contains(home, Ordinal))
            {
                weights.Add(new NationalityWeight(home, WorldInitEstimates.HomeCountryWeight));
            }

            return weights.ToArray();
        }

        // ---------------------------------------------------------------- staff

        private List<StaffSlot> StaffSlots()
        {
            var slots = new List<StaffSlot>();
            var seen = new HashSet<string>(Ordinal);
            foreach (var member in _data.Staff.OrderBy(staff => staff.Id, Ordinal))
            {
                foreach (var stint in member.Career)
                {
                    if (stint.From > stint.To || stint.From > _reference || _reference > stint.To)
                    {
                        continue;
                    }

                    OrganizationId organization = default;
                    var found = false;
                    if (stint.Series is null)
                    {
                        found = _currentTeams.ContainsKey(stint.Org) && _teamIds.TryGetValue(stint.Org, out organization);
                    }
                    else if (string.Equals(stint.Series, "engine_supplier", StringComparison.Ordinal))
                    {
                        found = _supplierIds.TryGetValue(Slug(stint.Org), out organization);
                    }
                    else
                    {
                        continue;
                    }

                    if (!TryMapRole(stint.Role, out var role))
                    {
                        Gap(WorldInitGapCodes.StaffUnmappedRole, member.Id + ":" + stint.Role + "@" + stint.Org);
                        continue;
                    }

                    if (role == StaffRole.EngineDesigner && stint.Series is null)
                    {
                        organization = EngineMaker(stint.Org);
                        found = organization.IsAssigned;
                    }

                    if (!found)
                    {
                        Gap(WorldInitGapCodes.StaffOrganizationAbsent, member.Id + "@" + stint.Org);
                        continue;
                    }

                    if (seen.Add(member.Id + "|" + organization.Value + "|" + role))
                    {
                        slots.Add(new StaffSlot(member.Id, member, organization, role));
                    }
                }
            }

            return slots;
        }

        private void AddKnownStaff(List<StaffSlot> slots)
        {
            foreach (var group in slots.GroupBy(slot => slot.PersonId, Ordinal).OrderBy(group => group.Key, Ordinal))
            {
                var member = group.First().Member;
                if (_realIds.Contains(member.Id))
                {
                    Gap(WorldInitGapCodes.IdCollision, member.Id);
                    continue;
                }

                var born = StaffBirth(member);
                var roles = group.Select(slot => slot.Role).Distinct().OrderBy(role => (int)role).ToArray();
                var (given, family) = SplitName(member.Name);
                var nationality = NationalityOrUnknown(member.Nationality, member.Id);
                var band = StaffRoster.BandOf(_world, group.First().Organization);
                var attributes = StaffRoster.AttributesForKnown(_people, member.Id, roles, band).ToList();

                var spec = new PersonSpec(
                    given,
                    family,
                    born,
                    nationality,
                    true,
                    member.Id,
                    roles.Select(PersonRole.Staff).ToArray(),
                    new PersonTruth(attributes, attributes));
                (_world, var id) = _world.AddPerson(spec);
                _realIds.Add(member.Id);
                _staffPeople++;
                Gap(WorldInitGapCodes.StaffWithoutRatings, member.Id);
                var known = attributes.Select(attribute => new KnownAttribute(attribute.Key, new AttributeBand(attribute.Value, attribute.Value))).ToArray();
                foreach (var slot in group)
                {
                    AddStaffContract(id, slot.Organization, slot.Role);
                    _world = _world.SetKnowledge(new PersonKnowledge(slot.Organization, id, known, null));
                }
            }
        }

        private void FillStaff()
        {
            var countries = _currentTeams.Values.ToDictionary(plan => plan.Id, plan => plan.Country, Ordinal);
            var filled = StaffRoster.Fill(
                _world,
                _people,
                _names,
                _blocklist,
                GameDate.SeasonStart(_start),
                id => countries.TryGetValue(id.Value, out var country) ? country : null);
            _world = filled.World;
            _staffPeople += filled.Hired.Count;
            foreach (var person in filled.Hired)
            {
                var contract = _world.Contracts.First(item => item.PersonId == person);
                _staffedOrganizations.Add(contract.OrganizationId.Value);
            }
        }

        /// <summary>
        /// The engine maker for a team stint. A works engine stays with the team (PP-019). A customer engine goes to
        /// <c>supplier:{slug}</c>. A maker whose team is not racing this season is still created, so the designer has an employer.
        /// </summary>
        private OrganizationId EngineMaker(string constructorId)
        {
            if (_currentTeams.ContainsKey(constructorId) && _teamIds.TryGetValue(constructorId, out var team))
            {
                var links = _supplies.Where(link => link.Constructor == team).ToArray();
                if (links.Any(link => string.Equals(link.SupplyType, "works", StringComparison.Ordinal)))
                {
                    return team;
                }

                if (links.Length > 0)
                {
                    return links.OrderBy(link => link.Supplier.Value, Ordinal).First().Supplier;
                }

                return team;
            }

            var row = _data.Engines.Entries
                .Where(entry => string.Equals(entry.ConstructorId, constructorId, StringComparison.Ordinal) && entry.Year <= _reference)
                .OrderByDescending(entry => entry.Year)
                .ThenBy(entry => string.Equals(entry.Type, "works", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(entry => entry.Supplier, Ordinal)
                .FirstOrDefault();
            if (row is null)
            {
                return default;
            }

            var slug = Slug(row.Supplier);
            if (slug.Length == 0 || string.Equals(slug, "unknown", StringComparison.Ordinal))
            {
                return default;
            }

            if (_supplierIds.TryGetValue(slug, out var existing))
            {
                return existing;
            }

            var founded = GameDate.SeasonStart(row.Year);
            var spec = new OrganizationSpec(
                OrganizationKind.EngineSupplier,
                true,
                SupplierIdText(slug),
                founded,
                null,
                WorldInitEstimates.PlaceholderBudget,
                [new OrganizationNameSpan(row.Supplier.Trim(), founded, null)]);
            (_world, var created) = _world.AddOrganization(spec);
            _supplierIds[slug] = created;
            Gap(WorldInitGapCodes.SupplierFoundedFromData, created.Value);
            return created;
        }

        private void AddStaffContract(PersonId person, OrganizationId organization, StaffRole role)
        {
            var spec = new ContractSpec(
                person,
                organization,
                ContractRole.Staff(role),
                GameDate.SeasonStart(_start),
                GameDate.SeasonEnd(_start + WorldInitEstimates.InitialContractSeasons - 1),
                WorldInitEstimates.PlaceholderSalary,
                false,
                null,
                null);
            (_world, _) = _world.AddContract(spec);
            _staffedOrganizations.Add(organization.Value);
        }

        private GameDate StaffBirth(StaffMember member)
        {
            if (member.Born is not null
                && DateOnly.TryParseExact(member.Born, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return ToGameDate(date);
            }

            Gap(WorldInitGapCodes.StaffBirthEstimated, member.Id);
            return GameDate.SeasonStart(_start - WorldInitEstimates.EstimatedStaffAge);
        }

        private void ReportTeamsWithoutStaff()
        {
            foreach (var plan in _currentTeams.Values)
            {
                if (!_staffedOrganizations.Contains(plan.Id))
                {
                    Gap(WorldInitGapCodes.TeamsWithoutStaff, plan.Id);
                }
            }
        }

        private void AddCars()
        {
            var fits = new List<DriverFitProfile>();
            foreach (var person in _world.Persons)
            {
                if (!person.Roles.Any(role => role.IsDriver))
                {
                    continue;
                }

                fits.Add(DriverFitFactory.Roll(person.Id, _people.DeriveChild("driver-fit:" + person.Id.Value)));
            }

            _world = InitialCarFactory.Install(
                _world,
                _masterSeed,
                _start,
                _config.PeopleSource == PeopleSource.FullyGenerated,
                _carStrength,
                fits);
        }

        // ---------------------------------------------------------------- shared helpers

        /// <summary>
        /// A constructor whose every authored engine row for the reference season cites the Indianapolis 500.
        /// Those rows are the season's entries we have; calibration already drops that round
        /// (<see cref="HistoricalEdges.IsIndianapolis500"/>). 1950–1960 only.
        /// </summary>
        private bool OnlyIndianapolis500(string constructorId)
        {
            if (_reference < HistoricalEdges.IndianapolisFirstSeason || _reference > HistoricalEdges.IndianapolisLastSeason)
            {
                return false;
            }

            var rows = _data.Engines.Entries
                .Where(entry => entry.Year == _reference && string.Equals(entry.ConstructorId, constructorId, StringComparison.Ordinal))
                .ToArray();
            return rows.Length > 0 && rows.All(CitesIndianapolis500);
        }

        private static bool CitesIndianapolis500(EngineEntry entry)
        {
            var source = entry.Source ?? string.Empty;
            var notes = entry.Notes ?? string.Empty;
            return source.Contains("Indianapolis_500", StringComparison.Ordinal)
                || notes.Contains("Indianapolis 500", StringComparison.Ordinal);
        }

        private bool TryBirth(DateOnly? date, int? year, string id, string estimatedCode, out DateOnly birth)
        {
            if (date is DateOnly exact)
            {
                birth = exact;
                return true;
            }

            if (year is int known && known >= GenerationEstimates.MinBirthYear && known <= GenerationEstimates.MaxBirthYear)
            {
                birth = new DateOnly(known, WorldInitEstimates.EstimatedBirthMonth, WorldInitEstimates.EstimatedBirthDay);
                Gap(estimatedCode, id);
                return true;
            }

            birth = default;
            return false;
        }

        private string NationalityOrUnknown(string? nationality, string id)
        {
            if (string.IsNullOrWhiteSpace(nationality))
            {
                Gap(WorldInitGapCodes.NationalityMissing, id);
                return WorldInitEstimates.UnknownNationality;
            }

            return nationality.Trim();
        }

        private void Gap(string code, string subject)
        {
            if (!_gaps.TryGetValue(code, out var subjects))
            {
                subjects = new SortedSet<string>(Ordinal);
                _gaps[code] = subjects;
            }

            subjects.Add(subject);
        }

        private WorldInitReport Report()
        {
            var organizations = _world.Organizations;
            var persons = _world.Persons;
            var counts = new WorldInitCounts(
                Teams: organizations.Count(organization => organization.Kind == OrganizationKind.Team && organization.Dissolved is null),
                DissolvedTeams: _dissolvedTeams,
                EngineSuppliers: organizations.Count(organization => organization.Kind == OrganizationKind.EngineSupplier),
                RacingDrivers: _racingDrivers,
                PoolDrivers: _pool.Count,
                StaffPeople: _staffPeople,
                Contracts: _world.Contracts.Count,
                RealPersons: persons.Count(person => person.IsReal),
                GeneratedPersons: persons.Count(person => !person.IsReal),
                LineageLinks: _links);
            var gaps = _gaps
                .Where(pair => pair.Value.Count > 0)
                .Select(pair => new WorldInitGap(pair.Key, pair.Value.ToArray()))
                .ToArray();
            return new WorldInitReport(_start, _reference, _config.PeopleSource, counts, gaps);
        }

        private static bool Covers(LineageSpan span, int season) =>
            span.FromYear <= season && (span.ToYear is null || season <= span.ToYear.Value);

        private static GameDate ToGameDate(DateOnly date) => new(date.Year, date.Month, date.Day);

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static bool TryMapRole(string role, out StaffRole mapped)
        {
            switch (role)
            {
                case "technical_director":
                    mapped = StaffRole.TechnicalDirector;
                    return true;
                case "team_principal":
                    mapped = StaffRole.TeamPrincipal;
                    return true;
                case "chief_designer":
                case "designer":
                    mapped = StaffRole.ChiefDesigner;
                    return true;
                case "head_of_aero":
                    mapped = StaffRole.HeadOfAerodynamics;
                    return true;
                case "race_engineer":
                    mapped = StaffRole.RaceEngineer;
                    return true;
                case "engine_designer":
                    mapped = StaffRole.EngineDesigner;
                    return true;
                default:
                    mapped = default;
                    return false;
            }
        }

        private static string SupplierIdText(string slug) => "supplier:" + slug;

        private static string Slug(string text)
        {
            var builder = new StringBuilder(text.Length);
            var pendingSeparator = false;
            foreach (var character in text.Trim())
            {
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSeparator && builder.Length > 0)
                    {
                        builder.Append('_');
                    }

                    pendingSeparator = false;
                    builder.Append(char.ToLowerInvariant(character));
                }
                else
                {
                    pendingSeparator = true;
                }
            }

            return builder.ToString();
        }

        private static string PrettifyId(string id)
        {
            var words = id.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length == 0)
            {
                return id;
            }

            return string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
        }

        private static readonly string[] NameSuffixes = ["Junior", "Júnior", "Jr", "Jr.", "Sr", "Sr.", "Filho"];

        private static (string Given, string Family) SplitName(string full)
        {
            var tokens = full.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
            {
                throw new ArgumentException("A staff member has no name.", nameof(full));
            }

            if (tokens.Length == 1)
            {
                return (tokens[0], tokens[0]);
            }

            var familyStart = tokens.Length - 1;
            if (NameSuffixes.Contains(tokens[familyStart], Ordinal) && familyStart > 1)
            {
                familyStart--;
            }

            while (familyStart > 1 && char.IsLower(tokens[familyStart - 1][0]))
            {
                familyStart--;
            }

            return (string.Join(' ', tokens[..familyStart]), string.Join(' ', tokens[familyStart..]));
        }
    }
}

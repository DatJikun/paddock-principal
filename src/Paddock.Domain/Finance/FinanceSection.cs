using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Finance;

/// <summary>Cash, obligations and the season-end forecast for one organization. A pure function of the section and the contracts.</summary>
public sealed record FinanceOutlook(
    long CashCents,
    long ObligationsCents,
    long CertainIncomeCents,
    long ForecastCashCents,
    string? WarningKey);

/// <summary>The day an overdraft began, and whether <c>OrganizationInsolvent</c> has already been emitted.</summary>
public readonly record struct InsolvencyWatch(GameDate BelowSince, bool Emitted);

/// <summary>
/// The <c>finance</c> section (T37). One ledger per organization, one global popularity index, and the insolvency watches.
/// Immutable. Every posting goes through <see cref="Post"/>; lines are append-only. Commands and the day handler are the
/// callers. No random stream is read.
/// <para>
/// Canonical text (schema 1), after the section header. Amounts are cents. A missing counterparty or warning is <c>-</c>.
/// <code>
/// next &lt;sequence&gt;
/// popularity &lt;milli&gt;
/// season &lt;year&gt;
/// races &lt;count&gt;
/// completed &lt;count&gt;
/// model &lt;len&gt;:&lt;id&gt;
/// typical &lt;cents&gt;
/// warning &lt;len&gt;:&lt;key or -&gt;
/// books &lt;count&gt;
/// book &lt;len&gt;:&lt;organization&gt; &lt;balance&gt; &lt;entries&gt; &lt;lastRaceEntries&gt;
/// entry &lt;sequence&gt; &lt;date&gt; &lt;len&gt;:&lt;category&gt; &lt;len&gt;:&lt;counterparty or -&gt; &lt;amount&gt; &lt;len&gt;:&lt;reason&gt;
/// watches &lt;count&gt;
/// watch &lt;len&gt;:&lt;organization&gt; &lt;date&gt; &lt;0|1&gt;
/// </code>
/// </para>
/// </summary>
public sealed class FinanceSection : IWorldSection
{
    public const string SectionName = "finance";

    private readonly SortedDictionary<string, OrganizationLedger> _books;
    private readonly SortedDictionary<string, InsolvencyWatch> _watches;

    private FinanceSection(
        long nextSequence,
        int popularityMilli,
        int season,
        int races,
        int completed,
        string revenueModel,
        long typicalCents,
        string? warningKey,
        SortedDictionary<string, OrganizationLedger> books,
        SortedDictionary<string, InsolvencyWatch> watches)
    {
        NextSequence = nextSequence;
        PopularityMilli = popularityMilli;
        Season = season;
        Races = races;
        Completed = completed;
        RevenueModel = revenueModel;
        TypicalCents = typicalCents;
        WarningKey = warningKey;
        _books = books;
        _watches = watches;
    }

    public static FinanceSection Empty { get; } = new(
        1,
        FinanceEstimates.BaselinePopularityMilli,
        0,
        0,
        0,
        string.Empty,
        0,
        null,
        new SortedDictionary<string, OrganizationLedger>(StringComparer.Ordinal),
        new SortedDictionary<string, InsolvencyWatch>(StringComparer.Ordinal));

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public long NextSequence { get; }

    public int PopularityMilli { get; }

    public int Season { get; }

    public int Races { get; }

    public int Completed { get; }

    public string RevenueModel { get; }

    public long TypicalCents { get; }

    public string? WarningKey { get; }

    public bool HasBooks => _books.Count > 0;

    public bool HasBook(OrganizationId organization) =>
        organization.IsAssigned && _books.ContainsKey(organization.Value);

    public long BalanceOf(OrganizationId organization) =>
        _books.TryGetValue(RequireOrg(organization), out var book) ? book.BalanceCents : 0;

    public IReadOnlyList<LedgerEntry> EntriesOf(OrganizationId organization) =>
        _books.TryGetValue(RequireOrg(organization), out var book) ? book.Entries : [];

    public bool IsInsolvent(OrganizationId organization) =>
        _watches.TryGetValue(RequireOrg(organization), out var watch) && watch.Emitted;

    public GameDate? OverdraftSince(OrganizationId organization) =>
        _watches.TryGetValue(RequireOrg(organization), out var watch) ? watch.BelowSince : null;

    public int LastRaceEntries(OrganizationId organization) =>
        _books.TryGetValue(RequireOrg(organization), out var book) ? book.LastRaceEntries : 0;

    /// <summary>Rebuilds a section from stored rows. Every sequence from 1 to <paramref name="nextSequence"/> − 1 appears once.</summary>
    public static FinanceSection RestoreRows(
        long nextSequence,
        int popularityMilli,
        int season,
        int races,
        int completed,
        string revenueModel,
        long typicalCents,
        string? warningKey,
        IReadOnlyList<(string OrganizationId, LedgerEntry Entry)> rows,
        IReadOnlyDictionary<string, int> lastRaceEntries,
        IReadOnlyList<(string OrganizationId, InsolvencyWatch Watch)> watches)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextSequence, 1);
        ArgumentNullException.ThrowIfNull(revenueModel);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(lastRaceEntries);
        ArgumentNullException.ThrowIfNull(watches);
        if (string.IsNullOrWhiteSpace(warningKey))
        {
            warningKey = null;
        }

        if (popularityMilli < FinanceEstimates.MinPopularityMilli || popularityMilli > FinanceEstimates.MaxPopularityMilli)
        {
            throw new ArgumentOutOfRangeException(nameof(popularityMilli), popularityMilli, "Popularity is outside the estimate bounds.");
        }

        var seen = new HashSet<long>();
        var groups = new SortedDictionary<string, List<LedgerEntry>>(StringComparer.Ordinal);
        foreach (var (organization, entry) in rows.OrderBy(row => row.Entry.Sequence))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(organization);
            ArgumentNullException.ThrowIfNull(entry);
            if (entry.Sequence >= nextSequence || !seen.Add(entry.Sequence))
            {
                throw new InvalidOperationException("Ledger sequence " + entry.Sequence.ToString(CultureInfo.InvariantCulture) + " is duplicated or past the counter.");
            }

            if (!groups.TryGetValue(organization, out var list))
            {
                list = [];
                groups.Add(organization, list);
            }

            list.Add(entry);
        }

        if (seen.Count != nextSequence - 1)
        {
            throw new InvalidOperationException("The ledger counter does not match the number of entries.");
        }

        foreach (var key in lastRaceEntries.Keys)
        {
            groups.TryAdd(key, []);
        }

        var books = new SortedDictionary<string, OrganizationLedger>(StringComparer.Ordinal);
        foreach (var (organization, list) in groups)
        {
            var ledger = OrganizationLedger.Empty;
            foreach (var entry in list)
            {
                ledger = ledger.Append(entry);
            }

            var count = lastRaceEntries.TryGetValue(organization, out var known) ? known : 0;
            books.Add(organization, ledger.WithLastRaceEntries(count));
        }

        var watchMap = new SortedDictionary<string, InsolvencyWatch>(StringComparer.Ordinal);
        foreach (var (organization, watch) in watches)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(organization);
            if (!books.ContainsKey(organization))
            {
                throw new InvalidOperationException("Insolvency is recorded for '" + organization + "', which has no ledger.");
            }

            if (!watchMap.TryAdd(organization, watch))
            {
                throw new InvalidOperationException("Insolvency for '" + organization + "' is recorded twice.");
            }
        }

        return new FinanceSection(
            nextSequence,
            popularityMilli,
            season,
            races,
            completed,
            revenueModel,
            typicalCents,
            warningKey,
            books,
            watchMap);
    }

    public FinanceSection Open(OrganizationId organization, GameDate on, long openingDollars, EraFinanceFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var key = RequireOrg(organization);
        if (_books.ContainsKey(key))
        {
            throw new InvalidOperationException("Organization '" + key + "' already has books.");
        }

        if (openingDollars < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(openingDollars), openingDollars, "Opening capital cannot be negative.");
        }

        var section = Season == 0
            ? WithSeason(on.Year, facts.RevenueModel, Money.FromDollars(facts.TypicalDollars).Cents, 0, 0, WarningKey)
            : this;
        section = section.WithBook(key, OrganizationLedger.Empty);
        if (openingDollars == 0)
        {
            return section;
        }

        return section.Post(organization, on, LedgerCategories.OwnerFunds, null, Money.FromDollars(openingDollars).Cents, FinanceReason.OpeningCapital);
    }

    /// <summary>Appends one line. The organization must already have books. The previous section is left unchanged.</summary>
    public FinanceSection Post(
        OrganizationId organization,
        GameDate date,
        string category,
        string? counterparty,
        long amountCents,
        string reasonKey)
    {
        var key = RequireOrg(organization);
        if (!_books.TryGetValue(key, out var book))
        {
            throw new InvalidOperationException("Organization '" + key + "' has no books to post to.");
        }

        if (NextSequence == long.MaxValue)
        {
            throw new InvalidOperationException("The ledger sequence is exhausted.");
        }

        var entry = new LedgerEntry(NextSequence, date, category, counterparty, amountCents, reasonKey);
        return new FinanceSection(
            NextSequence + 1,
            PopularityMilli,
            Season,
            Races,
            Completed,
            RevenueModel,
            TypicalCents,
            WarningKey,
            CopyBooks(key, book.Append(entry)),
            _watches);
    }

    public (FinanceSection Section, RevenueReport Report) ApplyRace(RaceResultsPublished race, long typicalCents, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(race);
        var report = RevenueModels.Quote(race, typicalCents, PopularityMilli);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in race.Entries)
        {
            counts.TryGetValue(entry.Organization.Value, out var count);
            counts[entry.Organization.Value] = count + 1;
        }

        var section = WithSeason(race.Season, race.RevenueModel, typicalCents, race.RacesInSeason, race.Round, report.WarningKey);
        foreach (var draft in report.Postings)
        {
            section = section.Post(draft.Organization, on, draft.Category, draft.Counterparty, draft.AmountCents, draft.ReasonKey);
        }

        return (section.WithEntryCounts(counts), report);
    }

    public FinanceSection ApplySeason(SeasonEnded season)
    {
        ArgumentNullException.ThrowIfNull(season);
        var next = PopularityModel.Next(PopularityMilli, season);
        return new FinanceSection(
            NextSequence,
            next,
            season.Season,
            Races,
            Completed,
            RevenueModel,
            TypicalCents,
            WarningKey,
            _books,
            _watches);
    }

    public FinanceSection AccrueSalaries(WorldState world, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (today.Day != FinanceEstimates.SalaryDayOfMonth)
        {
            return this;
        }

        var section = this;
        foreach (var contract in world.Contracts.OrderBy(contract => contract.Id.Value, StringComparer.Ordinal))
        {
            if (!contract.IsActiveOn(today) || !section._books.ContainsKey(contract.OrganizationId.Value))
            {
                continue;
            }

            var cents = MonthSalaryCents(contract.Salary, today.Month);
            if (cents == 0)
            {
                continue;
            }

            section = section.Post(
                contract.OrganizationId,
                today,
                LedgerCategories.Salary,
                contract.PersonId.Value,
                -cents,
                FinanceReason.Salary);
        }

        return section;
    }

    public FinanceSection PostDrafts(IEnumerable<LedgerDraft> drafts, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var section = this;
        foreach (var draft in drafts)
        {
            section = section.Post(draft.Organization, today, draft.Category, draft.Counterparty, draft.AmountCents, draft.ReasonKey);
        }

        return section;
    }

    /// <summary>
    /// PP-050: cash may be negative. <c>OrganizationInsolvent</c> is raised on the anniversary of the day the balance
    /// first went below <see cref="FinanceEstimates.InsolvencyThresholdCents"/>, which is one championship season later,
    /// and not on any earlier day. Recovering to the threshold clears the watch. The event is emitted once.
    /// </summary>
    public (FinanceSection Section, IReadOnlyList<OrganizationId> NewlyInsolvent) ReviewInsolvency(GameDate today)
    {
        var watches = new SortedDictionary<string, InsolvencyWatch>(_watches, StringComparer.Ordinal);
        var newly = new List<OrganizationId>();
        var dirty = false;
        foreach (var organization in _books.Keys)
        {
            var balance = _books[organization].BalanceCents;
            var has = watches.TryGetValue(organization, out var watch);
            if (balance >= FinanceEstimates.InsolvencyThresholdCents)
            {
                if (has)
                {
                    watches.Remove(organization);
                    dirty = true;
                }

                continue;
            }

            if (!has)
            {
                watches.Add(organization, new InsolvencyWatch(today, false));
                dirty = true;
                continue;
            }

            if (watch.Emitted || today < PlusOneSeason(watch.BelowSince))
            {
                continue;
            }

            watches[organization] = new InsolvencyWatch(watch.BelowSince, true);
            newly.Add(ParseOrganization(organization));
            dirty = true;
        }

        if (!dirty)
        {
            return (this, []);
        }

        return (new FinanceSection(
            NextSequence,
            PopularityMilli,
            Season,
            Races,
            Completed,
            RevenueModel,
            TypicalCents,
            WarningKey,
            _books,
            watches), newly);
    }

    /// <summary>STATE and FORECAST for one organization. Does not change the section and does not draw a random number.</summary>
    public FinanceOutlook Outlook(OrganizationId organization, WorldState world, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        var key = RequireOrg(organization);
        var cash = BalanceOf(organization);
        long obligations = 0;
        if (_books.ContainsKey(key))
        {
            var contracts = new List<Contract>();
            foreach (var contract in world.Contracts)
            {
                if (contract.OrganizationId.Value == key)
                {
                    contracts.Add(contract);
                }
            }

            foreach (var payDay in SalaryDays(today))
            {
                foreach (var contract in contracts)
                {
                    if (!contract.IsActiveOn(payDay))
                    {
                        continue;
                    }

                    obligations += MonthSalaryCents(contract.Salary, payDay.Month);
                }
            }
        }

        var remaining = Math.Max(0, Races - Completed);
        var entries = LastRaceEntries(organization);
        var popularity = PopularityMilli / 1000.0;
        var promoter = string.Equals(RevenueModel, FinanceEstimates.PromoterModel, StringComparison.Ordinal);
        long certain = 0;
        long expectedPrize = 0;
        long expectedRunning = 0;
        if (remaining > 0 && entries > 0 && TypicalCents > 0 && RevenueModel.Length > 0)
        {
            var startEach = Money.RoundCents(TypicalCents * FinanceEstimates.StartMoneyShare * popularity / Math.Max(1, Races));
            var prizeShare = promoter ? FinanceEstimates.PrizeMoneyShare : FinanceEstimates.FallbackPoolShare;
            var prizePool = Money.RoundCents(TypicalCents * prizeShare * popularity / Math.Max(1, Races));
            var weight = RevenueModels.PrizeWeight(FinanceEstimates.ForecastFinishPosition, true);
            var prizeEach = RevenueModels.WeightSum == 0 ? 0 : prizePool * weight / RevenueModels.WeightSum;
            var running = Money.RoundCents(TypicalCents * FinanceEstimates.RaceRunningShare * popularity / Math.Max(1, Races));
            if (promoter)
            {
                certain = startEach * entries * remaining;
            }

            expectedPrize = prizeEach * entries * remaining;
            expectedRunning = running * remaining;
        }

        var forecast = cash + certain + expectedPrize - obligations - expectedRunning;
        return new FinanceOutlook(cash, obligations, certain, forecast, WarningKey);
    }

    public static long MonthSalaryCents(long annualDollars, int month)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(annualDollars);
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be 1 to 12.");
        }

        var annual = checked(annualDollars * Money.CentsPerDollar);
        var each = annual / 12;
        var remainder = annual % 12;
        return month == 12 ? each + remainder : each;
    }

    public static GameDate PlusOneSeason(GameDate date)
    {
        var day = date.Month == 2 && date.Day == 29 ? 28 : date.Day;
        return new GameDate(date.Year + 1, date.Month, day);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next", NextSequence);
        writer.Number("popularity", PopularityMilli);
        writer.Number("season", Season);
        writer.Number("races", Races);
        writer.Number("completed", Completed);
        writer.TextLine("model", RevenueModel);
        writer.Number("typical", TypicalCents);
        writer.TextLine("warning", WarningKey ?? "-");
        writer.Count("books", _books.Count);
        foreach (var (organization, book) in _books)
        {
            writer.Begin("book");
            writer.Field(organization);
            writer.Space();
            writer.Raw(book.BalanceCents.ToString(CultureInfo.InvariantCulture));
            writer.Space();
            writer.Raw(book.Entries.Count.ToString(CultureInfo.InvariantCulture));
            writer.Space();
            writer.Raw(book.LastRaceEntries.ToString(CultureInfo.InvariantCulture));
            writer.End();
            foreach (var entry in book.Entries)
            {
                writer.Begin("entry");
                writer.Raw(entry.Sequence.ToString(CultureInfo.InvariantCulture));
                writer.Space();
                writer.Raw(entry.Date.ToString());
                writer.Space();
                writer.Field(entry.Category);
                writer.Space();
                writer.Field(entry.Counterparty ?? "-");
                writer.Space();
                writer.Raw(entry.AmountCents.ToString(CultureInfo.InvariantCulture));
                writer.Space();
                writer.Field(entry.ReasonKey);
                writer.End();
            }
        }

        writer.Count("watches", _watches.Count);
        foreach (var (organization, watch) in _watches)
        {
            writer.Begin("watch");
            writer.Field(organization);
            writer.Space();
            writer.Raw(watch.BelowSince.ToString());
            writer.Space();
            writer.Raw(watch.Emitted ? "1" : "0");
            writer.End();
        }
    }

    public IReadOnlyList<(string OrganizationId, InsolvencyWatch Watch)> Watches()
    {
        var list = new List<(string, InsolvencyWatch)>(_watches.Count);
        foreach (var (organization, watch) in _watches)
        {
            list.Add((organization, watch));
        }

        return list;
    }

    public IReadOnlyList<(string OrganizationId, LedgerEntry Entry)> Rows()
    {
        var list = new List<(string, LedgerEntry)>();
        foreach (var (organization, book) in _books)
        {
            foreach (var entry in book.Entries)
            {
                list.Add((organization, entry));
            }
        }

        list.Sort(static (left, right) => left.Item2.Sequence.CompareTo(right.Item2.Sequence));
        return list;
    }

    public IReadOnlyDictionary<string, int> EntryCounts()
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (organization, book) in _books)
        {
            counts.Add(organization, book.LastRaceEntries);
        }

        return counts;
    }

    private FinanceSection WithSeason(int season, string model, long typicalCents, int races, int completed, string? warning)
    {
        var done = Math.Max(Completed, completed);
        if (races > 0 && done > races)
        {
            done = races;
        }

        return new FinanceSection(
            NextSequence,
            PopularityMilli,
            season,
            races == 0 ? Races : races,
            done,
            model,
            typicalCents,
            warning,
            _books,
            _watches);
    }

    private FinanceSection WithBook(string organization, OrganizationLedger book) =>
        new(
            NextSequence,
            PopularityMilli,
            Season,
            Races,
            Completed,
            RevenueModel,
            TypicalCents,
            WarningKey,
            CopyBooks(organization, book),
            _watches);

    private FinanceSection WithEntryCounts(Dictionary<string, int> counts)
    {
        var books = new SortedDictionary<string, OrganizationLedger>(_books, StringComparer.Ordinal);
        foreach (var (organization, count) in counts)
        {
            if (!books.TryGetValue(organization, out var book))
            {
                continue;
            }

            books[organization] = book.WithLastRaceEntries(count);
        }

        return new FinanceSection(NextSequence, PopularityMilli, Season, Races, Completed, RevenueModel, TypicalCents, WarningKey, books, _watches);
    }

    private SortedDictionary<string, OrganizationLedger> CopyBooks(string organization, OrganizationLedger book)
    {
        var copy = new SortedDictionary<string, OrganizationLedger>(_books, StringComparer.Ordinal)
        {
            [organization] = book,
        };
        return copy;
    }

    private static string RequireOrg(OrganizationId organization)
    {
        if (!organization.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(organization));
        }

        return organization.Value;
    }

    private static OrganizationId ParseOrganization(string value)
    {
        const string prefix = "org:";
        if (value.StartsWith(prefix, StringComparison.Ordinal))
        {
            var tail = value.AsSpan(prefix.Length);
            if (long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) && sequence >= 1
                && (tail.Length == 1 || tail[0] != '0'))
            {
                var generated = OrganizationId.Generated(sequence);
                if (generated.Value == value)
                {
                    return generated;
                }
            }
        }

        return OrganizationId.Real(value);
    }

    private static IEnumerable<GameDate> SalaryDays(GameDate today)
    {
        var month = today.Month;
        if (today.Day > FinanceEstimates.SalaryDayOfMonth)
        {
            month++;
        }

        for (; month <= 12; month++)
        {
            yield return new GameDate(today.Year, month, FinanceEstimates.SalaryDayOfMonth);
        }
    }
}

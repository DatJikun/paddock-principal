using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Finance;

/// <summary>Ledger category keys. The set is closed; a new cost is a new key here, not a free string.</summary>
public static class LedgerCategories
{
    public const string StartMoney = "start_money";

    public const string PrizeMoney = "prize_money";

    public const string Sponsor = "sponsor";

    public const string OwnerFunds = "owner_funds";

    public const string Salary = "salary";

    public const string RaceRunning = "race_running";

    public const string CarBuild = "car_build";

    public const string Development = "development";

    public const string Supply = "supply";

    public const string Infrastructure = "infrastructure";

    public const string Logistics = "logistics";

    public const string Other = "other";

    private static readonly HashSet<string> All = new(StringComparer.Ordinal)
    {
        StartMoney, PrizeMoney, Sponsor, OwnerFunds, Salary, RaceRunning, CarBuild, Development, Supply, Infrastructure, Logistics, Other,
    };

    public static bool IsKnown(string category) => category is not null && All.Contains(category);

    public static string Require(string category, string paramName)
    {
        if (!IsKnown(category))
        {
            throw new ArgumentException("Unknown ledger category '" + category + "'.", paramName);
        }

        return category;
    }
}

/// <summary>
/// One posted line. Immutable: there is no way to change a field, and <see cref="OrganizationLedger"/> never replaces a line.
/// <paramref name="AmountCents"/> is signed (income positive, cost negative). <paramref name="Counterparty"/> is an id or null.
/// </summary>
public sealed class LedgerEntry
{
    public LedgerEntry(long sequence, GameDate date, string category, string? counterparty, long amountCents, string reasonKey)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        Category = LedgerCategories.Require(category, nameof(category));
        if (counterparty is not null && string.IsNullOrWhiteSpace(counterparty))
        {
            throw new ArgumentException("A counterparty id cannot be blank.", nameof(counterparty));
        }

        if (amountCents == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountCents), amountCents, "A ledger entry cannot be zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reasonKey);
        Sequence = sequence;
        Date = date;
        Counterparty = counterparty;
        AmountCents = amountCents;
        ReasonKey = reasonKey;
    }

    public long Sequence { get; }

    public GameDate Date { get; }

    public string Category { get; }

    public string? Counterparty { get; }

    public long AmountCents { get; }

    public string ReasonKey { get; }
}

/// <summary>A line a system wants posted. <see cref="FinanceSection.Post"/> assigns the sequence.</summary>
public sealed record LedgerDraft(
    OrganizationId Organization,
    string Category,
    string? Counterparty,
    long AmountCents,
    string ReasonKey);

/// <summary>
/// One organization's lines, in sequence order. Balance is the sum of the amounts. Posting returns a new ledger;
/// the previous one, and every entry in it, stays as it was.
/// </summary>
public sealed class OrganizationLedger
{
    private readonly LedgerEntry[] _entries;

    private OrganizationLedger(LedgerEntry[] entries, int lastRaceEntries)
    {
        _entries = entries;
        LastRaceEntries = lastRaceEntries;
    }

    public static OrganizationLedger Empty { get; } = new([], 0);

    public int LastRaceEntries { get; }

    public IReadOnlyList<LedgerEntry> Entries => _entries.ToArray();

    public long BalanceCents
    {
        get
        {
            long sum = 0;
            foreach (var entry in _entries)
            {
                sum = checked(sum + entry.AmountCents);
            }

            return sum;
        }
    }

    public OrganizationLedger Append(LedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_entries.Length > 0 && entry.Sequence <= _entries[^1].Sequence)
        {
            throw new InvalidOperationException("Ledger entries must be appended in sequence order.");
        }

        var next = new LedgerEntry[_entries.Length + 1];
        Array.Copy(_entries, next, _entries.Length);
        next[^1] = entry;
        return new OrganizationLedger(next, LastRaceEntries);
    }

    public OrganizationLedger WithLastRaceEntries(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new OrganizationLedger(_entries.ToArray(), count);
    }
}

/// <summary>
/// Something that will owe or earn money once its own system exists (car build T41, development T42, supply T43).
/// The finance day handler posts whatever is due. A source must be a pure read of its own state (INV-005).
/// </summary>
public interface ILedgerSource
{
    IReadOnlyList<LedgerDraft> Due(GameDate today);
}

/// <summary>No extra postings. The default until T41–T43 register.</summary>
public sealed class NoLedgerSources : ILedgerSource
{
    public static NoLedgerSources Instance { get; } = new();

    public IReadOnlyList<LedgerDraft> Due(GameDate today) => [];
}

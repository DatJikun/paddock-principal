using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Objectives;

/// <summary>
/// How an objective reads the world. Systems that own a number or a lineup register it here;
/// the objective code never reaches into them. A fact is about one organization (the objective's owner).
/// Both methods return null when the system that owns the fact has nothing to say yet.
/// Implementations must be pure queries: no state change, no RNG (INV-005).
/// </summary>
public interface IObjectiveFacts
{
    /// <summary>A numeric fact such as the championship position, or null when unknown.</summary>
    decimal? Number(OrganizationId owner, string factKey);

    /// <summary>A yes/no fact that takes an argument such as a nationality, or null when unknown.</summary>
    bool? Flag(OrganizationId owner, string factKey, string argument);
}

/// <summary>The fact keys the built-in predicates read. Other systems register under these keys.</summary>
public static class ObjectiveFactKeys
{
    /// <summary>Current championship position of the organization, 1 is the leader.</summary>
    public const string ChampionshipPosition = "championship.position";

    /// <summary>Podium finishes so far this season.</summary>
    public const string SeasonPodiums = "season.podiums";

    /// <summary>Championship points so far this season.</summary>
    public const string SeasonPoints = "season.points";

    /// <summary>Cash on hand, in the world's money unit.</summary>
    public const string Cash = "finance.cash";

    /// <summary>Flag with a nationality argument: a driver of that nationality is in the lineup.</summary>
    public const string LineupDriverNationality = "lineup.driverNationality";

    /// <summary>Flag with a country argument: a person from that country is in the lineup.</summary>
    public const string LineupPersonCountry = "lineup.personCountry";
}

/// <summary>
/// Facts supplied by the systems that own them, one registration per key. Registering a key twice is a mistake and throws.
/// A key nobody registered reads as unknown (null).
/// </summary>
public sealed class ObjectiveFactRegistry : IObjectiveFacts
{
    private readonly Dictionary<string, Func<OrganizationId, decimal?>> _numbers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<OrganizationId, string, bool?>> _flags = new(StringComparer.Ordinal);

    public void RegisterNumber(string factKey, Func<OrganizationId, decimal?> source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factKey);
        ArgumentNullException.ThrowIfNull(source);
        if (_flags.ContainsKey(factKey) || !_numbers.TryAdd(factKey, source))
        {
            throw new InvalidOperationException($"Fact '{factKey}' is already registered.");
        }
    }

    public void RegisterFlag(string factKey, Func<OrganizationId, string, bool?> source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(factKey);
        ArgumentNullException.ThrowIfNull(source);
        if (_numbers.ContainsKey(factKey) || !_flags.TryAdd(factKey, source))
        {
            throw new InvalidOperationException($"Fact '{factKey}' is already registered.");
        }
    }

    public decimal? Number(OrganizationId owner, string factKey) =>
        _numbers.TryGetValue(factKey, out var source) ? source(owner) : null;

    public bool? Flag(OrganizationId owner, string factKey, string argument) =>
        _flags.TryGetValue(factKey, out var source) ? source(owner, argument) : null;
}

/// <summary>
/// What an objective asks for. The set is closed: a new kind of question is a new predicate here, while a new
/// source of numbers for an existing question is a fact registered in <see cref="ObjectiveFactRegistry"/>.
/// </summary>
public abstract record ObjectivePredicate
{
    internal ObjectivePredicate()
    {
    }

    /// <summary>Stable name of the kind, for saves, hashes and text lookup (lowerCamelCase).</summary>
    public abstract string Name { get; }

    /// <summary>The fact the predicate reads.</summary>
    public abstract string FactKey { get; }

    /// <summary>The single parameter as invariant text (a number, a nationality, a country).</summary>
    public abstract string Parameter { get; }

    /// <summary>True, false, or null when the fact is unknown.</summary>
    public abstract bool? Evaluate(OrganizationId owner, IObjectiveFacts facts);

    /// <summary>Rebuilds a predicate from <see cref="Name"/> and <see cref="Parameter"/>, as saved. Throws for an unknown name or a bad parameter.</summary>
    public static ObjectivePredicate FromParts(string name, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(parameter);
        return name switch
        {
            "championshipPositionAtMost" => new ChampionshipPositionAtMost(int.Parse(parameter, CultureInfo.InvariantCulture)),
            "podiumsAtLeast" => new PodiumsAtLeast(int.Parse(parameter, CultureInfo.InvariantCulture)),
            "pointsAtLeast" => new PointsAtLeast(decimal.Parse(parameter, CultureInfo.InvariantCulture)),
            "cashAtLeast" => new CashAtLeast(long.Parse(parameter, CultureInfo.InvariantCulture)),
            "driverNationalityInLineup" => new DriverNationalityInLineup(parameter),
            "personFromCountryInLineup" => new PersonFromCountryInLineup(parameter),
            _ => throw new ArgumentException("Unknown objective predicate '" + name + "'.", nameof(name)),
        };
    }
}

/// <summary>A predicate over a number, which can therefore be projected forward (<see cref="NumericPredicate.IsMetBy"/>).</summary>
public abstract record NumericPredicate : ObjectivePredicate
{
    internal NumericPredicate()
    {
    }

    public abstract decimal Target { get; }

    /// <summary>True when lower is better, as for a championship position.</summary>
    public abstract bool LowerIsBetter { get; }

    public abstract bool IsMetBy(decimal value);

    public override bool? Evaluate(OrganizationId owner, IObjectiveFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Number(owner, FactKey) is decimal value ? IsMetBy(value) : null;
    }

    public override string Parameter => Invariant(Target);

    /// <summary>Invariant text with no trailing zeros, so 10 and 10.0 hash alike.</summary>
    internal static string Invariant(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);
}

/// <summary>The championship position is <paramref name="Position"/> or better (a smaller number).</summary>
public sealed record ChampionshipPositionAtMost : NumericPredicate
{
    public ChampionshipPositionAtMost(int position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        Position = position;
    }

    public int Position { get; }

    public override string Name => "championshipPositionAtMost";

    public override string FactKey => ObjectiveFactKeys.ChampionshipPosition;

    public override decimal Target => Position;

    public override bool LowerIsBetter => true;

    public override bool IsMetBy(decimal value) => value <= Position;
}

public sealed record PodiumsAtLeast : NumericPredicate
{
    public PodiumsAtLeast(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        Count = count;
    }

    public int Count { get; }

    public override string Name => "podiumsAtLeast";

    public override string FactKey => ObjectiveFactKeys.SeasonPodiums;

    public override decimal Target => Count;

    public override bool LowerIsBetter => false;

    public override bool IsMetBy(decimal value) => value >= Count;
}

public sealed record PointsAtLeast : NumericPredicate
{
    public PointsAtLeast(decimal points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        Points = points;
    }

    public decimal Points { get; }

    public override string Name => "pointsAtLeast";

    public override string FactKey => ObjectiveFactKeys.SeasonPoints;

    public override decimal Target => Points;

    public override bool LowerIsBetter => false;

    public override bool IsMetBy(decimal value) => value >= Points;
}

public sealed record CashAtLeast : NumericPredicate
{
    public CashAtLeast(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Amount = amount;
    }

    public long Amount { get; }

    public override string Name => "cashAtLeast";

    public override string FactKey => ObjectiveFactKeys.Cash;

    public override decimal Target => Amount;

    public override bool LowerIsBetter => false;

    public override bool IsMetBy(decimal value) => value >= Amount;
}

/// <summary>A driver of this nationality is in the lineup. There is nothing to project, only a state to reach.</summary>
public sealed record DriverNationalityInLineup : ObjectivePredicate
{
    public DriverNationalityInLineup(string nationality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nationality);
        Nationality = nationality;
    }

    public string Nationality { get; }

    public override string Name => "driverNationalityInLineup";

    public override string FactKey => ObjectiveFactKeys.LineupDriverNationality;

    public override string Parameter => Nationality;

    public override bool? Evaluate(OrganizationId owner, IObjectiveFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Flag(owner, FactKey, Nationality);
    }
}

/// <summary>A person (driver or staff) from this country is in the lineup.</summary>
public sealed record PersonFromCountryInLineup : ObjectivePredicate
{
    public PersonFromCountryInLineup(string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        Country = country;
    }

    public string Country { get; }

    public override string Name => "personFromCountryInLineup";

    public override string FactKey => ObjectiveFactKeys.LineupPersonCountry;

    public override string Parameter => Country;

    public override bool? Evaluate(OrganizationId owner, IObjectiveFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return facts.Flag(owner, FactKey, Country);
    }
}

using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Localization;
using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Objectives;

/// <summary>Player-facing keys of objectives. The effect keys of a given objective belong to the system that grants it.</summary>
public static class ObjectiveKeys
{
    [TranslationKey]
    public const string StatusOpen = "objective.status.open";

    [TranslationKey]
    public const string StatusMet = "objective.status.met";

    [TranslationKey]
    public const string StatusFailed = "objective.status.failed";

    [TranslationKey]
    public const string ForecastUnknown = "objective.forecast.unknown";

    [TranslationKey]
    public const string ForecastOnTrack = "objective.forecast.onTrack";

    [TranslationKey]
    public const string ForecastOffTrack = "objective.forecast.offTrack";

    [TranslationKey]
    public const string ForecastNotProjectable = "objective.forecast.notProjectable";

    [TranslationKey]
    public const string PredicateChampionshipPositionAtMost = "objective.predicate.championshipPositionAtMost";

    [TranslationKey]
    public const string PredicatePodiumsAtLeast = "objective.predicate.podiumsAtLeast";

    [TranslationKey]
    public const string PredicatePointsAtLeast = "objective.predicate.pointsAtLeast";

    [TranslationKey]
    public const string PredicateCashAtLeast = "objective.predicate.cashAtLeast";

    [TranslationKey]
    public const string PredicateDriverNationalityInLineup = "objective.predicate.driverNationalityInLineup";

    [TranslationKey]
    public const string PredicatePersonFromCountryInLineup = "objective.predicate.personFromCountryInLineup";

    public static string StatusKey(ObjectiveStatus status) => status switch
    {
        ObjectiveStatus.Open => StatusOpen,
        ObjectiveStatus.Met => StatusMet,
        ObjectiveStatus.Failed => StatusFailed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
    };

    public static string ForecastKey(ForecastKind kind) => kind switch
    {
        ForecastKind.Unknown => ForecastUnknown,
        ForecastKind.OnTrack => ForecastOnTrack,
        ForecastKind.OffTrack => ForecastOffTrack,
        ForecastKind.NotProjectable => ForecastNotProjectable,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown forecast kind."),
    };

    /// <summary>The key and parameter name of the sentence that states what a predicate asks for.</summary>
    public static (string Key, string Parameter) PredicateText(ObjectivePredicate predicate) => predicate switch
    {
        ChampionshipPositionAtMost => (PredicateChampionshipPositionAtMost, "target"),
        PodiumsAtLeast => (PredicatePodiumsAtLeast, "target"),
        PointsAtLeast => (PredicatePointsAtLeast, "target"),
        CashAtLeast => (PredicateCashAtLeast, "target"),
        DriverNationalityInLineup => (PredicateDriverNationalityInLineup, "nationality"),
        PersonFromCountryInLineup => (PredicatePersonFromCountryInLineup, "country"),
        _ => throw new ArgumentOutOfRangeException(nameof(predicate), predicate, "Unknown predicate."),
    };
}

/// <summary>Which organization a manager runs. Objectives are owned by organizations; a manager reads those of their own.</summary>
public interface IManagerOrganizations
{
    OrganizationId? OrganizationOf(string managerId);
}

/// <summary>
/// STATE: where the objective stands today. <paramref name="Current"/> is the number for a numeric objective,
/// <paramref name="Holds"/> the yes/no for a lineup objective, null when the owning system cannot say.
/// <paramref name="DaysLeft"/> is 0 once the deadline has come.
/// </summary>
public sealed record ObjectiveStateView(ObjectiveStatus Status, decimal? Current, bool? Holds, decimal? Target, int DaysLeft);

/// <summary>WHY: who asks and the reason they gave.</summary>
public sealed record ObjectiveWhyView(string GrantorId, TranslationMessage Reason);

/// <summary>FORECAST: the straight-line projection of an open objective (ESTIMATE, see <see cref="ObjectiveForecast"/>).</summary>
public sealed record ObjectiveForecastView(ForecastKind Kind, decimal? Projected, TranslationMessage Message);

/// <summary>One objective as its owner reads it. The consequences are shown up front (DESIGN §3.2).</summary>
public sealed record ObjectiveItemView(
    string Id,
    string OwnerId,
    TranslationMessage Title,
    TranslationMessage Requirement,
    DateOnly Deadline,
    ObjectiveStateView State,
    ObjectiveWhyView Why,
    ObjectiveForecastView? Forecast,
    TranslationMessage OnMet,
    TranslationMessage OnFailed);

public sealed record ObjectiveView(AccessContext Viewer, IReadOnlyList<ObjectiveItemView> Items);

/// <summary>
/// The read side of objectives (INV-003, INV-005): STATE, WHY, and FORECAST per objective.
/// A manager sees the objectives owned by their own organization; the developer sees all. It changes nothing and draws no RNG.
/// </summary>
public sealed class ObjectiveQuery
{
    private readonly IObjectiveFacts _facts;
    private readonly IManagerOrganizations _organizations;

    public ObjectiveQuery(IObjectiveFacts facts, IManagerOrganizations organizations)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(organizations);
        _facts = facts;
        _organizations = organizations;
    }

    public ObjectiveView View(AccessContext access, ObjectivesSection section, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(section);
        IEnumerable<Objective> visible;
        if (access.Kind == AccessKind.Developer)
        {
            visible = section.Objectives;
        }
        else
        {
            var manager = access.Manager ?? throw new InvalidOperationException("Non-developer context without a manager.");
            visible = _organizations.OrganizationOf(manager.Value) is OrganizationId own
                ? section.OwnedBy(own)
                : [];
        }

        return new ObjectiveView(access, visible.Select(objective => Describe(objective, today)).ToArray());
    }

    private ObjectiveItemView Describe(Objective objective, GameDate today)
    {
        decimal? current = null;
        bool? holds = null;
        decimal? target = null;
        if (objective.Predicate is NumericPredicate numeric)
        {
            current = _facts.Number(objective.Owner, numeric.FactKey);
            target = numeric.Target;
        }
        else
        {
            holds = objective.Predicate.Evaluate(objective.Owner, _facts);
        }

        var open = objective.IsOpen;
        var state = new ObjectiveStateView(objective.Status, current, holds, target, open ? Math.Max(0, today.DaysUntil(objective.Deadline)) : 0);
        var (key, parameter) = ObjectiveKeys.PredicateText(objective.Predicate);
        ObjectiveForecastView? forecast = null;
        if (open)
        {
            var result = ObjectiveForecast.Project(objective, current, today);
            forecast = new ObjectiveForecastView(result.Kind, result.Projected, TranslationMessage.Of(ObjectiveKeys.ForecastKey(result.Kind)));
        }

        return new ObjectiveItemView(
            objective.Id,
            objective.Owner.Value,
            TranslationMessage.Of(objective.KindKey),
            TranslationMessage.Of(key, (parameter, objective.Predicate.Parameter)),
            new DateOnly(objective.Deadline.Year, objective.Deadline.Month, objective.Deadline.Day),
            state,
            new ObjectiveWhyView(objective.Grantor.Value, TranslationMessage.Of(objective.ReasonKey)),
            forecast,
            Effect(objective.EffectOnMet),
            Effect(objective.EffectOnFailed));
    }

    private static TranslationMessage Effect(ObjectiveEffect effect) =>
        TranslationMessage.Of(effect.Key, effect.Arguments.Select(pair => (pair.Key, pair.Value)).ToArray());
}

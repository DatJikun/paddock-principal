namespace Paddock.Simulation.Racing.Incidents;

/// <summary>
/// What kind of incident it was. <see cref="IncidentSampler"/> picks the kind by cumulative weights in this order,
/// so reordering or inserting a member changes every sampled incident.
/// </summary>
public enum IncidentKind
{
    /// <summary>A car spins without hitting anything.</summary>
    Spin,

    /// <summary>Light contact between two cars.</summary>
    Contact,

    /// <summary>A collision between two cars.</summary>
    Collision,

    /// <summary>A car crashes into a barrier.</summary>
    Barrier,

    /// <summary>A car runs over debris (a puncture or damage). Nobody is at fault.</summary>
    Debris,
}

/// <summary>How bad the outcome is for one car. The order is the order of severity.</summary>
public enum OutcomeSeverity
{
    None,

    /// <summary>Damage or lost time; the car goes on.</summary>
    Minor,

    /// <summary>The car is out of the race, the driver is unhurt.</summary>
    Retire,

    /// <summary>The car is out and the driver is injured; see <see cref="InjuryGrade"/>.</summary>
    Injury,

    /// <summary>The driver is killed. Only possible when fatalities are enabled (PP-006).</summary>
    Fatal,
}

/// <summary>How serious an injury is. Applying injuries to people is a domain task, not done here.</summary>
public enum InjuryGrade
{
    None,
    Light,
    Serious,
    CareerEnding,
}

/// <summary>
/// The outcome for one car: a severity and, only for <see cref="OutcomeSeverity.Injury"/>, a grade.
/// The only way to build one is through the factories, so a combination such as "minor with a serious injury" cannot be made.
/// </summary>
public sealed record IncidentOutcome
{
    private IncidentOutcome(OutcomeSeverity severity, InjuryGrade injury)
    {
        Severity = severity;
        Injury = injury;
    }

    public static IncidentOutcome None { get; } = new(OutcomeSeverity.None, InjuryGrade.None);

    public static IncidentOutcome Minor { get; } = new(OutcomeSeverity.Minor, InjuryGrade.None);

    public static IncidentOutcome Retire { get; } = new(OutcomeSeverity.Retire, InjuryGrade.None);

    public static IncidentOutcome Fatal { get; } = new(OutcomeSeverity.Fatal, InjuryGrade.None);

    public OutcomeSeverity Severity { get; }

    /// <summary>The injury grade; <see cref="InjuryGrade.None"/> unless <see cref="Severity"/> is <see cref="OutcomeSeverity.Injury"/>.</summary>
    public InjuryGrade Injury { get; }

    /// <summary>True when the car is out of the race (retired, injured or the driver killed).</summary>
    public bool RetiresCar => Severity >= OutcomeSeverity.Retire;

    public bool IsFatal => Severity == OutcomeSeverity.Fatal;

    public static IncidentOutcome Injured(InjuryGrade grade)
    {
        if (grade == InjuryGrade.None || !Enum.IsDefined(grade))
        {
            throw new ArgumentOutOfRangeException(nameof(grade), "An injury needs a grade other than None.");
        }

        return new IncidentOutcome(OutcomeSeverity.Injury, grade);
    }
}

/// <summary>Whether a car is the one the incident was sampled for or was drawn into it.</summary>
public enum IncidentRole
{
    /// <summary>The car the incident was sampled for (the only car in a spin, barrier or debris incident).</summary>
    Instigator,

    /// <summary>The other car of a two-car incident, chosen from the cars near the instigator.</summary>
    Other,
}

/// <summary>One car in an incident.</summary>
public sealed record IncidentParticipant(string CarId, IncidentRole Role, IncidentOutcome Outcome);

/// <summary>
/// One incident, as plain simulation truth (TECH §3). It is not tied to the T26 race event stream; an adapter maps it later:
/// an <c>Incident</c> event (severity from <see cref="WorstSeverity"/>), a <c>Retirement</c> with the accident reason for each
/// participant whose outcome <see cref="IncidentOutcome.RetiresCar"/>, and a <c>SafetyCar</c> event from <see cref="NeutralisationPlanner"/>.
/// A car can be drawn into an incident as the other car and also have an incident of its own on the same lap;
/// the adapter keeps the first retirement of a car and ignores the rest.
/// </summary>
/// <param name="Lap">The lap (1-based) on which the incident happens.</param>
/// <param name="Kind">The kind of incident.</param>
/// <param name="Instigator">The car the incident was sampled for.</param>
/// <param name="Other">The second car of a contact or collision; null for single-car incidents.</param>
public sealed record IncidentResult(int Lap, IncidentKind Kind, IncidentParticipant Instigator, IncidentParticipant? Other)
{
    /// <summary>The instigator and, when there is one, the other car.</summary>
    public IEnumerable<IncidentParticipant> Participants
    {
        get
        {
            yield return Instigator;
            if (Other is not null)
            {
                yield return Other;
            }
        }
    }

    /// <summary>The worst severity among the participants.</summary>
    public OutcomeSeverity WorstSeverity =>
        Other is not null && Other.Outcome.Severity > Instigator.Outcome.Severity
            ? Other.Outcome.Severity
            : Instigator.Outcome.Severity;

    /// <summary>The worst injury grade among the participants; None when nobody is injured.</summary>
    public InjuryGrade WorstInjury =>
        Other is not null && Other.Outcome.Injury > Instigator.Outcome.Injury
            ? Other.Outcome.Injury
            : Instigator.Outcome.Injury;
}

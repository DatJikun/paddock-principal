namespace Paddock.Simulation.Racing.Incidents;

/// <summary>How the race is slowed or stopped after an incident.</summary>
public enum NeutralisationKind
{
    /// <summary>The race goes on at full speed.</summary>
    None,

    /// <summary>Local yellow flags at the place of the incident; the race goes on. No lap count.</summary>
    LocalYellow,

    /// <summary>Virtual safety car: cars keep their gaps and slow down.</summary>
    VirtualSafetyCar,

    /// <summary>Physical safety car: the field bunches up behind it.</summary>
    SafetyCar,

    /// <summary>Red flag: the race is stopped.</summary>
    RedFlag,
}

/// <summary>A neutralisation of the race.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="StartLap">The lap on which it starts: the incident's lap.</param>
/// <param name="DurationLaps">
/// Laps under the neutralisation, cut off at the end of the race. For a red flag it is the length of the suspension
/// expressed in laps of racing. 0 for <see cref="NeutralisationKind.None"/> and <see cref="NeutralisationKind.LocalYellow"/>.
/// </param>
/// <param name="ResumesRace">For a red flag: whether the race is restarted (see <see cref="IncidentConstants.RedFlagResumeUntilFraction"/>). False for every other kind.</param>
public sealed record Neutralisation(NeutralisationKind Kind, int StartLap, int DurationLaps, bool ResumesRace)
{
    public static Neutralisation None(int lap) => new(NeutralisationKind.None, lap, 0, false);
}

/// <summary>
/// Decides the neutralisation that follows an incident, from the worst outcome and the era's rules. No randomness.
/// </summary>
/// <remarks>
/// Rules by era (<see cref="NeutralisationRules"/>, data fields in <see cref="EraSafetyProfile"/>):
/// no neutralisation in the 1950s; marshals and local yellow flags from the 1960s (ESTIMATE); a physical safety car from
/// 1993; a virtual safety car as well from 2015; a red flag with a restart where <see cref="EraSafetyProfile.RedFlagAvailable"/>.
/// By worst outcome: none gives nothing; a minor outcome gives local yellow flags where there are marshals; a retirement or a
/// light injury gives the virtual safety car (single car) or the safety car (two cars, debris), else local yellow;
/// a serious injury gives a red flag where available, else a longer safety car, else local yellow;
/// a career-ending injury or a death gives a red flag where available, else a long safety car, else local yellow.
/// A death looks the same as the career-ending injury it is mapped to when fatalities are disabled.
/// Durations are ESTIMATE (<see cref="IncidentConstants"/>).
/// </remarks>
public static class NeutralisationPlanner
{
    /// <summary>The neutralisation after the incident.</summary>
    /// <param name="incident">The incident.</param>
    /// <param name="era">The era's safety rules.</param>
    /// <param name="totalLaps">Laps in the race, at least the incident's lap.</param>
    public static Neutralisation Plan(IncidentResult incident, EraSafetyProfile era, int totalLaps)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(era);
        if (totalLaps < incident.Lap)
        {
            throw new ArgumentOutOfRangeException(nameof(totalLaps), "The race cannot be shorter than the lap of the incident.");
        }

        var lap = incident.Lap;
        if (era.Rules == NeutralisationRules.None)
        {
            return Neutralisation.None(lap);
        }

        var worst = incident.WorstSeverity;
        if (worst == OutcomeSeverity.None)
        {
            return Neutralisation.None(lap);
        }

        var yellow = new Neutralisation(NeutralisationKind.LocalYellow, lap, 0, false);
        if (worst == OutcomeSeverity.Minor)
        {
            return yellow;
        }

        var remaining = totalLaps - lap + 1;
        var multiCar = incident.Other is not null || incident.Kind == IncidentKind.Debris;
        var injury = worst == OutcomeSeverity.Fatal ? InjuryGrade.CareerEnding : incident.WorstInjury;

        if (worst == OutcomeSeverity.Fatal || injury is InjuryGrade.CareerEnding or InjuryGrade.Serious)
        {
            var severe = worst == OutcomeSeverity.Fatal || injury == InjuryGrade.CareerEnding;
            if (era.RedFlagAvailable)
            {
                var suspension = severe ? IncidentConstants.RedFlagSevereLaps : IncidentConstants.RedFlagSeriousLaps;
                var resumes = lap <= IncidentConstants.RedFlagResumeUntilFraction * totalLaps;
                return new Neutralisation(NeutralisationKind.RedFlag, lap, suspension, resumes);
            }

            return era.HasSafetyCar
                ? new Neutralisation(NeutralisationKind.SafetyCar, lap, Math.Min(IncidentConstants.SafetyCarSeriousLaps, remaining), false)
                : yellow;
        }

        // Retirement or light injury.
        if (era.HasVsc && !multiCar)
        {
            return new Neutralisation(NeutralisationKind.VirtualSafetyCar, lap, Math.Min(IncidentConstants.VscLaps, remaining), false);
        }

        if (era.HasSafetyCar)
        {
            var laps = multiCar ? IncidentConstants.SafetyCarMultiCarLaps : IncidentConstants.SafetyCarRetirementLaps;
            return new Neutralisation(NeutralisationKind.SafetyCar, lap, Math.Min(laps, remaining), false);
        }

        return yellow;
    }
}

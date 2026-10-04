using Paddock.Application.Localization;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Reliability;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Application.Racing;

/// <summary>
/// Every translation key of the race report, and the one place that maps tape facts to keys. No enum name is ever
/// printed raw: an event kind, a retirement reason, a failed part, an incident severity or a track state is chosen
/// by key here. The i18n scan (<c>i18n-check</c>) requires each constant in both <c>strings/pl.json</c> and
/// <c>strings/en.json</c>. Entries whose line carries a count (see <see cref="RaceReportLine.Count"/>) are plural
/// entries with the forms of both languages.
/// </summary>
public static class RaceReportKeys
{
    [TranslationKey]
    public const string Title = "report.title";

    [TranslationKey]
    public const string SectionConditions = "report.section.conditions";

    [TranslationKey]
    public const string SectionQualifying = "report.section.qualifying";

    [TranslationKey]
    public const string PhaseOpening = "report.phase.opening";

    [TranslationKey]
    public const string PhaseMiddle = "report.phase.middle";

    [TranslationKey]
    public const string PhaseClosing = "report.phase.closing";

    [TranslationKey]
    public const string PhaseQuiet = "report.phase.quiet";

    [TranslationKey]
    public const string SectionPits = "report.section.pits";

    [TranslationKey]
    public const string SectionDrives = "report.section.drives";

    [TranslationKey]
    public const string SectionResult = "report.section.result";

    [TranslationKey]
    public const string SectionPoints = "report.section.points";

    [TranslationKey]
    public const string SectionLog = "report.section.log";

    [TranslationKey]
    public const string ConditionsDry = "report.conditions.start.dry";

    [TranslationKey]
    public const string ConditionsDamp = "report.conditions.start.damp";

    [TranslationKey]
    public const string ConditionsWet = "report.conditions.start.wet";

    [TranslationKey]
    public const string ConditionsExtreme = "report.conditions.start.extreme";

    [TranslationKey]
    public const string ChangeDry = "report.conditions.change.dry";

    [TranslationKey]
    public const string ChangeDamp = "report.conditions.change.damp";

    [TranslationKey]
    public const string ChangeWet = "report.conditions.change.wet";

    [TranslationKey]
    public const string ChangeExtreme = "report.conditions.change.extreme";

    [TranslationKey]
    public const string ChangeOther = "report.conditions.change.other";

    [TranslationKey]
    public const string QualifyingPole = "report.qualifying.pole";

    [TranslationKey]
    public const string QualifyingPoleAlone = "report.qualifying.poleAlone";

    [TranslationKey]
    public const string QualifyingSecond = "report.qualifying.second";

    [TranslationKey]
    public const string QualifyingThird = "report.qualifying.third";

    [TranslationKey]
    public const string QualifyingNotQualified = "report.qualifying.notQualified";

    [TranslationKey]
    public const string QualifyingPitLane = "report.qualifying.pitLane";

    [TranslationKey]
    public const string StartLeaderPole = "report.start.leaderPole";

    [TranslationKey]
    public const string StartLeaderOther = "report.start.leaderOther";

    [TranslationKey]
    public const string StartLeaderOtherOut = "report.start.leaderOtherOut";

    [TranslationKey]
    public const string StartGain = "report.start.gain";

    [TranslationKey]
    public const string StartLoss = "report.start.loss";

    [TranslationKey]
    public const string StartQuiet = "report.start.quiet";

    [TranslationKey]
    public const string LeadChange = "report.lead.change";

    [TranslationKey]
    public const string RetireEngine = "report.retire.engine";

    [TranslationKey]
    public const string RetireGearbox = "report.retire.gearbox";

    [TranslationKey]
    public const string RetireSuspension = "report.retire.suspension";

    [TranslationKey]
    public const string RetireBrakes = "report.retire.brakes";

    [TranslationKey]
    public const string RetireElectrics = "report.retire.electrics";

    [TranslationKey]
    public const string RetireCooling = "report.retire.cooling";

    [TranslationKey]
    public const string RetireMechanical = "report.retire.mechanical";

    [TranslationKey]
    public const string RetireAccident = "report.retire.accident";

    [TranslationKey]
    public const string RetireOther = "report.retire.other";

    [TranslationKey]
    public const string InjuryLight = "report.injury.light";

    [TranslationKey]
    public const string InjurySerious = "report.injury.serious";

    [TranslationKey]
    public const string InjuryCareerEnding = "report.injury.careerEnding";

    [TranslationKey]
    public const string InjuryFatal = "report.injury.fatal";

    [TranslationKey]
    public const string IncidentMinor = "report.incident.minor";

    [TranslationKey]
    public const string IncidentMajor = "report.incident.major";

    [TranslationKey]
    public const string IncidentSevere = "report.incident.severe";

    [TranslationKey]
    public const string SafetyCarDeployed = "report.neutral.safetyCar.deployed";

    [TranslationKey]
    public const string SafetyCarEnding = "report.neutral.safetyCar.ending";

    [TranslationKey]
    public const string VirtualSafetyCarDeployed = "report.neutral.virtual.deployed";

    [TranslationKey]
    public const string VirtualSafetyCarEnding = "report.neutral.virtual.ending";

    [TranslationKey]
    public const string NeutralRedFlag = "report.neutral.redFlag";

    [TranslationKey]
    public const string RedFlagRestart = "report.neutral.redFlag.restart";

    [TranslationKey]
    public const string PitsNone = "report.pits.none";

    [TranslationKey]
    public const string PitsTotal = "report.pits.total";

    [TranslationKey]
    public const string PitsNoStops = "report.pits.noStops";

    [TranslationKey]
    public const string PitsByCount = "report.pits.byCount";

    [TranslationKey]
    public const string PitsFastest = "report.pits.fastest";

    [TranslationKey]
    public const string PitsSlowest = "report.pits.slowest";

    [TranslationKey]
    public const string PitsRepair = "report.pits.repair";

    [TranslationKey]
    public const string PitsSlowStops = "report.pits.slowStops";

    [TranslationKey]
    public const string PitsFaultError = "report.pits.faultError";

    [TranslationKey]
    public const string DriveSwap = "report.drive.swap";

    [TranslationKey]
    public const string DriveShared = "report.drive.shared";

    [TranslationKey]
    public const string ResultWinner = "report.result.winner";

    [TranslationKey]
    public const string ResultMarginTime = "report.result.marginTime";

    [TranslationKey]
    public const string ResultMarginLaps = "report.result.marginLaps";

    [TranslationKey]
    public const string ResultAlone = "report.result.alone";

    [TranslationKey]
    public const string ResultNoWinner = "report.result.noWinner";

    [TranslationKey]
    public const string ResultFastest = "report.result.fastest";

    [TranslationKey]
    public const string ResultFinishers = "report.result.finishers";

    [TranslationKey]
    public const string ResultAttrition = "report.result.attrition";

    [TranslationKey]
    public const string ResultNoRetirements = "report.result.noRetirements";

    [TranslationKey]
    public const string ResultLeadLap = "report.result.leadLap";

    [TranslationKey]
    public const string ResultLapped = "report.result.lapped";

    [TranslationKey]
    public const string ResultLeadChanges = "report.result.leadChanges";

    [TranslationKey]
    public const string ResultLedAll = "report.result.ledAll";

    [TranslationKey]
    public const string ResultMostLaps = "report.result.mostLaps";

    [TranslationKey]
    public const string ResultShortened = "report.result.shortened";

    [TranslationKey]
    public const string PointsDriver = "report.points.driver";

    [TranslationKey]
    public const string PointsTeam = "report.points.team";

    [TranslationKey]
    public const string PointsNone = "report.points.none";

    [TranslationKey]
    public const string EventRaceStarted = "report.event.raceStarted";

    [TranslationKey]
    public const string EventLap = "report.event.lap";

    [TranslationKey]
    public const string EventPitIn = "report.event.pitIn";

    [TranslationKey]
    public const string EventPitOut = "report.event.pitOut";

    [TranslationKey]
    public const string EventPosition = "report.event.position";

    [TranslationKey]
    public const string EventFastestLap = "report.event.fastestLap";

    [TranslationKey]
    public const string EventFinished = "report.event.finished";

    [TranslationKey]
    public const string EventRaceEnded = "report.event.raceEnded";

    /// <summary>
    /// The key a tape event is reported under in the lap-by-lap section. Every <see cref="RaceEventKind"/> has one; a
    /// kind with variants (pit lane in or out, safety car or virtual, severity) picks by variant. The retirement key here
    /// is the general one; the narrative picks the failed part's key through <see cref="RetirementKey"/>.
    /// </summary>
    public static string For(RaceEvent raceEvent)
    {
        ArgumentNullException.ThrowIfNull(raceEvent);
        return raceEvent switch
        {
            RaceStarted => EventRaceStarted,
            LapCompleted => EventLap,
            PitStop { Phase: PitLanePhase.In } => EventPitIn,
            PitStop { Phase: PitLanePhase.Out } => EventPitOut,
            PositionChange => EventPosition,
            Incident incident => IncidentKey(incident.Severity),
            Retirement retirement => RetirementKey(retirement.Reason, null),
            WeatherChange change => ConditionChangeKey(change.Condition),
            SafetyCar { IsVirtual: false, Phase: SafetyCarPhase.Deployed } => SafetyCarDeployed,
            SafetyCar { IsVirtual: false, Phase: SafetyCarPhase.Ending } => SafetyCarEnding,
            SafetyCar { IsVirtual: true, Phase: SafetyCarPhase.Deployed } => VirtualSafetyCarDeployed,
            SafetyCar { IsVirtual: true, Phase: SafetyCarPhase.Ending } => VirtualSafetyCarEnding,
            RedFlag => NeutralRedFlag,
            FastestLap => EventFastestLap,
            Finished => EventFinished,
            RaceEnded => EventRaceEnded,
            _ => throw new ArgumentOutOfRangeException(nameof(raceEvent), raceEvent.GetType().Name, "No report key for this event."),
        };
    }

    public static string IncidentKey(IncidentSeverity severity) => severity switch
    {
        IncidentSeverity.Minor => IncidentMinor,
        IncidentSeverity.Major => IncidentMajor,
        IncidentSeverity.Severe => IncidentSevere,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
    };

    /// <summary>The key of a retirement: the failed part for a mechanical one when it is known, else the general bucket.</summary>
    public static string RetirementKey(RetirementReason reason, MechanicalComponent? component) => reason switch
    {
        RetirementReason.Mechanical => component switch
        {
            MechanicalComponent.Engine => RetireEngine,
            MechanicalComponent.Gearbox => RetireGearbox,
            MechanicalComponent.Suspension => RetireSuspension,
            MechanicalComponent.Brakes => RetireBrakes,
            MechanicalComponent.ElectricsHydraulics => RetireElectrics,
            MechanicalComponent.Cooling => RetireCooling,
            null => RetireMechanical,
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        },
        RetirementReason.Accident => RetireAccident,
        RetirementReason.Other => RetireOther,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };

    public static string InjuryKey(InjuryGrade grade) => grade switch
    {
        InjuryGrade.Light => InjuryLight,
        InjuryGrade.Serious => InjurySerious,
        InjuryGrade.CareerEnding => InjuryCareerEnding,
        _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "No key for an uninjured driver."),
    };

    public static string ConditionStartKey(WetnessBand band) => band switch
    {
        WetnessBand.Dry => ConditionsDry,
        WetnessBand.Damp => ConditionsDamp,
        WetnessBand.Wet => ConditionsWet,
        WetnessBand.Extreme => ConditionsExtreme,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };

    public static string ConditionChangeKey(WetnessBand band) => band switch
    {
        WetnessBand.Dry => ChangeDry,
        WetnessBand.Damp => ChangeDamp,
        WetnessBand.Wet => ChangeWet,
        WetnessBand.Extreme => ChangeExtreme,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };

    /// <summary>The key of a <see cref="WeatherChange"/> condition text (the lower-case band name); an unknown text gets the neutral key, never its own text.</summary>
    public static string ConditionChangeKey(string condition) =>
        Enum.TryParse<WetnessBand>(condition, ignoreCase: true, out var band) && Enum.IsDefined(band)
            ? ConditionChangeKey(band)
            : ChangeOther;
}

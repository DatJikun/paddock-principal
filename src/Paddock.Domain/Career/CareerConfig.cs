using System.Globalization;
using System.Text;

namespace Paddock.Domain.Career;

/// <summary>
/// How real people enter the career (PP-046).
/// </summary>
public enum PeopleSource
{
    RealTrajectory = 0,
    RealPotential = 1,
    RealNamesRandomSkills = 2,
    FullyGenerated = 3,
}

/// <summary>
/// Where sporting and technical rules come from (PP-046).
/// </summary>
public enum RulesSource
{
    Historical = 0,
    VotedEachSeason = 1,
}

/// <summary>
/// How AI actors choose transfers, entries, and exits (PP-046).
/// Replay is a script of events, not knowledge of the future.
/// </summary>
public enum AiBehavior
{
    ReplayHistory = 0,
    ReactToSituation = 1,
    PureRandom = 2,
}

/// <summary>
/// PP-006. <see cref="Off"/> is the default: a serious accident ends in injury or the end of a career.
/// <see cref="On"/> allows a fatal outcome. Real tragedies are never scripted.
/// </summary>
public enum FatalityLevel
{
    Off = 0,
    On = 1,
}

/// <summary>
/// Named composition of the mode axes. <see cref="Custom"/> is derived; it is not a selectable preset.
/// </summary>
public enum CareerPreset
{
    MostHistorical = 0,
    Balanced = 1,
    Chaos = 2,
    Custom = 3,
}

/// <summary>
/// Translation keys for <see cref="CareerConfig.Validate"/>.
/// There is no player-facing screen in this task; the UI shows these keys later.
/// </summary>
public static class CareerConfigCodes
{
    public const string PeopleSource = "config.error.people_source";

    public const string RulesSource = "config.error.rules_source";

    public const string AiBehavior = "config.error.ai_behavior";

    public const string FatalityLevel = "config.error.fatality_level";

    public const string HistoryStrengthRange = "config.error.history_strength_range";

    public const string RandomnessRange = "config.error.randomness_range";

    public const string StartYearMin = "config.error.start_year_min";

    public const string PlayerTeamRequired = "config.error.player_team_required";

    public const string ReplayNeedsHistoricalRules = "config.error.replay_needs_historical_rules";

    public const string ReplayNeedsRealPeople = "config.error.replay_needs_real_people";

    public const string LateStartNeedsGeneratedPeople = "config.error.late_start_needs_generated_people";

    public const string HistoryStrengthWithPureRandom = "config.warning.history_strength_with_pure_random";
}

/// <summary>
/// One validation failure or warning. <see cref="Code"/> is a translation key.
/// <see cref="Arguments"/> are invariant display values (ranges, years), not sentences.
/// </summary>
public sealed class CareerConfigIssue : IEquatable<CareerConfigIssue>
{
    private readonly string[] _arguments;

    public CareerConfigIssue(string code, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(arguments);
        Code = code;
        _arguments = arguments.ToArray();
    }

    public string Code { get; }

    public IReadOnlyList<string> Arguments => _arguments;

    public static CareerConfigIssue Of(string code, params string[] arguments) => new(code, arguments);

    public bool Equals(CareerConfigIssue? other)
    {
        return other is not null
            && string.Equals(Code, other.Code, StringComparison.Ordinal)
            && _arguments.SequenceEqual(other._arguments, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as CareerConfigIssue);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Code, StringComparer.Ordinal);
        foreach (var argument in _arguments)
        {
            hash.Add(argument, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Every problem <see cref="CareerConfig.Validate"/> found.
/// <see cref="IsValid"/> ignores warnings.
/// </summary>
public sealed class CareerConfigValidation
{
    public CareerConfigValidation(
        IReadOnlyList<CareerConfigIssue> errors,
        IReadOnlyList<CareerConfigIssue> warnings)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);
        Errors = errors;
        Warnings = warnings;
    }

    public IReadOnlyList<CareerConfigIssue> Errors { get; }

    public IReadOnlyList<CareerConfigIssue> Warnings { get; }

    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Career mode axes and the preset that composes them (PP-046).
/// Immutable. No I/O. Systems should read the axes, not <see cref="PresetName"/>.
/// Slider bounds and preset numbers live on this type so they can be edited in one place.
/// </summary>
public sealed record CareerConfig
{
    /// <summary>Inclusive lower bound for <see cref="HistoryStrength"/> and the Chaos preset.</summary>
    public const int MinHistoryStrength = 0;

    /// <summary>Inclusive upper bound for <see cref="HistoryStrength"/> and the Most Historical preset.</summary>
    public const int MaxHistoryStrength = 100;

    /// <summary>Inclusive lower bound for <see cref="RandomnessLevel"/>.</summary>
    public const int MinRandomnessLevel = 0;

    /// <summary>Inclusive upper bound for <see cref="RandomnessLevel"/>.</summary>
    public const int MaxRandomnessLevel = 100;

    /// <summary>
    /// Balanced history strength. The task describes this as about 50; the stored value is exactly 50.
    /// </summary>
    public const int BalancedHistoryStrength = 50;

    /// <summary>
    /// Randomness shared by every preset. The task does not give a per-preset randomness value.
    /// </summary>
    public const int DefaultRandomnessLevel = 50;

    /// <summary>Earliest start year (PP-001).</summary>
    public const int MinStartYear = 1950;

    /// <summary>
    /// Last year that may use real people. A later <see cref="StartYear"/> requires
    /// <see cref="PeopleSource.FullyGenerated"/>.
    /// </summary>
    public const int RealPeopleLastYear = 2026;

    /// <summary>Pure-random AI treats history strength above this value as unused (warning, not an error).</summary>
    public const int PureRandomHistoryStrengthWarningAbove = 0;

    /// <summary>Sentinel <see cref="PlayerTeam"/> for a team founded at career start.</summary>
    public const string NewTeam = "NewTeam";

    public CareerConfig(
        PeopleSource peopleSource,
        RulesSource rulesSource,
        AiBehavior aiBehavior,
        int historyStrength,
        int randomnessLevel,
        FatalityLevel fatalityLevel,
        int startYear,
        string playerTeam,
        bool noNumbers)
    {
        ArgumentNullException.ThrowIfNull(playerTeam);
        PeopleSource = peopleSource;
        RulesSource = rulesSource;
        AiBehavior = aiBehavior;
        HistoryStrength = historyStrength;
        RandomnessLevel = randomnessLevel;
        FatalityLevel = fatalityLevel;
        StartYear = startYear;
        PlayerTeam = playerTeam;
        NoNumbers = noNumbers;
    }

    public PeopleSource PeopleSource { get; }

    public RulesSource RulesSource { get; }

    public AiBehavior AiBehavior { get; }

    public int HistoryStrength { get; }

    public int RandomnessLevel { get; }

    public FatalityLevel FatalityLevel { get; }

    public int StartYear { get; }

    public string PlayerTeam { get; }

    public bool NoNumbers { get; }

    /// <summary>
    /// The preset these axes match, or <see cref="CareerPreset.Custom"/> when any axis differs.
    /// </summary>
    public CareerPreset PresetName =>
        Matches(CareerPreset.MostHistorical) ? CareerPreset.MostHistorical
        : Matches(CareerPreset.Balanced) ? CareerPreset.Balanced
        : Matches(CareerPreset.Chaos) ? CareerPreset.Chaos
        : CareerPreset.Custom;

    public static CareerConfig FromPreset(CareerPreset preset)
    {
        if (!Enum.IsDefined(preset) || preset == CareerPreset.Custom)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preset),
                preset,
                "Custom is derived from the axes and is not a selectable preset.");
        }

        return preset switch
        {
            CareerPreset.MostHistorical => new CareerConfig(
                PeopleSource.RealTrajectory,
                RulesSource.Historical,
                AiBehavior.ReplayHistory,
                MaxHistoryStrength,
                DefaultRandomnessLevel,
                FatalityLevel.Off,
                MinStartYear,
                NewTeam,
                noNumbers: false),
            CareerPreset.Balanced => new CareerConfig(
                PeopleSource.RealPotential,
                RulesSource.Historical,
                AiBehavior.ReactToSituation,
                BalancedHistoryStrength,
                DefaultRandomnessLevel,
                FatalityLevel.Off,
                MinStartYear,
                NewTeam,
                noNumbers: false),
            CareerPreset.Chaos => new CareerConfig(
                PeopleSource.FullyGenerated,
                RulesSource.VotedEachSeason,
                AiBehavior.PureRandom,
                MinHistoryStrength,
                DefaultRandomnessLevel,
                FatalityLevel.Off,
                MinStartYear,
                NewTeam,
                noNumbers: false),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
        };
    }

    public CareerConfig WithPeopleSource(PeopleSource peopleSource) =>
        new(
            peopleSource,
            RulesSource,
            AiBehavior,
            HistoryStrength,
            RandomnessLevel,
            FatalityLevel,
            StartYear,
            PlayerTeam,
            NoNumbers);

    public CareerConfig WithRulesSource(RulesSource rulesSource) =>
        new(
            PeopleSource,
            rulesSource,
            AiBehavior,
            HistoryStrength,
            RandomnessLevel,
            FatalityLevel,
            StartYear,
            PlayerTeam,
            NoNumbers);

    public CareerConfig WithAiBehavior(AiBehavior aiBehavior) =>
        new(
            PeopleSource,
            RulesSource,
            aiBehavior,
            HistoryStrength,
            RandomnessLevel,
            FatalityLevel,
            StartYear,
            PlayerTeam,
            NoNumbers);

    public CareerConfig WithHistoryStrength(int historyStrength) =>
        new(
            PeopleSource,
            RulesSource,
            AiBehavior,
            historyStrength,
            RandomnessLevel,
            FatalityLevel,
            StartYear,
            PlayerTeam,
            NoNumbers);

    public CareerConfig WithRandomnessLevel(int randomnessLevel) =>
        new(
            PeopleSource,
            RulesSource,
            AiBehavior,
            HistoryStrength,
            randomnessLevel,
            FatalityLevel,
            StartYear,
            PlayerTeam,
            NoNumbers);

    public CareerConfig WithFatalityLevel(FatalityLevel fatalityLevel) =>
        new(
            PeopleSource,
            RulesSource,
            AiBehavior,
            HistoryStrength,
            RandomnessLevel,
            fatalityLevel,
            StartYear,
            PlayerTeam,
            NoNumbers);

    public CareerConfig WithStartYear(int startYear) =>
        new(
            PeopleSource,
            RulesSource,
            AiBehavior,
            HistoryStrength,
            RandomnessLevel,
            FatalityLevel,
            startYear,
            PlayerTeam,
            NoNumbers);

    public CareerConfig WithPlayerTeam(string playerTeam) =>
        new(
            PeopleSource,
            RulesSource,
            AiBehavior,
            HistoryStrength,
            RandomnessLevel,
            FatalityLevel,
            StartYear,
            playerTeam,
            NoNumbers);

    public CareerConfig WithNoNumbers(bool noNumbers) =>
        new(
            PeopleSource,
            RulesSource,
            AiBehavior,
            HistoryStrength,
            RandomnessLevel,
            FatalityLevel,
            StartYear,
            PlayerTeam,
            noNumbers);

    public CareerConfigValidation Validate()
    {
        var errors = new List<CareerConfigIssue>();
        var peopleDefined = Enum.IsDefined(PeopleSource);
        var rulesDefined = Enum.IsDefined(RulesSource);
        var aiDefined = Enum.IsDefined(AiBehavior);

        if (!peopleDefined)
        {
            errors.Add(CareerConfigIssue.Of(CareerConfigCodes.PeopleSource));
        }

        if (!rulesDefined)
        {
            errors.Add(CareerConfigIssue.Of(CareerConfigCodes.RulesSource));
        }

        if (!aiDefined)
        {
            errors.Add(CareerConfigIssue.Of(CareerConfigCodes.AiBehavior));
        }

        if (!Enum.IsDefined(FatalityLevel))
        {
            errors.Add(CareerConfigIssue.Of(CareerConfigCodes.FatalityLevel));
        }

        if (HistoryStrength < MinHistoryStrength || HistoryStrength > MaxHistoryStrength)
        {
            errors.Add(CareerConfigIssue.Of(
                CareerConfigCodes.HistoryStrengthRange,
                Invariant(MinHistoryStrength),
                Invariant(MaxHistoryStrength)));
        }

        if (RandomnessLevel < MinRandomnessLevel || RandomnessLevel > MaxRandomnessLevel)
        {
            errors.Add(CareerConfigIssue.Of(
                CareerConfigCodes.RandomnessRange,
                Invariant(MinRandomnessLevel),
                Invariant(MaxRandomnessLevel)));
        }

        if (StartYear < MinStartYear)
        {
            errors.Add(CareerConfigIssue.Of(CareerConfigCodes.StartYearMin, Invariant(MinStartYear)));
        }

        if (string.IsNullOrWhiteSpace(PlayerTeam))
        {
            errors.Add(CareerConfigIssue.Of(CareerConfigCodes.PlayerTeamRequired));
        }

        if (aiDefined && AiBehavior == AiBehavior.ReplayHistory && rulesDefined && RulesSource != RulesSource.Historical)
        {
            errors.Add(CareerConfigIssue.Of(CareerConfigCodes.ReplayNeedsHistoricalRules));
        }

        if (aiDefined
            && AiBehavior == AiBehavior.ReplayHistory
            && peopleDefined
            && PeopleSource is not (PeopleSource.RealTrajectory or PeopleSource.RealPotential))
        {
            errors.Add(CareerConfigIssue.Of(CareerConfigCodes.ReplayNeedsRealPeople));
        }

        if (StartYear > RealPeopleLastYear && peopleDefined && PeopleSource != PeopleSource.FullyGenerated)
        {
            errors.Add(CareerConfigIssue.Of(
                CareerConfigCodes.LateStartNeedsGeneratedPeople,
                Invariant(RealPeopleLastYear)));
        }

        var warnings = new List<CareerConfigIssue>();
        if (aiDefined
            && AiBehavior == AiBehavior.PureRandom
            && HistoryStrength > PureRandomHistoryStrengthWarningAbove
            && HistoryStrength <= MaxHistoryStrength)
        {
            warnings.Add(CareerConfigIssue.Of(CareerConfigCodes.HistoryStrengthWithPureRandom));
        }

        return new CareerConfigValidation(errors, warnings);
    }

    /// <summary>
    /// Compact JSON with a fixed property order. This string is the save payload and the hash input.
    /// </summary>
    public string ToCanonicalJson()
    {
        var builder = new StringBuilder(256);
        builder.Append('{');
        AppendString(builder, first: true, "peopleSource", PeopleSource.ToString());
        AppendString(builder, first: false, "rulesSource", RulesSource.ToString());
        AppendString(builder, first: false, "aiBehavior", AiBehavior.ToString());
        AppendNumber(builder, "historyStrength", HistoryStrength);
        AppendNumber(builder, "randomnessLevel", RandomnessLevel);
        AppendString(builder, first: false, "fatalityLevel", FatalityLevel.ToString());
        AppendNumber(builder, "startYear", StartYear);
        AppendString(builder, first: false, "playerTeam", PlayerTeam);
        AppendBool(builder, "noNumbers", NoNumbers);
        AppendString(builder, first: false, "presetName", PresetName.ToString());
        builder.Append('}');
        return builder.ToString();
    }

    private bool Matches(CareerPreset preset)
    {
        var prototype = FromPreset(preset);
        return PeopleSource == prototype.PeopleSource
            && RulesSource == prototype.RulesSource
            && AiBehavior == prototype.AiBehavior
            && HistoryStrength == prototype.HistoryStrength
            && RandomnessLevel == prototype.RandomnessLevel
            && FatalityLevel == prototype.FatalityLevel
            && StartYear == prototype.StartYear
            && string.Equals(PlayerTeam, prototype.PlayerTeam, StringComparison.Ordinal)
            && NoNumbers == prototype.NoNumbers;
    }

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void AppendString(StringBuilder builder, bool first, string name, string value)
    {
        if (!first)
        {
            builder.Append(',');
        }

        builder.Append('"').Append(name).Append("\":");
        AppendJsonString(builder, value);
    }

    private static void AppendNumber(StringBuilder builder, string name, int value)
    {
        builder.Append(",\"").Append(name).Append("\":").Append(Invariant(value));
    }

    private static void AppendBool(StringBuilder builder, string name, bool value)
    {
        builder.Append(",\"").Append(name).Append("\":").Append(value ? "true" : "false");
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}

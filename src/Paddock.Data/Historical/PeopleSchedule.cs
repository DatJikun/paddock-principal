using System.Globalization;

namespace Paddock.Data.Historical;

/// <summary>
/// Named settings for the historical people schedule.
/// Edit these rather than literals at the call site.
/// Pool lead is a placeholder until calibration (ROADMAP open question 4).
/// </summary>
public static class PeopleScheduleRules
{
    /// <summary>Default of <c>--pool-lead-years</c>. Not a whole year; see <see cref="ResolveLeadYears"/>.</summary>
    public const decimal DefaultPoolLeadYears = 2.5m;

    /// <summary>
    /// Debut season at which a half-year lead rounds up.
    /// The default 2.5 therefore means 2 years before 1980 and 3 years from 1980.
    /// This split is a stand-in, not a measured historical fact.
    /// </summary>
    public const int PoolDebutSplitSeason = 1980;

    /// <summary>Pool entry is not earlier than this many years after the birth year.</summary>
    public const int PoolMinimumAgeYears = 17;

    /// <summary>A season-and-constructor with this many Grand Prix starts, or fewer, is a substitute.</summary>
    public const int SubstituteMaxStarts = 3;

    public static bool IsValidLead(decimal leadYears)
    {
        if (leadYears < 0)
        {
            return false;
        }

        if (leadYears == decimal.Truncate(leadYears))
        {
            return true;
        }

        var doubled = leadYears * 2m;
        return doubled == decimal.Truncate(doubled);
    }

    /// <summary>
    /// Whole-year lead for one debut.
    /// A whole configured value is used as-is. A half-year value uses the floor when
    /// <paramref name="debutSeason"/> is before <see cref="PoolDebutSplitSeason"/>,
    /// and the ceiling otherwise.
    /// </summary>
    public static int ResolveLeadYears(decimal configuredLeadYears, int debutSeason)
    {
        if (!IsValidLead(configuredLeadYears))
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuredLeadYears),
                "Pool lead years must be a non-negative whole or half year.");
        }

        if (configuredLeadYears == decimal.Truncate(configuredLeadYears))
        {
            return (int)configuredLeadYears;
        }

        return debutSeason >= PoolDebutSplitSeason
            ? (int)decimal.Ceiling(configuredLeadYears)
            : (int)decimal.Floor(configuredLeadYears);
    }

    /// <summary>
    /// <c>max(first season − lead, birth year + minimum age)</c>.
    /// Unknown birth year uses the debut term only.
    /// The age floor is not pulled back when it falls after the debut.
    /// </summary>
    public static int EntryYear(int firstSeason, int? bornYear, decimal configuredLeadYears)
    {
        var fromDebut = firstSeason - ResolveLeadYears(configuredLeadYears, firstSeason);
        if (bornYear is null)
        {
            return fromDebut;
        }

        var fromAge = bornYear.Value + PoolMinimumAgeYears;
        return Math.Max(fromDebut, fromAge);
    }

    /// <summary>Year from an ISO date (<c>YYYY</c> or <c>YYYY-MM-DD</c>). Anything else is unknown.</summary>
    public static int? BornYear(string? dateOfBirth)
    {
        if (dateOfBirth is null || dateOfBirth.Length < 4)
        {
            return null;
        }

        if (dateOfBirth.Length > 4 && dateOfBirth[4] != '-')
        {
            return null;
        }

        if (!int.TryParse(dateOfBirth.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            return null;
        }

        return year;
    }
}

public static class ScheduleRoles
{
    public const string Race = "race";
    public const string Substitute = "substitute";
    public const string SharedDrive = "shared_drive";
}

public static class DuplicateSuspectReasons
{
    public const string SameName = "same_name";
    public const string SameBirthAndFamily = "same_birth_and_family";
}

public sealed record PeopleScheduleReport(
    decimal PoolLeadYears,
    int DebutSplitSeason,
    IReadOnlyList<ScheduledDriver> Drivers,
    IReadOnlyList<TeamChangeEvent> TeamChanges,
    IReadOnlyList<DuplicateDriverSuspect> DuplicateSuspects,
    IReadOnlyList<string> SingleStartDriverIds);

public sealed record ScheduledDriver(
    string DriverId,
    int? Born,
    string? Nationality,
    int? FirstSeason,
    int? LastSeason,
    int? PoolEntryYear,
    IReadOnlyList<DriverStint> Stints,
    IReadOnlyList<IndianapolisAppearance> Indianapolis500);

public sealed record DriverStint(
    int Season,
    string ConstructorId,
    int FirstRound,
    int LastRound,
    int Starts,
    string Role);

public sealed record IndianapolisAppearance(int Season, int Round, string ConstructorId);

/// <summary>
/// A move from one constructor to another.
/// <see cref="Round"/> is set for a mid-season move and null when the move is between seasons.
/// </summary>
public sealed record TeamChangeEvent(
    int Season,
    string DriverId,
    string FromConstructorId,
    string ToConstructorId,
    int? Round);

public sealed record DuplicateDriverSuspect(string DriverId, string OtherDriverId, string Reason);

using Paddock.Domain.Career;
using Paddock.Domain.World;

namespace Paddock.Data.World;

/// <summary>
/// Translation keys of the gaps a <see cref="WorldInitReport"/> can list. The subjects are ids, not sentences.
/// Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// </summary>
public static class WorldInitGapCodes
{
    public const string TeamsWithoutStaff = "world.init.gap.teams_without_staff";

    public const string DriversWithoutRatings = "world.init.gap.drivers_without_ratings";

    public const string StaffWithoutRatings = "world.init.gap.staff_without_ratings";

    public const string StaffUnmappedRole = "world.init.gap.staff_unmapped_role";

    public const string StaffOrganizationAbsent = "world.init.gap.staff_organization_absent";

    public const string StaffRoleUnavailable = "world.init.gap.staff_role_unavailable";

    public const string StaffBirthEstimated = "world.init.gap.staff_birth_estimated";

    public const string DriverBirthEstimated = "world.init.gap.driver_birth_estimated";

    public const string DriverSkippedNoBirth = "world.init.gap.driver_skipped_no_birth";

    public const string DriverConstructorAbsent = "world.init.gap.driver_constructor_absent";

    public const string SeatFilledFromSameSeasonStint = "world.init.gap.seat_filled_same_season_stint";

    public const string SeatFilledFromPreviousSeasonStint = "world.init.gap.seat_filled_previous_season_stint";

    public const string SeatFilledByGeneratedDriver = "world.init.gap.seat_filled_by_generated_driver";

    public const string NationalityMissing = "world.init.gap.nationality_missing";

    public const string IdCollision = "world.init.gap.id_collision";

    public const string OrganizationNameFromId = "world.init.gap.organization_name_from_id";

    public const string OrganizationFoundedFromData = "world.init.gap.organization_founded_from_data";

    public const string SupplierFoundedFromData = "world.init.gap.supplier_founded_from_data";

    public const string SupplierUnknown = "world.init.gap.supplier_unknown";

    public const string LineageTruncated = "world.init.gap.lineage_truncated";

    public const string ReferenceSeasonClamped = "world.init.gap.reference_season_clamped";

    public const string IndianapolisOnly = "world.init.gap.indianapolis_only";
}

/// <summary>Translation keys of <see cref="WorldInitException"/>.</summary>
public static class WorldInitErrorCodes
{
    public const string InvalidConfig = "world.init.error.invalid_config";

    public const string UnknownPlayerTeam = "world.init.error.unknown_player_team";
}

/// <summary>The initializer refused its input. <see cref="Code"/> is a translation key.</summary>
public sealed class WorldInitException : Exception
{
    public WorldInitException(string code, IReadOnlyList<string> arguments)
        : base(code + (arguments.Count == 0 ? string.Empty : " " + string.Join(' ', arguments)))
    {
        Code = code;
        Arguments = arguments;
    }

    public string Code { get; }

    public IReadOnlyList<string> Arguments { get; }
}

/// <summary>One kind of gap and the ids it concerns. Subjects are in ordinal order.</summary>
public sealed record WorldInitGap(string Code, IReadOnlyList<string> Subjects);

/// <summary>Counts per kind of what the world holds after initialization.</summary>
public sealed record WorldInitCounts(
    int Teams,
    int DissolvedTeams,
    int EngineSuppliers,
    int RacingDrivers,
    int PoolDrivers,
    int StaffPeople,
    int Contracts,
    int RealPersons,
    int GeneratedPersons,
    int LineageLinks);

/// <summary>What the initializer built and what it could not build from data.</summary>
public sealed record WorldInitReport(
    int StartYear,
    int ReferenceSeason,
    PeopleSource PeopleSource,
    WorldInitCounts Counts,
    IReadOnlyList<WorldInitGap> Gaps);

/// <summary>An engine supplied to a team in the reference season. Both ids are organization ids in the world.</summary>
public sealed record EngineSupplyLink(
    OrganizationId Constructor,
    OrganizationId Supplier,
    string EngineName,
    string SupplyType);

/// <summary>
/// The starting world with its report. <see cref="TalentPool"/> lists drivers who are in the world with no
/// contract and have not yet debuted; the world state itself has no pool concept.
/// </summary>
public sealed record WorldInitResult(
    WorldState World,
    WorldInitReport Report,
    IReadOnlyList<PersonId> TalentPool,
    OrganizationId PlayerOrganization,
    IReadOnlyList<EngineSupplyLink> EngineSupplies);

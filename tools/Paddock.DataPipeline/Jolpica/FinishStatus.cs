namespace Paddock.DataPipeline;

/// <summary>
/// Maps a Jolpica <c>status</c> string onto the race-result buckets used by <c>stats</c>.
/// Classified finishes and non-starts are not DNFs. A status that is not in this table is
/// unmapped: the report lists it instead of folding it into "other".
/// The strings are the Jolpica status index (136 values) plus three Ergast non-start
/// wordings that index does not currently emit.
/// </summary>
public static class FinishStatus
{
    public enum Kind
    {
        ClassifiedFinish,
        NonStart,
        Mechanical,
        Accident,
        Other,
        Unmapped,
    }

    // Not DNFs. "Lapped" is Jolpica's summary status; "+N Lap(s)" is the usual classified gap.
    private static readonly string[] ClassifiedFinishStatuses = ["Finished", "Lapped"];

    // Did not take the start. "Withdrew" in this API is a pre-race withdrawal.
    // The last three are Ergast wordings absent from the current Jolpica index.
    private static readonly string[] NonStartStatuses =
    [
        "Withdrew",
        "Did not start",
        "Did not qualify",
        "Did not prequalify",
        "107% Rule",
    ];

    // Named component or system failures. Tyres, wings, fire, and fuel-strategy
    // wordings are deliberately absent: those strings do not say whether the cause
    // was a part failure or a driving incident, so they stay unmapped.
    private static readonly string[] MechanicalStatuses =
    [
        "Engine",
        "Gearbox",
        "Transmission",
        "Suspension",
        "Electrical",
        "Brakes",
        "Clutch",
        "Hydraulics",
        "Fuel system",
        "Turbo",
        "Ignition",
        "Oil leak",
        "Throttle",
        "Halfshaft",
        "Wheel",
        "Oil pressure",
        "Fuel pump",
        "Differential",
        "Fuel leak",
        "Steering",
        "Radiator",
        "Power Unit",
        "Wheel bearing",
        "Injection",
        "Fuel pressure",
        "Water leak",
        "Alternator",
        "Exhaust",
        "Chassis",
        "Mechanical",
        "Magneto",
        "Driveshaft",
        "Axle",
        "Heat shield fire",
        "Battery",
        "Oil pump",
        "Power loss",
        "Oil pipe",
        "Electronics",
        "Water pressure",
        "Water pump",
        "ERS",
        "Supercharger",
        "Distributor",
        "Vibrations",
        "Pneumatics",
        "Technical",
        "Undertray",
        "Brake duct",
        "CV joint",
        "Crankshaft",
        "Cooling system",
        "Spark plugs",
        "Fuel pipe",
        "Engine fire",
        "Engine misfire",
        "Water pipe",
        "Wheel rim",
        "Wheel nut",
        "Oil line",
        "Drivetrain",
        "Track rod",
        "Launch control",
        "Seat",
        "Driver Seat",
    ];

    // Contact or leaving the track. "Collision damage" is the contact, not a named part failure.
    private static readonly string[] AccidentStatuses =
    [
        "Accident",
        "Collision",
        "Spun off",
        "Collision damage",
        "Fatal accident",
    ];

    // Started, did not classify, and the API did not call it a mechanical failure or an accident.
    // "Retired" and "Not classified" name no cause. Driver-condition strings stay here;
    // "Fatal accident" is the one injury wording that names the incident.
    private static readonly string[] OtherStatuses =
    [
        "Retired",
        "Disqualified",
        "Not classified",
        "Excluded",
        "Underweight",
        "Injury",
        "Injured",
        "Physical",
        "Illness",
        "Driver unwell",
        "Eye injury",
        "Safety",
        "Safety concerns",
        "Safety belt",
        "Refuelling",
        "Fuel rig",
    ];

    // Known Jolpica strings left unmapped because the wording is ambiguous.
    // Damage / wings: part failure or contact. Stalled: driver or car.
    // Fire: cause unnamed. Puncture / Tyre: debris or failure. Debris: may not be a racing incident.
    // Out of fuel / Fuel: strategy or a system failure. Not restarted: the original incident is unnamed.
    // Handling: gave up on balance, not a named failure.
    private static readonly string[] DeliberatelyUnmapped =
    [
        "Damage",
        "Broken wing",
        "Front wing",
        "Rear wing",
        "Stalled",
        "Fire",
        "Puncture",
        "Tyre",
        "Tyre puncture",
        "Debris",
        "Out of fuel",
        "Fuel",
        "Not restarted",
        "Handling",
    ];

    private static readonly HashSet<string> ClassifiedFinishes = new(ClassifiedFinishStatuses, StringComparer.Ordinal);
    private static readonly HashSet<string> NonStarts = new(NonStartStatuses, StringComparer.Ordinal);
    private static readonly Dictionary<string, Kind> DnfBuckets = BuildDnfBuckets();

    public static IReadOnlyList<string> DeliberatelyUnmappedStatuses => DeliberatelyUnmapped;

    static FinishStatus()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        RequireUnique(seen, ClassifiedFinishStatuses);
        RequireUnique(seen, NonStartStatuses);
        RequireUnique(seen, MechanicalStatuses);
        RequireUnique(seen, AccidentStatuses);
        RequireUnique(seen, OtherStatuses);
        RequireUnique(seen, DeliberatelyUnmapped);
    }

    public static Kind Classify(string status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (ClassifiedFinishes.Contains(status) || IsLapDown(status))
        {
            return Kind.ClassifiedFinish;
        }

        if (NonStarts.Contains(status))
        {
            return Kind.NonStart;
        }

        if (DnfBuckets.TryGetValue(status, out var kind))
        {
            return kind;
        }

        return Kind.Unmapped;
    }

    private static Dictionary<string, Kind> BuildDnfBuckets()
    {
        var map = new Dictionary<string, Kind>(StringComparer.Ordinal);
        Add(map, MechanicalStatuses, Kind.Mechanical);
        Add(map, AccidentStatuses, Kind.Accident);
        Add(map, OtherStatuses, Kind.Other);
        return map;
    }

    private static void Add(Dictionary<string, Kind> map, string[] statuses, Kind kind)
    {
        foreach (var status in statuses)
        {
            map.Add(status, kind);
        }
    }

    private static void RequireUnique(HashSet<string> seen, string[] statuses)
    {
        foreach (var status in statuses)
        {
            if (!seen.Add(status))
            {
                throw new InvalidOperationException($"Finish status '{status}' is listed twice.");
            }
        }
    }

    private static bool IsLapDown(string status)
    {
        if (status.Length < 6 || status[0] != '+')
        {
            return false;
        }

        var space = status.IndexOf(' ');
        if (space <= 1 || space == status.Length - 1)
        {
            return false;
        }

        for (var i = 1; i < space; i++)
        {
            if (!char.IsAsciiDigit(status[i]))
            {
                return false;
            }
        }

        var word = status.Substring(space + 1);
        return word is "Lap" or "Laps";
    }
}

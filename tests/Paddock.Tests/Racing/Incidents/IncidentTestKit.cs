using Paddock.Domain.Random;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Tests.Racing.Points;

namespace Paddock.Tests.Racing.Incidents;

internal static class IncidentTestKit
{
    public const ulong Seed = 20_261_003UL;

    public static readonly IncidentCar Typical = new("car-0", 50, 50);

    public static readonly IReadOnlyList<string> ThreeNear = ["near-a", "near-b", "near-c"];

    /// <summary>The era's safety profile built from the real authored data, so the tests follow the data and not a copy of it.</summary>
    public static EraSafetyProfile EraOf(int season) =>
        EraSafetyProfile.FromAuthored(
            season,
            PointsTestKit.Real.EraSetFor(season).Value("fatality_risk"),
            PointsTestKit.Real.RuleSetFor(season).Value("safety_car"));

    public static IncidentRaceContext Race(EraSafetyProfile era, bool fatalities = false, double danger = 1.0) =>
        new(era, danger, fatalities);

    public static IncidentRaceContext Race(int season, bool fatalities = false, double danger = 1.0) =>
        Race(EraOf(season), fatalities, danger);

    public static RngStream Stream(int season = 1955, int round = 1, ulong seed = Seed) =>
        IncidentSampler.DeriveRaceStream(seed, season, round);

    public static LapContext Lap(int lap, double wetness = 0, IReadOnlyList<string>? nearby = null) =>
        new(lap, wetness, nearby ?? ThreeNear);

    /// <summary>
    /// Incidents of a stream of fresh cars on one lap, until <paramref name="count"/> incidents have happened.
    /// The cars are all given the same traits and the same neighbours.
    /// </summary>
    public static IEnumerable<IncidentResult> SampleIncidents(
        int count,
        IncidentRaceContext race,
        RngStream stream,
        int lap = 1,
        double wetness = 0,
        double aggression = 50,
        double composure = 50)
    {
        var found = 0;
        for (var i = 0; found < count; i++)
        {
            var car = new IncidentCar("c" + i, aggression, composure);
            var result = IncidentSampler.SampleLap(stream, car, Lap(lap, wetness), race);
            if (result is not null)
            {
                found++;
                yield return result;
            }
        }
    }

    public static IncidentResult Single(
        OutcomeSeverity severity,
        InjuryGrade grade = InjuryGrade.None,
        IncidentKind kind = IncidentKind.Barrier,
        int lap = 10,
        IncidentOutcome? other = null)
    {
        var outcome = severity switch
        {
            OutcomeSeverity.None => IncidentOutcome.None,
            OutcomeSeverity.Minor => IncidentOutcome.Minor,
            OutcomeSeverity.Retire => IncidentOutcome.Retire,
            OutcomeSeverity.Injury => IncidentOutcome.Injured(grade),
            _ => IncidentOutcome.Fatal,
        };
        return new IncidentResult(
            lap,
            kind,
            new IncidentParticipant("a", IncidentRole.Instigator, outcome),
            other is null ? null : new IncidentParticipant("b", IncidentRole.Other, other));
    }
}

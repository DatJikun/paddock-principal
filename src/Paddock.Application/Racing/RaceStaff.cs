using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Racing;

/// <summary>
/// What a team's own people bring to a race: the strategist's skill and forecast, the chief mechanic's pit crew. The weekend
/// takes these numbers; the stops and the strategy are still decided inside the race by the strategist (T34), never by a script.
/// </summary>
/// <param name="StrategistId">The strategist whose numbers these are; null when the chair is empty and the neutral values apply.</param>
/// <param name="StrategistSkill">0..100, the strategist's misjudgement scale (<c>RuleBasedStrategist</c>).</param>
/// <param name="ForecastQuality">0..1, how close the team's forecast is to the truth.</param>
/// <param name="PitCrewQuality">0..100, the pit crew of <c>PitCrew</c>.</param>
public sealed record RaceStaff(string? StrategistId, int StrategistSkill, double ForecastQuality, double PitCrewQuality)
{
    public static RaceStaff Neutral { get; } = new(
        null,
        RacingEstimates.NeutralStrategistSkill,
        RacingEstimates.NeutralForecastQuality,
        RacingEstimates.NeutralPitCrewQuality);

    /// <summary>
    /// The team's race staff on <paramref name="on"/>, read from the contracts of the world (truth: the race is the
    /// simulation, not a manager's view). The strategist sets skill and forecast, the chief mechanic the crew. An empty chair
    /// keeps that part neutral. Two people on one chair: the lowest id, so the choice never depends on list order (INV-002).
    /// </summary>
    public static RaceStaff Of(WorldState world, OrganizationId team, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        var strategist = Holder(world, team, on, StaffRole.Strategist);
        var mechanic = Holder(world, team, on, StaffRole.ChiefMechanic);
        if (strategist is null && mechanic is null)
        {
            return Neutral;
        }

        var skill = RacingEstimates.NeutralStrategistSkill;
        var forecast = RacingEstimates.NeutralForecastQuality;
        if (strategist is not null)
        {
            var judgement = (Value(strategist, "strategy") * RaceStaffEstimates.StrategyWeight)
                + (Value(strategist, "reaction") * (1 - RaceStaffEstimates.StrategyWeight));
            skill = (int)Math.Round(Scale(judgement) * 100, MidpointRounding.AwayFromZero);
            forecast = RaceStaffEstimates.ForecastFloor
                + ((RaceStaffEstimates.ForecastCeiling - RaceStaffEstimates.ForecastFloor) * Scale(Value(strategist, "weather")));
        }

        var crew = mechanic is null
            ? RacingEstimates.NeutralPitCrewQuality
            : Math.Round(Scale(Value(mechanic, "pit_stops")) * 100, MidpointRounding.AwayFromZero);
        return new RaceStaff(strategist?.Id.Value, skill, forecast, crew);
    }

    private static Person? Holder(WorldState world, OrganizationId team, GameDate on, StaffRole role)
    {
        Person? found = null;
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != team
                || !contract.IsActiveOn(on)
                || !contract.Role.IsStaff
                || contract.Role.StaffRole != role)
            {
                continue;
            }

            var person = world.GetPerson(contract.PersonId);
            if (person.IsRetired)
            {
                continue;
            }

            if (found is null || string.CompareOrdinal(person.Id.Value, found.Id.Value) < 0)
            {
                found = person;
            }
        }

        return found;
    }

    /// <summary>An attribute on the 1..20 scale; a person without the attribute counts as the middle of it.</summary>
    private static double Value(Person person, string key)
    {
        foreach (var attribute in person.Truth.Attributes)
        {
            if (string.Equals(attribute.Key, key, StringComparison.Ordinal))
            {
                return attribute.Value;
            }
        }

        return (GenerationEstimates.AttributeMax + 1) / 2.0;
    }

    /// <summary>1..20 onto 0..1.</summary>
    private static double Scale(double value) =>
        Math.Clamp((value - 1) / (GenerationEstimates.AttributeMax - 1), 0, 1);
}

/// <summary>Guessed mappings from staff attributes to race numbers. None of these is calibrated.</summary>
public static class RaceStaffEstimates
{
    /// <summary>ESTIMATE: the strategist's skill is mostly planning ("strategy"), partly how fast they react ("reaction").</summary>
    public const double StrategyWeight = 0.7;

    /// <summary>ESTIMATE: the forecast quality of a strategist with the lowest "weather" attribute.</summary>
    public const double ForecastFloor = 0.75;

    /// <summary>ESTIMATE: the forecast quality of a strategist with the highest "weather" attribute (never a perfect forecast).</summary>
    public const double ForecastCeiling = 0.98;
}

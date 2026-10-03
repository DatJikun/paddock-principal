using System.Collections.Immutable;

namespace Paddock.Simulation.Racing.Points;

/// <summary>
/// Points a driver took from one race. <see cref="Position"/> is the classified position of the car the driver
/// scored in, or null when the driver has no classified result (not classified, or a shared-drive partner whose
/// car's points were not split). It feeds the tie-break by finishing positions.
/// </summary>
public sealed record DriverScore(string DriverId, decimal Points, int? Position);

/// <summary>Points a constructor took from one race and the classified positions of its cars.</summary>
public sealed record ConstructorScore(string ConstructorId, decimal Points, ImmutableArray<int> Positions);

/// <summary>
/// One car in the classification. <see cref="Position"/> runs 1..n over the classified cars in finishing order;
/// cars that are not classified follow with the next numbers, in the order they were given.
/// </summary>
/// <param name="CarPoints">The points the car earned, with the fastest-lap point and double points, before any shared-drive split.</param>
/// <param name="ConstructorPoints">What the car adds to its constructor under the era's counting rule.</param>
public sealed record ClassifiedCar(
    int Position,
    bool IsClassified,
    string ConstructorId,
    ImmutableArray<string> DriverIds,
    decimal CarPoints,
    decimal ConstructorPoints);

/// <summary>The outcome of one race: positions, who is classified, and the points every driver and constructor takes.</summary>
public sealed record RaceClassification(
    ImmutableArray<ClassifiedCar> Cars,
    ImmutableArray<DriverScore> DriverScores,
    ImmutableArray<ConstructorScore> ConstructorScores);

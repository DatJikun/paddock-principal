namespace Paddock.Domain.World;

/// <summary>
/// One engine supplied to a constructor in a season.
/// Several supplies for the same constructor and season are allowed.
/// </summary>
public readonly record struct EngineSupply(
    string ConstructorId,
    int Season,
    string Supplier,
    string EngineName,
    string SupplyType);

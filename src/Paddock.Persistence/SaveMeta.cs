namespace Paddock.Persistence;

/// <summary>
/// The single career header stored in <c>meta</c>.
/// Timestamps are UTC ISO-8601 (<c>yyyy-MM-ddTHH:mm:ss.fffffffZ</c>).
/// The game date is a calendar date (<c>yyyy-MM-dd</c>).
/// <see cref="SchemaVersion"/>, <see cref="CreatedAtUtc"/>, and <see cref="SavedAtUtc"/>
/// are assigned by <see cref="SaveFile.Create"/>; values on the instance passed in are ignored.
/// </summary>
public sealed record SaveMeta
{
    public SaveMeta(
        string careerName,
        string managerName,
        string playerTeamId,
        DateOnly currentGameDate,
        string worldDataHash,
        ulong masterSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(careerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerTeamId);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDataHash);

        CareerName = careerName;
        ManagerName = managerName;
        PlayerTeamId = playerTeamId;
        CurrentGameDate = currentGameDate;
        WorldDataHash = worldDataHash;
        MasterSeed = masterSeed;
    }

    public int SchemaVersion { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset SavedAtUtc { get; init; }

    public string CareerName { get; }

    public string ManagerName { get; }

    public string PlayerTeamId { get; }

    public DateOnly CurrentGameDate { get; }

    public string WorldDataHash { get; }

    public ulong MasterSeed { get; }
}

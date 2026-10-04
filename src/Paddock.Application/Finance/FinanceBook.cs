using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Finance;

/// <summary>The live world as finance commands see it. The host owns the world; this only reads and puts a section back.</summary>
public sealed class FinanceBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public FinanceBook(Func<WorldState> read, Action<WorldState> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    public WorldState World => _read();

    public FinanceSection Section => World.Section<FinanceSection>(FinanceSection.SectionName) ?? FinanceSection.Empty;

    public static FinanceBook ForSession(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new FinanceBook(() => session.World, session.StoreWorld);
    }

    public void Replace(FinanceSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        _write(World.WithSection(section));
    }

    public static GameDate ToGameDate(DateOnly date) => new(date.Year, date.Month, date.Day);

    public static DateOnly ToDateOnly(GameDate date) => new(date.Year, date.Month, date.Day);
}

public static class FinanceEventTypes
{
    public const string Insolvent = "finance.organization_insolvent";

    public const string BooksOpened = "finance.books_opened";

    public const string RacePosted = "finance.race_posted";

    public const string SeasonPosted = "finance.season_posted";
}

public sealed record BooksOpened(ManagerId ManagerId, DateOnly OccurredOn, string OrganizationId, long OpeningCents) : IDomainEvent
{
    public string TypeId => FinanceEventTypes.BooksOpened;
}

public sealed record RaceMoneyPosted(ManagerId ManagerId, DateOnly OccurredOn, int Season, int Round) : IDomainEvent
{
    public string TypeId => FinanceEventTypes.RacePosted;
}

public sealed record SeasonPopularityUpdated(ManagerId ManagerId, DateOnly OccurredOn, int Season, int PopularityMilli) : IDomainEvent
{
    public string TypeId => FinanceEventTypes.SeasonPosted;
}

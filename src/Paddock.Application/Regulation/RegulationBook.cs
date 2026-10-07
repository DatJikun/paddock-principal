using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Regulation;

/// <summary>The live world as the political life of the series sees it. The host owns the world; this reads sections and puts them back.</summary>
public sealed class RegulationBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public RegulationBook(Func<WorldState> read, Action<WorldState> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    public WorldState World => _read();

    /// <summary>The section, or null while the career has not opened its political life (a historical career never does).</summary>
    public RegulationsSection? Section => World.Section<RegulationsSection>(RegulationsSection.SectionName);

    public FinanceSection Finance => World.Section<FinanceSection>(FinanceSection.SectionName) ?? FinanceSection.Empty;

    public static RegulationBook ForSession(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new RegulationBook(() => session.World, session.StoreWorld);
    }

    public void Write(RegulationsSection regulations, FinanceSection? finance = null)
    {
        ArgumentNullException.ThrowIfNull(regulations);
        var world = World.WithSection(regulations);
        if (finance is not null)
        {
            world = world.WithSection(finance);
        }

        _write(world);
    }
}

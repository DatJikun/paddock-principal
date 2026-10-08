using Paddock.Simulation.Time;

namespace Paddock.Application.Regulation;

/// <summary>
/// One lived day of the political life of every series (#275). It runs after the race weekend (order 50): the table it reads for
/// the public place of a team already holds that day's race. A quiet day writes nothing, and a historical career never registers it.
/// </summary>
public sealed class RegulationDayHandler : IDayHandler
{
    /// <summary>After the race weekend (50) and before negotiations (700).</summary>
    public const int HandlerOrder = 55;

    private readonly RegulationPolitics _politics;

    public RegulationDayHandler(RegulationPolitics politics)
    {
        ArgumentNullException.ThrowIfNull(politics);
        _politics = politics;
    }

    public int Order => HandlerOrder;

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _politics.OnDay(context.Today);
    }
}

using System.Globalization;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Career;

/// <summary>
/// The host's change of season. On 1 January, before any other day handler, it runs the season-change steps the modules
/// registered (<see cref="CareerModuleContext.AddSeasonChange"/>) and emits <see cref="CareerEventType.SeasonChanged"/>. The
/// systems themselves do not decide when a season changes (owner decision, #160): they move their cars and budgets in their step
/// and react to the new season in their daily handler.
/// </summary>
internal sealed class SeasonChangeHandler : IDayHandler
{
    /// <summary>First of the day: nothing is allowed to see the old season on 1 January.</summary>
    public const int HandlerOrder = 5;

    private readonly CareerModuleContext _context;

    public SeasonChangeHandler(CareerModuleContext context) => _context = context;

    public int Order => HandlerOrder;

    public void OnDay(DayContext context)
    {
        if (!context.Today.IsSeasonStart)
        {
            return;
        }

        _context.ChangeSeason(context.Today);
        context.Emit(
            CareerEventType.SeasonChanged,
            new MarkerPayload(context.Today.Year.ToString(CultureInfo.InvariantCulture)));
    }
}

using Paddock.Simulation.Time;

namespace Paddock.Application.Development;

/// <summary>
/// One lived day of car development for every team that has cars, the player's and the AI's alike. It names no manager:
/// a plan is data in the development section, whoever set it. Writes nothing when nothing changed.
/// <para>
/// To wire it, register it with the day-handler registry of the career host. That is the host's job, not this task's:
/// <c>registry.Register(new DevelopmentDayHandler(book, environment))</c>.
/// </para>
/// </summary>
public sealed class DevelopmentDayHandler : IDayHandler
{
    /// <summary>ESTIMATE: after sponsors (750) so the day's income is booked, before the finance review (800) so it sees this spending.</summary>
    public const int DefaultOrder = 780;

    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;

    public DevelopmentDayHandler(DevelopmentBook book, DevelopmentEnvironment environment, int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_book.Cars.Cars.Count == 0)
        {
            return;
        }

        var outcome = DevelopmentEngine.Step(_book.Inputs(context.Today, _environment));
        if (outcome.Changed)
        {
            _book.Write(outcome);
        }
    }
}

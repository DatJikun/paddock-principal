namespace Paddock.Simulation.Time;

/// <summary>
/// A system that runs once per lived day. Lower <see cref="Order"/> runs first.
/// Equal orders keep registration order. The handler must not read the wall clock or create its own random generator.
/// </summary>
public interface IDayHandler
{
    int Order { get; }

    void OnDay(DayContext context);
}

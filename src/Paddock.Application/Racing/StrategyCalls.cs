using System.Globalization;
using Paddock.Domain.Spy;

namespace Paddock.Application.Racing;

/// <summary>What a strategist told one car on the pit wall: come in, push, save, back to normal pace.</summary>
/// <param name="CarId">The car (the lap engine uses the driver id).</param>
/// <param name="Lap">The lap the call was made for, from 1.</param>
/// <param name="Call">One of <see cref="StrategyCalls.Pit"/>, <see cref="StrategyCalls.Push"/>, <see cref="StrategyCalls.Save"/>, <see cref="StrategyCalls.Standard"/>.</param>
/// <param name="Compound">The compound asked for at a stop; null when the tyres stay on or the call is not a stop.</param>
public sealed record StrategyCall(string CarId, int Lap, string Call, string? Compound);

/// <summary>
/// Listens to the strategists' decision traces during a race (TECH §7) and keeps the calls a pit wall would hear on the
/// radio: every stop, and every change of pace. Passive (INV-006): turning it on changes no draw and no decision, the
/// traces are only read. It keeps every car; the read for a manager shows only that manager's own cars (INV-003).
/// </summary>
public sealed class StrategyCalls : ITraceSink
{
    public const string Pit = "pit";
    public const string Push = "push";
    public const string Save = "save";
    public const string Standard = "standard";

    private const string StrategistPrefix = "strategist:";
    private const string TriggerPrefix = "lap_check:";

    private readonly List<StrategyCall> _calls = [];
    private readonly Dictionary<string, string> _pace = new(StringComparer.Ordinal);

    public bool IsEnabled => true;

    public IReadOnlyList<StrategyCall> Calls => _calls;

    public void Record(DecisionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        if (!trace.Who.StartsWith(StrategistPrefix, StringComparison.Ordinal)
            || !TryLap(trace.Trigger, out var carId, out var lap))
        {
            return;
        }

        // Option ids are "stay_out/<pace>" or "pit_now/<compound|keep>/<pace>[/swap]" (RuleBasedStrategist).
        var parts = trace.ChosenOptionId.Split('/');
        if (parts.Length < 2)
        {
            return;
        }

        var pit = parts[0] == "pit_now";
        var pace = pit ? (parts.Length > 2 ? parts[2] : Standard) : parts[1];
        var before = _pace.TryGetValue(carId, out var known) ? known : Standard;
        _pace[carId] = pace;
        if (pit)
        {
            _calls.Add(new StrategyCall(carId, lap, Pit, parts[1] == "keep" ? null : parts[1]));
        }

        if (!string.Equals(before, pace, StringComparison.Ordinal) && pace is Push or Save or Standard)
        {
            _calls.Add(new StrategyCall(carId, lap, pace, null));
        }
    }

    private static bool TryLap(string trigger, out string carId, out int lap)
    {
        // "lap_check:<car>:lap:<n>"; a car id may itself contain ':', so the lap is read from the end.
        carId = "";
        lap = 0;
        if (!trigger.StartsWith(TriggerPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var marker = trigger.LastIndexOf(":lap:", StringComparison.Ordinal);
        if (marker <= TriggerPrefix.Length
            || !int.TryParse(trigger.AsSpan(marker + 5), NumberStyles.None, CultureInfo.InvariantCulture, out lap))
        {
            return false;
        }

        carId = trigger[TriggerPrefix.Length..marker];
        return true;
    }
}

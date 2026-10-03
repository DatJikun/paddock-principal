namespace Paddock.Simulation.Racing.Pace;

/// <summary>The layers of a lap time, in the order they are summed. Positive seconds = slower.</summary>
public enum LapLayer
{
    TrackBase,
    CarFit,
    Driver,
    DriverAffinity,
    Fuel,
    Tyres,
    Traffic,
    DirtyAir,
    Wetness,
    Noise,
}

/// <summary>One layer's contribution in seconds (positive = slower, negative = faster).</summary>
public readonly record struct LapLayerValue(LapLayer Layer, double Seconds);

/// <summary>A lap time and the layers it is made of; for Spy and explanations.</summary>
public sealed record LapTimeBreakdown
{
    public LapTimeBreakdown(IReadOnlyList<LapLayerValue> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        Layers = layers.ToArray();
        var total = 0d;
        foreach (var layer in Layers)
        {
            total += layer.Seconds;
        }

        TotalSeconds = total;
    }

    /// <summary>Layers in summation order; the total is exactly their sum in this order.</summary>
    public IReadOnlyList<LapLayerValue> Layers { get; }

    public double TotalSeconds { get; }

    public double Seconds(LapLayer layer)
    {
        foreach (var value in Layers)
        {
            if (value.Layer == layer)
            {
                return value.Seconds;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(layer), layer, "No such layer in this breakdown.");
    }
}

namespace Paddock.Domain.People;

/// <summary>
/// The eleven driver attributes from DESIGN §6.1, each an integer from 1 to 20.
/// </summary>
public readonly record struct DriverAttributes
{
    public DriverAttributes(
        int cornering,
        int braking,
        int smoothness,
        int overtaking,
        int defending,
        int consistency,
        int composure,
        int adaptability,
        int wetWeather,
        int fitness,
        int feedback)
    {
        Cornering = cornering;
        Braking = braking;
        Smoothness = smoothness;
        Overtaking = overtaking;
        Defending = defending;
        Consistency = consistency;
        Composure = composure;
        Adaptability = adaptability;
        WetWeather = wetWeather;
        Fitness = fitness;
        Feedback = feedback;
        EnsureValid();
    }

    public int Cornering { get; }

    public int Braking { get; }

    public int Smoothness { get; }

    public int Overtaking { get; }

    public int Defending { get; }

    public int Consistency { get; }

    public int Composure { get; }

    public int Adaptability { get; }

    public int WetWeather { get; }

    public int Fitness { get; }

    public int Feedback { get; }

    public int Get(string key)
    {
        return key switch
        {
            "cornering" => Cornering,
            "braking" => Braking,
            "smoothness" => Smoothness,
            "overtaking" => Overtaking,
            "defending" => Defending,
            "consistency" => Consistency,
            "composure" => Composure,
            "adaptability" => Adaptability,
            "wet_weather" => WetWeather,
            "fitness" => Fitness,
            "feedback" => Feedback,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown driver attribute."),
        };
    }

    public void EnsureValid()
    {
        Check(Cornering);
        Check(Braking);
        Check(Smoothness);
        Check(Overtaking);
        Check(Defending);
        Check(Consistency);
        Check(Composure);
        Check(Adaptability);
        Check(WetWeather);
        Check(Fitness);
        Check(Feedback);
    }

    private static void Check(int value)
    {
        if (value < GenerationEstimates.AttributeMin || value > GenerationEstimates.AttributeMax)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Driver attributes must be from 1 to 20.");
        }
    }
}

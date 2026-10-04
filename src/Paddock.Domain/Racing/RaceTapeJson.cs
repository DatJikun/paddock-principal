using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Paddock.Domain.Racing;

/// <summary>
/// Hand-written canonical JSON (Domain has no System.Text.Json dependency). Property order is fixed
/// per kind: seq, lap, raceTime, kind, then the kind's own fields.
/// </summary>
internal static class RaceTapeJson
{
    public static string Write(ImmutableArray<RaceEvent> events)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < events.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            WriteEvent(sb, events[i]);
        }

        return sb.Append(']').ToString();
    }

    private static void WriteEvent(StringBuilder sb, RaceEvent e)
    {
        sb.Append('{');
        Num(sb, "seq", e.Seq, first: true);
        Num(sb, "lap", e.Lap);
        Num(sb, "raceTime", e.RaceTime);
        Str(sb, "kind", e.Kind.ToString());
        switch (e)
        {
            case RaceStarted x:
                Num(sb, "totalLaps", x.TotalLaps);
                Arr(sb, "driverIds", x.DriverIds);
                break;
            case LapCompleted x:
                Str(sb, "driverId", x.DriverId);
                Num(sb, "lapTimeMs", x.LapTimeMs);
                Num(sb, "position", x.Position);
                Num(sb, "gapToLeaderMs", x.GapToLeaderMs);
                break;
            case PitStop x:
                Str(sb, "driverId", x.DriverId);
                Str(sb, "phase", x.Phase.ToString());
                Num(sb, "durationMs", x.DurationMs);
                Str(sb, "tyres", x.Tyres);
                break;
            case PositionChange x:
                Str(sb, "driverId", x.DriverId);
                Num(sb, "fromPosition", x.FromPosition);
                Num(sb, "toPosition", x.ToPosition);
                break;
            case Incident x:
                Arr(sb, "involvedIds", x.InvolvedIds);
                Str(sb, "severity", x.Severity.ToString());
                break;
            case Retirement x:
                Str(sb, "driverId", x.DriverId);
                Str(sb, "reason", x.Reason.ToString());
                break;
            case WeatherChange x:
                Str(sb, "condition", x.Condition);
                break;
            case SafetyCar x:
                Str(sb, "phase", x.Phase.ToString());
                sb.Append(",\"isVirtual\":").Append(x.IsVirtual ? "true" : "false");
                break;
            case FastestLap x:
                Str(sb, "driverId", x.DriverId);
                Num(sb, "lapTimeMs", x.LapTimeMs);
                break;
            case Finished x:
                Str(sb, "driverId", x.DriverId);
                Num(sb, "position", x.Position);
                Num(sb, "lapsCompleted", x.LapsCompleted);
                Num(sb, "totalTimeMs", x.TotalTimeMs);
                break;
            case RedFlag:
            case RaceEnded:
                break;
            default:
                throw new InvalidOperationException($"Unsupported race event {e.GetType().Name}.");
        }

        sb.Append('}');
    }

    private static void Num(StringBuilder sb, string name, long value, bool first = false)
    {
        Name(sb, name, first);
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Str(StringBuilder sb, string name, string value)
    {
        Name(sb, name, first: false);
        Quote(sb, value);
    }

    private static void Arr(StringBuilder sb, string name, ImmutableArray<string> values)
    {
        Name(sb, name, first: false);
        sb.Append('[');
        if (!values.IsDefault)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                Quote(sb, values[i]);
            }
        }

        sb.Append(']');
    }

    private static void Name(StringBuilder sb, string name, bool first)
    {
        if (!first)
        {
            sb.Append(',');
        }

        sb.Append('"').Append(name).Append("\":");
    }

    private static void Quote(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case < ' ' or (char)0x2028 or (char)0x2029:
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }

    public static string WriteDocument(ImmutableArray<RaceEvent> events, ImmutableArray<CarFrame> frames, FrameAccuracy accuracy)
    {
        var sb = new StringBuilder();
        sb.Append("{\"v\":2,\"accuracy\":");
        Quote(sb, accuracy.ToString());
        sb.Append(",\"events\":");
        sb.Append(Write(events));
        sb.Append(",\"frames\":[");
        for (var i = 0; i < frames.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var frame = frames[i];
            sb.Append('{');
            Num(sb, "t", frame.RaceTimeMs, first: true);
            Str(sb, "car", frame.CarId);
            Dbl(sb, "d", frame.DistanceM);
            Dbl(sb, "s", frame.SpeedMps);
            Name(sb, "pit", first: false);
            sb.Append(frame.InPitLane ? "true" : "false");
            Dbl(sb, "pitM", frame.PitDistanceM);
            sb.Append('}');
        }

        return sb.Append("]}").ToString();
    }

    public static (ImmutableArray<RaceEvent> Events, ImmutableArray<CarFrame> Frames, FrameAccuracy? Accuracy) Read(string json)
    {
        var reader = new Reader(json);
        var value = reader.ReadValue();
        reader.End();
        if (value is JArr array)
        {
            return ([.. ReadEvents(array)], [], null);
        }

        if (value is not JObj obj)
        {
            throw new RaceTapeException("A race tape document must be an array or an object.");
        }

        if (Long(obj, "v") != 2)
        {
            throw new RaceTapeException("Unsupported race tape document version.");
        }

        if (!Enum.TryParse<FrameAccuracy>(StrOf(obj, "accuracy"), out var accuracy))
        {
            throw new RaceTapeException("Unknown frame accuracy.");
        }

        var events = ReadEvents(Arr(obj, "events"));
        var frames = ReadFrames(Arr(obj, "frames"));
        return ([.. events], [.. frames], accuracy);
    }

    private static List<RaceEvent> ReadEvents(JArr array)
    {
        var events = new List<RaceEvent>(array.Items.Count);
        foreach (var item in array.Items)
        {
            if (item is not JObj obj)
            {
                throw new RaceTapeException("A race event must be an object.");
            }

            var seq = (int)Long(obj, "seq");
            var lap = (int)Long(obj, "lap");
            var raceTime = Long(obj, "raceTime");
            events.Add(StrOf(obj, "kind") switch
            {
                "RaceStarted" => new RaceStarted(seq, lap, raceTime, (int)Long(obj, "totalLaps"), Strings(obj, "driverIds")),
                "LapCompleted" => new LapCompleted(seq, lap, raceTime, StrOf(obj, "driverId"), Long(obj, "lapTimeMs"), (int)Long(obj, "position"), Long(obj, "gapToLeaderMs")),
                "PitStop" => new PitStop(seq, lap, raceTime, StrOf(obj, "driverId"), Enum.Parse<PitLanePhase>(StrOf(obj, "phase")), Long(obj, "durationMs"), StrOf(obj, "tyres")),
                "PositionChange" => new PositionChange(seq, lap, raceTime, StrOf(obj, "driverId"), (int)Long(obj, "fromPosition"), (int)Long(obj, "toPosition")),
                "Incident" => new Incident(seq, lap, raceTime, Strings(obj, "involvedIds"), Enum.Parse<IncidentSeverity>(StrOf(obj, "severity"))),
                "Retirement" => new Retirement(seq, lap, raceTime, StrOf(obj, "driverId"), Enum.Parse<RetirementReason>(StrOf(obj, "reason"))),
                "WeatherChange" => new WeatherChange(seq, lap, raceTime, StrOf(obj, "condition")),
                "SafetyCar" => new SafetyCar(seq, lap, raceTime, Enum.Parse<SafetyCarPhase>(StrOf(obj, "phase")), Bool(obj, "isVirtual")),
                "RedFlag" => new RedFlag(seq, lap, raceTime),
                "FastestLap" => new FastestLap(seq, lap, raceTime, StrOf(obj, "driverId"), Long(obj, "lapTimeMs")),
                "Finished" => new Finished(seq, lap, raceTime, StrOf(obj, "driverId"), (int)Long(obj, "position"), (int)Long(obj, "lapsCompleted"), Long(obj, "totalTimeMs")),
                "RaceEnded" => new RaceEnded(seq, lap, raceTime),
                var kind => throw new RaceTapeException("Unknown race event kind '" + kind + "'."),
            });
        }

        return events;
    }

    private static List<CarFrame> ReadFrames(JArr array)
    {
        var frames = new List<CarFrame>(array.Items.Count);
        foreach (var item in array.Items)
        {
            if (item is not JObj obj)
            {
                throw new RaceTapeException("A position frame must be an object.");
            }

            frames.Add(new CarFrame(Long(obj, "t"), StrOf(obj, "car"), Dbl(obj, "d"), Dbl(obj, "s"), Bool(obj, "pit"), Dbl(obj, "pitM")));
        }

        return frames;
    }

    private static long Long(JObj obj, string name) => obj.Get(name) switch
    {
        JLong n => n.Value,
        JDouble n when n.Value == Math.Round(n.Value) => (long)n.Value,
        _ => throw new RaceTapeException("Expected an integer '" + name + "'."),
    };

    private static double Dbl(JObj obj, string name) => obj.Get(name) switch
    {
        JDouble n => n.Value,
        JLong n => n.Value,
        _ => throw new RaceTapeException("Expected a number '" + name + "'."),
    };

    private static string StrOf(JObj obj, string name) =>
        obj.Get(name) as JStr is { } text ? text.Value : throw new RaceTapeException("Expected a string '" + name + "'.");

    private static bool Bool(JObj obj, string name) =>
        obj.Get(name) as JBool is { } flag ? flag.Value : throw new RaceTapeException("Expected a boolean '" + name + "'.");

    private static JArr Arr(JObj obj, string name) =>
        obj.Get(name) as JArr ?? throw new RaceTapeException("Expected an array '" + name + "'.");

    private static ImmutableArray<string> Strings(JObj obj, string name)
    {
        var array = Arr(obj, name);
        var values = new string[array.Items.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = array.Items[i] as JStr is { } text ? text.Value : throw new RaceTapeException("Expected a string in '" + name + "'.");
        }

        return [.. values];
    }

    private static void Dbl(StringBuilder sb, string name, double value)
    {
        Name(sb, name, first: false);
        sb.Append(value.ToString("G17", CultureInfo.InvariantCulture));
    }

    private abstract record J;
    private sealed record JLong(long Value) : J;
    private sealed record JDouble(double Value) : J;
    private sealed record JStr(string Value) : J;
    private sealed record JBool(bool Value) : J;
    private sealed record JArr(List<J> Items) : J;
    private sealed record JObj(Dictionary<string, J> Items) : J
    {
        public J Get(string name) =>
            Items.TryGetValue(name, out var value) ? value : throw new RaceTapeException("Missing '" + name + "'.");
    }

    private sealed class Reader(string text)
    {
        private int _i;

        public void End()
        {
            if (_i != text.Length)
            {
                throw new RaceTapeException("Unexpected trailing data in a race tape.");
            }
        }

        public J ReadValue() => Peek() switch
        {
            '{' => ReadObject(),
            '[' => ReadArray(),
            '"' => new JStr(ReadString()),
            't' => ReadLiteral("true", new JBool(true)),
            'f' => ReadLiteral("false", new JBool(false)),
            '-' or >= '0' and <= '9' => ReadNumber(),
            _ => throw new RaceTapeException("Invalid race tape JSON."),
        };

        private J ReadLiteral(string word, J value)
        {
            for (var i = 0; i < word.Length; i++)
            {
                if (Take() != word[i])
                {
                    throw new RaceTapeException("Invalid race tape JSON.");
                }
            }

            return value;
        }

        private JObj ReadObject()
        {
            Expect('{');
            var items = new Dictionary<string, J>(StringComparer.Ordinal);
            if (Peek() == '}')
            {
                Take();
                return new JObj(items);
            }

            while (true)
            {
                var name = ReadString();
                Expect(':');
                items[name] = ReadValue();
                if (Peek() == '}')
                {
                    Take();
                    return new JObj(items);
                }

                Expect(',');
            }
        }

        private JArr ReadArray()
        {
            Expect('[');
            var items = new List<J>();
            if (Peek() == ']')
            {
                Take();
                return new JArr(items);
            }

            while (true)
            {
                items.Add(ReadValue());
                if (Peek() == ']')
                {
                    Take();
                    return new JArr(items);
                }

                Expect(',');
            }
        }

        private J ReadNumber()
        {
            var start = _i;
            if (Peek() == '-')
            {
                Take();
            }

            while (Peek() is >= '0' and <= '9')
            {
                Take();
            }

            var real = false;
            if (Peek() == '.')
            {
                real = true;
                Take();
                while (Peek() is >= '0' and <= '9')
                {
                    Take();
                }
            }

            if (Peek() is 'e' or 'E')
            {
                real = true;
                Take();
                if (Peek() is '+' or '-')
                {
                    Take();
                }

                while (Peek() is >= '0' and <= '9')
                {
                    Take();
                }
            }

            var raw = text[start.._i];
            return real
                ? new JDouble(double.Parse(raw, CultureInfo.InvariantCulture))
                : new JLong(long.Parse(raw, CultureInfo.InvariantCulture));
        }

        private string ReadString()
        {
            Expect('"');
            var sb = new StringBuilder();
            while (true)
            {
                var c = Take();
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                sb.Append(Take() switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    'u' => (char)int.Parse(new string([Take(), Take(), Take(), Take()]), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    _ => throw new RaceTapeException("Invalid escape in a race tape."),
                });
            }
        }

        private char Peek() => _i >= text.Length ? '\0' : text[_i];

        private char Take()
        {
            if (_i >= text.Length)
            {
                throw new RaceTapeException("Unexpected end of a race tape.");
            }

            return text[_i++];
        }

        private void Expect(char c)
        {
            if (Take() != c)
            {
                throw new RaceTapeException("Invalid race tape JSON.");
            }
        }
    }
}

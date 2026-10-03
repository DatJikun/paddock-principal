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
}

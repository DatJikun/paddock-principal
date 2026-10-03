using System.Text;
using System.Text.Json;
using Paddock.Domain.Spy;

namespace Paddock.Application.Spy;

/// <summary>Appends every trace as one JSON line (developer dump; includes truth context).</summary>
public sealed class FileSink : ITraceSink
{
    private readonly string _path;

    public FileSink(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public bool IsEnabled => true;

    public void Record(DecisionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        File.AppendAllText(_path, TraceJson.Serialize(trace) + "\n", new UTF8Encoding(false));
    }
}

public static class TraceJson
{
    public static string Serialize(DecisionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        return JsonSerializer.Serialize(trace);
    }
}

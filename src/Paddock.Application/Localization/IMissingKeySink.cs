namespace Paddock.Application.Localization;

public enum MissingKeyKind
{
    /// <summary>The key is absent in the requested language, the English text was used.</summary>
    FallbackToEnglish,

    /// <summary>The key is absent everywhere, the key itself was returned.</summary>
    MissingEverywhere,

    /// <summary>The text uses a placeholder for which no argument was supplied.</summary>
    MissingArgument,

    /// <summary>A plural entry lacks the needed category form, the <c>other</c> form was used.</summary>
    MissingPluralForm,
}

public sealed record MissingKeyReport(MissingKeyKind Kind, string Key, Language Language, string? Detail = null);

/// <summary>Receives every fallback the localizer performs. Fallbacks are never silent.</summary>
public interface IMissingKeySink
{
    void Report(MissingKeyReport report);
}

/// <summary>Sink that keeps all reports in memory.</summary>
public sealed class CollectingMissingKeySink : IMissingKeySink
{
    private readonly List<MissingKeyReport> _reports = [];

    public IReadOnlyList<MissingKeyReport> Reports => _reports;

    public void Report(MissingKeyReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _reports.Add(report);
    }
}

using System.Globalization;
using System.Text.Json;

namespace Paddock.DataPipeline;

public static class FetchCommand
{
    // A 404 is cached as an empty MRData page so a later run skips it.
    // Jolpica usually returns HTTP 200 with total 0 instead (qualifying before 1994, sprints before 2021,
    // constructor standings before 1958). Both shapes mean "no rows".
    private const string EmptyPageJson = """{"MRData":{"limit":"0","offset":"0","total":"0"}}""";
    private const int MaxTransportAttempts = 8;
    private const int MaxThrottleAttempts = 8;

    public static async Task<int> ExecuteAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        IJolpicaHttp http,
        IJolpicaPacer pacer,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(pacer);
        ArgumentNullException.ThrowIfNull(delay);

        if (!CommandArgs.TryParse(args, start: 1, ["--from", "--to", "--cache", "--force"], stderr, out var options, out var force))
        {
            return 1;
        }

        if (!TryYear(options, "--from", stderr, out var from) || !TryYear(options, "--to", stderr, out var to))
        {
            return 1;
        }

        if (from > to)
        {
            stderr.WriteLine("--from must not be greater than --to.");
            return 1;
        }

        var cacheRoot = CacheRoot(options);
        var rawRoot = JolpicaCache.Raw(cacheRoot);
        stdout.WriteLine($"fetch {from.ToString(CultureInfo.InvariantCulture)}-{to.ToString(CultureInfo.InvariantCulture)}");

        // Drivers, constructors, circuits and the season index are global. A result in the requested
        // year range can reference any of them. Only the season-scoped endpoints honor --from/--to.
        await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, "seasons", "seasons", force, cancellationToken);
        await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, "circuits", "circuits", force, cancellationToken);
        await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, "drivers", "drivers", force, cancellationToken);
        await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, "constructors", "constructors", force, cancellationToken);

        for (var year = from; year <= to; year++)
        {
            var yearText = year.ToString(CultureInfo.InvariantCulture);
            await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, yearText, Path.Combine("races", yearText), force, cancellationToken);
            await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, $"{yearText}/results", Path.Combine("results", yearText), force, cancellationToken);
            await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, $"{yearText}/qualifying", Path.Combine("qualifying", yearText), force, cancellationToken);
            await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, $"{yearText}/sprint", Path.Combine("sprint", yearText), force, cancellationToken);
            await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, $"{yearText}/driverStandings", Path.Combine("driver-standings", yearText), force, cancellationToken);
            await FetchPagesAsync(http, pacer, delay, stdout, rawRoot, $"{yearText}/constructorStandings", Path.Combine("constructor-standings", yearText), force, cancellationToken);
        }

        return 0;
    }

    private static async Task FetchPagesAsync(
        IJolpicaHttp http,
        IJolpicaPacer pacer,
        Func<TimeSpan, CancellationToken, Task> delay,
        TextWriter stdout,
        string rawRoot,
        string resourcePath,
        string cacheSubdir,
        bool force,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        for (var guard = 0; guard < 10000; guard++)
        {
            var fullPath = Path.Combine(rawRoot, cacheSubdir, $"offset-{offset.ToString(CultureInfo.InvariantCulture)}.json");
            string body;
            if (!force && File.Exists(fullPath))
            {
                body = File.ReadAllText(fullPath);
                stdout.WriteLine($"cached {resourcePath} offset {offset.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                var relativeUrl = $"{resourcePath}.json?limit={JolpicaCache.PageLimit.ToString(CultureInfo.InvariantCulture)}&offset={offset.ToString(CultureInfo.InvariantCulture)}";
                body = await DownloadAsync(http, pacer, delay, stdout, relativeUrl, cancellationToken);
                JolpicaCache.WriteAtomic(fullPath, body);
                stdout.WriteLine($"fetched {relativeUrl}");
            }

            var page = ReadPage(body, fullPath);
            if (page.Limit <= 0 || page.Offset + page.Limit >= page.Total)
            {
                return;
            }

            var next = page.Offset + page.Limit;
            if (next <= offset)
            {
                throw new InvalidDataException($"Pagination for {resourcePath} did not advance at offset {offset.ToString(CultureInfo.InvariantCulture)}.");
            }

            offset = next;
        }

        throw new InvalidDataException($"Too many pages for {resourcePath}.");
    }

    private static async Task<string> DownloadAsync(
        IJolpicaHttp http,
        IJolpicaPacer pacer,
        Func<TimeSpan, CancellationToken, Task> delay,
        TextWriter stdout,
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        var transportAttempts = 0;
        var throttleAttempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await pacer.WaitTurnAsync(cancellationToken);

            JolpicaHttpResponse response;
            try
            {
                response = await http.GetAsync(relativeUrl, cancellationToken);
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken) && transportAttempts + 1 < MaxTransportAttempts)
            {
                transportAttempts++;
                stdout.WriteLine($"retry transport {relativeUrl} attempt {transportAttempts.ToString(CultureInfo.InvariantCulture)}");
                await delay(TimeSpan.FromSeconds(Math.Min(60, 1 << transportAttempts)), cancellationToken);
                continue;
            }
            catch (Exception ex) when (IsRetryable(ex, cancellationToken))
            {
                throw new InvalidDataException($"Failed to fetch {relativeUrl}: {ex.Message}", ex);
            }

            if (response.StatusCode == 429)
            {
                throttleAttempts++;
                if (throttleAttempts >= MaxThrottleAttempts)
                {
                    throw new InvalidDataException($"Gave up after HTTP 429 for {relativeUrl}.");
                }

                var backoff = response.RetryAfter ?? TimeSpan.FromSeconds(Math.Min(60, 1 << throttleAttempts));
                if (backoff < TimeSpan.Zero)
                {
                    backoff = TimeSpan.Zero;
                }

                stdout.WriteLine($"retry 429 {relativeUrl} attempt {throttleAttempts.ToString(CultureInfo.InvariantCulture)}");
                await delay(backoff, cancellationToken);
                continue;
            }

            if (response.StatusCode == 404)
            {
                return EmptyPageJson;
            }

            if (response.StatusCode >= 500)
            {
                transportAttempts++;
                if (transportAttempts >= MaxTransportAttempts)
                {
                    throw new InvalidDataException($"HTTP {response.StatusCode.ToString(CultureInfo.InvariantCulture)} for {relativeUrl}.");
                }

                stdout.WriteLine($"retry http {response.StatusCode.ToString(CultureInfo.InvariantCulture)} {relativeUrl} attempt {transportAttempts.ToString(CultureInfo.InvariantCulture)}");
                await delay(TimeSpan.FromSeconds(Math.Min(60, 1 << transportAttempts)), cancellationToken);
                continue;
            }

            if (response.StatusCode != 200)
            {
                throw new InvalidDataException($"HTTP {response.StatusCode.ToString(CultureInfo.InvariantCulture)} for {relativeUrl}.");
            }

            try
            {
                ReadPage(response.Body, relativeUrl);
            }
            catch (JsonException) when (transportAttempts + 1 < MaxTransportAttempts)
            {
                transportAttempts++;
                stdout.WriteLine($"retry json {relativeUrl} attempt {transportAttempts.ToString(CultureInfo.InvariantCulture)}");
                await delay(TimeSpan.FromSeconds(Math.Min(60, 1 << transportAttempts)), cancellationToken);
                continue;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Response for {relativeUrl} was not Jolpica JSON.", ex);
            }

            return response.Body;
        }
    }

    // HttpClient turns its own timeout into TaskCanceledException. That is not a caller cancel.
    private static bool IsRetryable(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is HttpRequestException)
        {
            return true;
        }

        return exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;
    }

    private static PageInfo ReadPage(string json, string context)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("MRData", out var mr))
            {
                throw new JsonException("missing MRData");
            }

            return new PageInfo(ReadJsonInt(mr, "limit"), ReadJsonInt(mr, "offset"), ReadJsonInt(mr, "total"));
        }
        catch (JsonException ex)
        {
            throw new JsonException($"{context} is not a Jolpica page: {ex.Message}", ex);
        }
    }

    private static int ReadJsonInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            throw new JsonException($"missing {name}");
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => int.Parse(value.GetString()!, CultureInfo.InvariantCulture),
            JsonValueKind.Number => value.GetInt32(),
            _ => throw new JsonException($"non-numeric {name}"),
        };
    }

    private static bool TryYear(Dictionary<string, string> options, string flag, TextWriter stderr, out int year)
    {
        if (!options.TryGetValue(flag, out var text)
            || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            stderr.WriteLine($"Invalid {flag} value: {text}");
            year = 0;
            return false;
        }

        return true;
    }

    private static string CacheRoot(Dictionary<string, string> options)
    {
        return options.TryGetValue("--cache", out var cache) ? cache : JolpicaCache.DefaultRoot();
    }

    private readonly record struct PageInfo(int Limit, int Offset, int Total);
}

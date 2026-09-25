using System.Globalization;
using System.Net;
using Paddock.DataPipeline;

namespace Paddock.Tests.DataPipeline;

public class JolpicaFetchTests
{
    [Fact]
    public async Task FetchPaginatesResumesAndForceRedownloadsWithoutNetwork()
    {
        var cache = TempCache();
        try
        {
            var http = new ScriptedHttp(EmptyOrDrivers);
            var pacer = new ImmediatePacer();
            var stderr = new StringWriter();
            var first = await PipelineCommands.ExecuteAsync(
                ["fetch", "--from", "1950", "--to", "1950", "--cache", cache],
                new StringWriter(),
                stderr,
                http,
                pacer,
                NoDelay);

            Assert.Equal(0, first);
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.Equal(11, http.Urls.Count);
            Assert.Contains("drivers.json?limit=100&offset=0", http.Urls);
            Assert.Contains("drivers.json?limit=100&offset=100", http.Urls);
            Assert.DoesNotContain(http.Urls, url => url.Contains("offset=200", StringComparison.Ordinal));
            Assert.Equal(http.Urls.Count, pacer.Turns);
            Assert.True(File.Exists(Path.Combine(cache, "raw", "results", "1950", "offset-0.json")));
            Assert.True(File.Exists(Path.Combine(cache, "raw", "driver-standings", "1950", "offset-0.json")));

            var cached = http.Urls.Count;
            var second = await PipelineCommands.ExecuteAsync(
                ["fetch", "--from", "1950", "--to", "1950", "--cache", cache],
                new StringWriter(),
                stderr,
                http,
                pacer,
                NoDelay);
            Assert.Equal(0, second);
            Assert.Equal(cached, http.Urls.Count);
            Assert.Equal(cached, pacer.Turns);

            var forced = await PipelineCommands.ExecuteAsync(
                ["fetch", "--from", "1950", "--to", "1950", "--cache", cache, "--force"],
                new StringWriter(),
                stderr,
                http,
                pacer,
                NoDelay);
            Assert.Equal(0, forced);
            Assert.Equal(cached * 2, http.Urls.Count);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task FetchContinuesAPartialDriverCache()
    {
        var cache = TempCache();
        try
        {
            var page = Page("drivers", offset: 0, total: 150);
            var path = Path.Combine(cache, "raw", "drivers", "offset-0.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, page);

            var http = new ScriptedHttp(EmptyOrDrivers);
            var code = await PipelineCommands.ExecuteAsync(
                ["fetch", "--from", "1950", "--to", "1950", "--cache", cache],
                new StringWriter(),
                new StringWriter(),
                http,
                new ImmediatePacer(),
                NoDelay);

            Assert.Equal(0, code);
            Assert.DoesNotContain(http.Urls, url => url.StartsWith("drivers.json?limit=100&offset=0", StringComparison.Ordinal));
            Assert.Contains("drivers.json?limit=100&offset=100", http.Urls);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task FetchRetries429AndCaches404AsAnEmptyPage()
    {
        var cache = TempCache();
        try
        {
            var delays = new List<TimeSpan>();
            var http = new ScriptedHttp((url, seen) =>
            {
                if (url.StartsWith("seasons.json", StringComparison.Ordinal))
                {
                    return new JolpicaHttpResponse(404, "missing", null);
                }

                if (url.StartsWith("circuits.json", StringComparison.Ordinal) && seen == 0)
                {
                    return new JolpicaHttpResponse(429, "throttled", TimeSpan.FromMilliseconds(5));
                }

                return new JolpicaHttpResponse(200, EmptyBody(url), null);
            });

            var stderr = new StringWriter();
            var stdout = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["fetch", "--from", "1950", "--to", "1950", "--cache", cache],
                stdout,
                stderr,
                http,
                new ImmediatePacer(),
                (delay, _) =>
                {
                    delays.Add(delay);
                    return Task.CompletedTask;
                });

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.Contains(TimeSpan.FromMilliseconds(5), delays);
            Assert.Contains("retry 429 circuits.json?limit=100&offset=0 attempt 1", JolpicaFixtures.Lines(stdout));
            var seasons = File.ReadAllText(Path.Combine(cache, "raw", "seasons", "offset-0.json"));
            Assert.Contains("\"total\":\"0\"", seasons, StringComparison.Ordinal);

            var calls = http.Urls.Count(url => url.StartsWith("seasons.json", StringComparison.Ordinal));
            var again = await PipelineCommands.ExecuteAsync(
                ["fetch", "--from", "1950", "--to", "1950", "--cache", cache],
                new StringWriter(),
                stderr,
                http,
                new ImmediatePacer(),
                NoDelay);
            Assert.Equal(0, again);
            Assert.Equal(calls, http.Urls.Count(url => url.StartsWith("seasons.json", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task FetchRetriesAnHttpClientTimeout()
    {
        var cache = TempCache();
        try
        {
            var timeouts = 0;
            var http = new ScriptedHttp((url, _) =>
            {
                if (url.StartsWith("seasons.json", StringComparison.Ordinal) && timeouts == 0)
                {
                    timeouts++;
                    throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 60 seconds elapsing.");
                }

                return new JolpicaHttpResponse(200, EmptyBody(url), null);
            });

            var stderr = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["fetch", "--from", "1950", "--to", "1950", "--cache", cache],
                new StringWriter(),
                stderr,
                http,
                new ImmediatePacer(),
                NoDelay);

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.Equal(1, timeouts);
            Assert.True(File.Exists(Path.Combine(cache, "raw", "seasons", "offset-0.json")));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task FetchRejectsANonJsonSuccessAfterRetries()
    {
        var cache = TempCache();
        try
        {
            var http = new ScriptedHttp((url, _) =>
            {
                if (url.StartsWith("constructors.json", StringComparison.Ordinal))
                {
                    return new JolpicaHttpResponse(200, "<html>nope</html>", null);
                }

                return new JolpicaHttpResponse(200, EmptyBody(url), null);
            });

            var stderr = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["fetch", "--from", "1950", "--to", "1950", "--cache", cache],
                new StringWriter(),
                stderr,
                http,
                new ImmediatePacer(),
                NoDelay);

            Assert.Equal(1, code);
            Assert.Contains("constructors.json", stderr.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(cache, "raw", "constructors", "offset-0.json")));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidFetches))]
    public async Task InvalidFetchArgumentsDoNotCallHttp(string[] args)
    {
        var http = new ScriptedHttp((_, _) => throw new InvalidOperationException("HTTP should not be called."));
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await PipelineCommands.ExecuteAsync(args, stdout, stderr, http, new ImmediatePacer(), NoDelay);
        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.False(string.IsNullOrWhiteSpace(stderr.ToString()));
        Assert.Empty(http.Urls);
    }

    public static IEnumerable<object[]> InvalidFetches()
    {
        yield return [Array.Empty<string>()];
        yield return [new[] { "fetch" }];
        yield return [new[] { "fetch", "--from", "1951", "--to", "1950" }];
        yield return [new[] { "fetch", "--from", "nope", "--to", "1950" }];
        yield return [new[] { "fetch", "--from", "1950", "--to", "1950", "--extra" }];
    }

    [Fact]
    public async Task ClientSendsTheRequiredUserAgent()
    {
        var handler = new CaptureHandler();
        using var client = new HttpJolpicaClient(handler);
        var response = await client.GetAsync("seasons.json?limit=1&offset=0", CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(handler.Request);
        Assert.Equal(new Uri("https://api.jolpi.ca/ergast/f1/seasons.json?limit=1&offset=0"), handler.Request.RequestUri);
        Assert.Contains("PaddockPrincipal/0.1", handler.Request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void JolpicaPaceStaysUnderPublishedLimits()
    {
        Assert.True(SlidingWindowPacer.JolpicaMinInterval >= TimeSpan.FromSeconds(1));
        var perHourAtMinInterval = TimeSpan.FromHours(1) / SlidingWindowPacer.JolpicaMinInterval;
        Assert.True(perHourAtMinInterval <= SlidingWindowPacer.JolpicaMaxRequestsPerHour);
        Assert.True(SlidingWindowPacer.JolpicaMaxRequestsPerHour < 500);
        Assert.True(SlidingWindowPacer.JolpicaMaxRequestsPerHour > 0);
    }

    [Fact]
    public async Task PacerSpacesRequestsAndHonorsTheHourlyCap()
    {
        var time = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var pacer = new SlidingWindowPacer(
            TimeSpan.FromSeconds(1),
            maxPerHour: 3,
            () => time,
            (delay, _) =>
            {
                time += delay;
                return Task.CompletedTask;
            });

        var stamps = new List<DateTimeOffset>();
        for (var i = 0; i < 4; i++)
        {
            await pacer.WaitTurnAsync(CancellationToken.None);
            stamps.Add(time);
        }

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), stamps[0]);
        Assert.Equal(stamps[0].AddSeconds(1), stamps[1]);
        Assert.Equal(stamps[0].AddSeconds(2), stamps[2]);
        Assert.Equal(stamps[0].AddHours(1), stamps[3]);
    }

    private static Task NoDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static string TempCache()
    {
        return Path.Combine(Path.GetTempPath(), "paddock-jolpica-" + Guid.NewGuid().ToString("N"));
    }

    private static JolpicaHttpResponse EmptyOrDrivers(string url, int seen)
    {
        if (url.StartsWith("drivers.json", StringComparison.Ordinal))
        {
            return new JolpicaHttpResponse(200, Page("drivers", OffsetOf(url), total: 150), null);
        }

        return new JolpicaHttpResponse(200, EmptyBody(url), null);
    }

    private static string EmptyBody(string url)
    {
        return Page("empty", OffsetOf(url), total: 0);
    }

    private static string Page(string kind, int offset, int total)
    {
        var offsetText = offset.ToString(CultureInfo.InvariantCulture);
        var totalText = total.ToString(CultureInfo.InvariantCulture);
        var table = kind == "drivers"
            ? ",\"DriverTable\":{\"Drivers\":[]}"
            : string.Empty;
        return "{\"MRData\":{\"limit\":\"100\",\"offset\":\"" + offsetText + "\",\"total\":\"" + totalText + "\"" + table + "}}";
    }

    private static int OffsetOf(string relativeUrl)
    {
        var query = relativeUrl.Split('?', 2)[1];
        foreach (var part in query.Split('&'))
        {
            if (part.StartsWith("offset=", StringComparison.Ordinal))
            {
                return int.Parse(part["offset=".Length..], CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException(relativeUrl);
    }

    private sealed class ImmediatePacer : IJolpicaPacer
    {
        public int Turns { get; private set; }

        public Task WaitTurnAsync(CancellationToken cancellationToken)
        {
            Turns++;
            return Task.CompletedTask;
        }
    }

    private sealed class ScriptedHttp : IJolpicaHttp
    {
        private readonly Func<string, int, JolpicaHttpResponse> _respond;
        private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);

        public ScriptedHttp(Func<string, int, JolpicaHttpResponse> respond)
        {
            _respond = respond;
        }

        public List<string> Urls { get; } = new();

        public Task<JolpicaHttpResponse> GetAsync(string relativeUrl, CancellationToken cancellationToken)
        {
            _seen.TryGetValue(relativeUrl, out var seen);
            _seen[relativeUrl] = seen + 1;
            Urls.Add(relativeUrl);
            return Task.FromResult(_respond(relativeUrl, seen));
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"MRData":{"limit":"0","offset":"0","total":"0"}}"""),
            };
            return Task.FromResult(response);
        }
    }
}

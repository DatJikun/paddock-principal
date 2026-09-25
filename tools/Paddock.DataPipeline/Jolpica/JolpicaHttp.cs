namespace Paddock.DataPipeline;

public interface IJolpicaHttp
{
    Task<JolpicaHttpResponse> GetAsync(string relativeUrl, CancellationToken cancellationToken);
}

public sealed record JolpicaHttpResponse(int StatusCode, string Body, TimeSpan? RetryAfter);

/// <summary>
/// Jolpica-F1 Ergast-compatible API (<c>https://api.jolpi.ca/ergast/f1/</c>).
/// The data license is CC BY-NC-SA 4.0 (jolpica-f1 TERMS.md). Ergast's own terms were
/// non-commercial as well. Raw responses stay in <c>data/cache</c>, which is gitignored (TECH §6.1).
/// Jolpica requires a custom User-Agent.
/// </summary>
public sealed class HttpJolpicaClient : IJolpicaHttp, IDisposable
{
    public const string BaseUrl = "https://api.jolpi.ca/ergast/f1/";
    public const string UserAgent = "PaddockPrincipal/0.1 (+https://github.com/DatJikun/paddock-principal)";

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public HttpJolpicaClient()
        : this(CreateClient(), ownsClient: true)
    {
    }

    public HttpJolpicaClient(HttpMessageHandler handler)
        : this(CreateClient(handler), ownsClient: true)
    {
    }

    private HttpJolpicaClient(HttpClient http, bool ownsClient)
    {
        _http = http;
        _ownsClient = ownsClient;
    }

    public async Task<JolpicaHttpResponse> GetAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativeUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        return new JolpicaHttpResponse((int)response.StatusCode, body, retryAfter);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    private static HttpClient CreateClient(HttpMessageHandler? handler = null)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        http.BaseAddress = new Uri(BaseUrl);
        http.Timeout = TimeSpan.FromSeconds(60);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return http;
    }
}

public interface IJolpicaPacer
{
    Task WaitTurnAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Jolpica's published unauthenticated limits are 4 requests/second burst and 500 requests/hour
/// sustained (docs/rate_limits.md). A steady 1 request/second respects the burst and still
/// exceeds the hourly cap, so the default pace is one request every 8 seconds (450/hour).
/// The rolling hourly cap is a second guard. HTTP 429 is handled by the fetcher, on top of this pace.
/// </summary>
public sealed class SlidingWindowPacer : IJolpicaPacer
{
    public const int JolpicaMaxRequestsPerHour = 450;
    public static readonly TimeSpan JolpicaMinInterval = TimeSpan.FromSeconds(8);

    private readonly TimeSpan _minInterval;
    private readonly int _maxPerHour;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Queue<DateTimeOffset> _stamps = new();
    private DateTimeOffset _last = DateTimeOffset.MinValue;

    public SlidingWindowPacer(
        TimeSpan minInterval,
        int maxPerHour,
        Func<DateTimeOffset> clock,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        if (minInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minInterval));
        }

        if (maxPerHour <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPerHour));
        }

        _minInterval = minInterval;
        _maxPerHour = maxPerHour;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public static SlidingWindowPacer ForJolpica()
    {
        return new SlidingWindowPacer(
            JolpicaMinInterval,
            JolpicaMaxRequestsPerHour,
            () => DateTimeOffset.UtcNow,
            Task.Delay);
    }

    public async Task WaitTurnAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var now = _clock();
            while (_stamps.Count > 0 && now - _stamps.Peek() >= TimeSpan.FromHours(1))
            {
                _stamps.Dequeue();
            }

            var wait = TimeSpan.Zero;
            if (_last != DateTimeOffset.MinValue)
            {
                var gap = _minInterval - (now - _last);
                if (gap > wait)
                {
                    wait = gap;
                }
            }

            if (_stamps.Count >= _maxPerHour)
            {
                var untilWindowSlides = _stamps.Peek() + TimeSpan.FromHours(1) - now;
                if (untilWindowSlides > wait)
                {
                    wait = untilWindowSlides;
                }
            }

            if (wait <= TimeSpan.Zero)
            {
                _last = now;
                _stamps.Enqueue(now);
                return;
            }

            await _delay(wait, cancellationToken);
        }
    }
}

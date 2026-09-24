using System.Text.Json;
using iptv.Domain.Collections;

namespace iptv.Services._IptvProvider.Fetchers;

/// <summary>
/// Shared HTTP plumbing for fetchers: header/ApiKey application, per-request timeout, 3-attempt
/// retry with backoff, and an optional fallback base URL/mirror. Streams response bodies so large
/// files are never materialised twice.
/// </summary>
public abstract class ProviderFetcherBase(IHttpClientFactory httpClientFactory)
{
    private const int MaxAttempts = 3;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected HttpClient CreateClient(IptvProviders provider)
    {
        var client = httpClientFactory.CreateClient("IptvProvider");

        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", provider.ApiKey);

        if (provider.Headers is { Count: > 0 })
            foreach (var (key, value) in provider.Headers)
                if (!string.IsNullOrWhiteSpace(key))
                    client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);

        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        return client;
    }

    /// <summary>Resolves an endpoint against a base url. Absolute endpoints are used as-is.</summary>
    protected static string ResolveUrl(string baseUrl, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return baseUrl;

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        if (!string.IsNullOrWhiteSpace(baseUrl) &&
            Uri.TryCreate(baseUrl, UriKind.Absolute, out var b))
            return new Uri(b, endpoint).ToString();

        return endpoint;
    }

    private static IReadOnlyList<string> CandidateUrls(IptvProviders provider, string endpoint)
    {
        var urls = new List<string> { ResolveUrl(provider.BaseUrl, endpoint) };

        if (!string.IsNullOrWhiteSpace(provider.FallbackBaseUrl))
        {
            var fallback = ResolveUrl(provider.FallbackBaseUrl, endpoint);
            if (!urls.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                urls.Add(fallback);
        }

        return urls;
    }

    /// <summary>
    /// GETs the endpoint (trying the fallback base url too), retrying with backoff. Returns a
    /// success response whose body the caller must read and dispose.
    /// </summary>
    protected async Task<HttpResponseMessage> SendAsync(
        HttpClient client, IptvProviders provider, string endpoint, CancellationToken cancellationToken)
    {
        var candidates = CandidateUrls(provider, endpoint);
        var timeoutSeconds = provider.FetchTimeoutSeconds > 0 ? provider.FetchTimeoutSeconds : 60;

        Exception lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            foreach (var url in candidates)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                    var response = await client.GetAsync(
                        url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                    response.EnsureSuccessStatusCode();
                    return response;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (attempt < MaxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 5), cancellationToken);
        }

        throw lastError ?? new HttpRequestException($"Failed to fetch '{endpoint}'.");
    }

    protected async Task<T> FetchJsonAsync<T>(
        HttpClient client, IptvProviders provider, string endpoint, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, provider, endpoint, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    /// <summary>Streams the response body line by line (used by the M3U parser to avoid double buffering).</summary>
    protected async IAsyncEnumerable<string> FetchLinesAsync(
        HttpClient client, IptvProviders provider, string endpoint,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, provider, endpoint, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
            yield return line;
    }
}

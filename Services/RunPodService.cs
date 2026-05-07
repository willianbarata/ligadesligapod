using System.Net.Http.Headers;
using LigaDesligaPod.Services.Options;
using Microsoft.Extensions.Options;

namespace LigaDesligaPod.Services;

public sealed class RunPodService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RunPodOptions _options;

    public RunPodService(IHttpClientFactory httpClientFactory, IOptions<RunPodOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public string GetComfyUiBaseUrl() => $"https://{_options.PodId}-{_options.ComfyPort}.proxy.runpod.net";

    public async Task<(bool Success, int StatusCode, string? Body)> StartPodAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.RestBaseUrl.TrimEnd('/')}/pods/{_options.PodId}/start");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var http = _httpClientFactory.CreateClient();
        using var response = await http.SendAsync(request, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.IsSuccessStatusCode, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
    }

    public async Task<(bool Success, int StatusCode, string? Body)> StopPodAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.RestBaseUrl.TrimEnd('/')}/pods/{_options.PodId}/stop");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var http = _httpClientFactory.CreateClient();
        using var response = await http.SendAsync(request, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.IsSuccessStatusCode, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
    }

    public async Task<(bool Online, int? StatusCode)> IsComfyUiOnlineAsync(CancellationToken cancellationToken)
    {
        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(_options.OnlineCheckTimeoutSeconds);

        try
        {
            using var response = await http.GetAsync(GetComfyUiBaseUrl(), cancellationToken);
            return (response.IsSuccessStatusCode, (int)response.StatusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, null);
        }
        catch (HttpRequestException)
        {
            return (false, null);
        }
    }

    public async Task<bool> WaitUntilOnlineAsync(TimeSpan? maxWait, CancellationToken cancellationToken)
    {
        DateTimeOffset? deadline = maxWait is null ? null : DateTimeOffset.UtcNow.Add(maxWait.Value);

        while (true)
        {
            var (online, _) = await IsComfyUiOnlineAsync(cancellationToken);
            if (online)
            {
                return true;
            }

            if (deadline is not null && DateTimeOffset.UtcNow >= deadline.Value)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }
}

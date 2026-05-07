using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.StaticFiles;

namespace LigaDesligaPod.Services;

public sealed class ComfyUiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RunPodService _runPod;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public ComfyUiService(IHttpClientFactory httpClientFactory, RunPodService runPod)
    {
        _httpClientFactory = httpClientFactory;
        _runPod = runPod;
    }

    private HttpClient CreateClient()
    {
        var http = _httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(_runPod.GetComfyUiBaseUrl());
        http.Timeout = TimeSpan.FromMinutes(5);
        return http;
    }

    public async Task<(bool Success, string? FileName, int StatusCode, string? Body)> UploadImageAsync(
        IFormFile image,
        CancellationToken cancellationToken)
    {
        var http = CreateClient();

        await using var stream = image.OpenReadStream();
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(image.ContentType) ? "application/octet-stream" : image.ContentType);

        // ComfyUI espera o campo "image"
        form.Add(fileContent, "image", image.FileName);

        using var response = await http.PostAsync("/upload/image", form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (false, null, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var name = doc.RootElement.TryGetProperty("name", out var prop) ? prop.GetString() : null;
            return (true, name, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }
        catch (JsonException)
        {
            return (true, null, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }
    }

    public async Task<(bool Success, string? PromptId, int? Number, int StatusCode, string? Body)> QueuePromptAsync(
        JsonObject promptGraph,
        CancellationToken cancellationToken)
    {
        var http = CreateClient();

        var payload = new JsonObject
        {
            ["prompt"] = promptGraph
        };

        using var response = await http.PostAsJsonAsync("/prompt", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (false, null, null, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            string? promptId = null;
            int? number = null;

            if (doc.RootElement.TryGetProperty("prompt_id", out var pid))
            {
                promptId = pid.GetString();
            }
            else if (doc.RootElement.TryGetProperty("promptId", out var pid2))
            {
                promptId = pid2.GetString();
            }

            if (doc.RootElement.TryGetProperty("number", out var num) && num.TryGetInt32(out var n))
            {
                number = n;
            }

            return (true, promptId, number, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }
        catch (JsonException)
        {
            return (true, null, null, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }
    }

    public async Task<(bool Success, JsonNode? BodyJson, int StatusCode, string? Body)> GetHistoryAsync(
        string promptId,
        CancellationToken cancellationToken)
    {
        var http = CreateClient();
        using var response = await http.GetAsync($"/history/{Uri.EscapeDataString(promptId)}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (false, null, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }

        try
        {
            var json = JsonNode.Parse(body);
            return (true, json, (int)response.StatusCode, null);
        }
        catch (JsonException)
        {
            return (true, null, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }
    }

    public async Task<(bool Found, string? FileName, string? Subfolder, string? Type)> TryFindFirstVideoFileAsync(
        string promptId,
        CancellationToken cancellationToken)
    {
        var (success, json, _, _) = await GetHistoryAsync(promptId, cancellationToken);
        if (!success || json is null)
        {
            return (false, null, null, null);
        }

        // Estrutura típica:
        // {
        //   "<promptId>": { "outputs": { "<nodeId>": { "videos"/"gifs"/"images": [ {filename, subfolder, type} ] } } }
        // }
        var promptNode = json[promptId];
        var outputs = promptNode?["outputs"] as JsonObject;
        if (outputs is null)
        {
            return (false, null, null, null);
        }

        foreach (var outputEntry in outputs)
        {
            if (outputEntry.Value is not JsonObject outputObj)
            {
                continue;
            }

            foreach (var kind in new[] { "videos", "gifs", "images" })
            {
                if (outputObj[kind] is not JsonArray filesArray)
                {
                    continue;
                }

                foreach (var item in filesArray)
                {
                    if (item is not JsonObject fileObj)
                    {
                        continue;
                    }

                    var filename = fileObj["filename"]?.GetValue<string>();
                    var subfolder = fileObj["subfolder"]?.GetValue<string>();
                    var type = fileObj["type"]?.GetValue<string>();

                    if (string.IsNullOrWhiteSpace(filename))
                    {
                        continue;
                    }

                    var lower = filename.ToLowerInvariant();
                    var looksLikeVideo =
                        lower.EndsWith(".mp4") ||
                        lower.EndsWith(".webm") ||
                        lower.EndsWith(".mov") ||
                        lower.EndsWith(".gif");

                    if (!looksLikeVideo)
                    {
                        continue;
                    }

                    return (true, filename, subfolder, string.IsNullOrWhiteSpace(type) ? "output" : type);
                }
            }
        }

        return (false, null, null, null);
    }

    public async Task<(bool Success, byte[]? Bytes, string? ContentType, int StatusCode, string? Body)> DownloadFileAsync(
        string filename,
        string? subfolder,
        string? type,
        CancellationToken cancellationToken)
    {
        var http = CreateClient();

        var qs = new List<string> { $"filename={Uri.EscapeDataString(filename)}" };
        if (!string.IsNullOrWhiteSpace(subfolder))
        {
            qs.Add($"subfolder={Uri.EscapeDataString(subfolder)}");
        }

        qs.Add($"type={Uri.EscapeDataString(string.IsNullOrWhiteSpace(type) ? "output" : type)}");

        using var response = await http.GetAsync($"/view?{string.Join("&", qs)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, null, null, (int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : body);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (string.IsNullOrWhiteSpace(contentType))
        {
            _contentTypeProvider.TryGetContentType(filename, out contentType);
            contentType ??= "application/octet-stream";
        }

        return (true, bytes, contentType, (int)response.StatusCode, null);
    }
}


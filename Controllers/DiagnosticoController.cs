using System.Reflection;
using LigaDesligaPod.Services;
using LigaDesligaPod.Services.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LigaDesligaPod.Controllers;

[ApiController]
[Route("api/diagnostico")]
public sealed class DiagnosticoController : ControllerBase
{
    private readonly RunPodService _runPod;
    private readonly RunPodOptions _options;

    public DiagnosticoController(RunPodService runPod, IOptions<RunPodOptions> options)
    {
        _runPod = runPod;
        _options = options.Value;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var version =
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        return Ok(new
        {
            appVersion = version,
            podId = _options.PodId,
            restBaseUrl = _options.RestBaseUrl,
            comfyPort = _options.ComfyPort,
            comfyUiBaseUrl = _runPod.GetComfyUiBaseUrl(),
            apiKeyConfigured = !string.IsNullOrWhiteSpace(_options.ApiKey)
        });
    }
}


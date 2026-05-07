using LigaDesligaPod.Services;
using Microsoft.AspNetCore.Mvc;

namespace LigaDesligaPod.Controllers;

[ApiController]
[Route("api/online")]
public sealed class OnlineController : ControllerBase
{
    private readonly RunPodService _runPod;

    public OnlineController(RunPodService runPod) => _runPod = runPod;

    [HttpGet]
    public async Task<IActionResult> OnlineAsync(CancellationToken cancellationToken)
    {
        var result = await _runPod.IsComfyUiOnlineAsync(cancellationToken);

        return Ok(new
        {
            online = result.Online,
            statusCode = result.StatusCode,
            checkedAtUtc = DateTimeOffset.UtcNow,
            comfyUiBaseUrl = _runPod.GetComfyUiBaseUrl()
        });
    }
}


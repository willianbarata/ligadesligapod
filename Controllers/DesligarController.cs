using LigaDesligaPod.Services;
using Microsoft.AspNetCore.Mvc;

namespace LigaDesligaPod.Controllers;

[ApiController]
[Route("api/desligar")]
public sealed class DesligarController : ControllerBase
{
    private readonly RunPodService _runPod;

    public DesligarController(RunPodService runPod) => _runPod = runPod;

    [HttpPost]
    public async Task<IActionResult> DesligarAsync(CancellationToken cancellationToken)
    {
        var stopped = await _runPod.StopPodAsync(cancellationToken);
        return Ok(new { stopped = stopped.Success, stopped.StatusCode, message = "Comando de stop enviado." });
    }
}


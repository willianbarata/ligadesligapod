using LigaDesligaPod.Services;
using Microsoft.AspNetCore.Mvc;

namespace LigaDesligaPod.Controllers;

[ApiController]
[Route("api/ligar")]
public sealed class LigarController : ControllerBase
{
    private readonly RunPodService _runPod;

    public LigarController(RunPodService runPod) => _runPod = runPod;

    [HttpPost]
    public async Task<IActionResult> LigarAsync(
        [FromQuery] bool esperarOnline = false,
        [FromQuery] int maxEsperaSegundos = 0,
        CancellationToken cancellationToken = default)
    {
        var started = await _runPod.StartPodAsync(cancellationToken);

        if (!started.Success)
        {
            return StatusCode(started.StatusCode, new
            {
                started = false,
                started.StatusCode,
                started.Body,
                message = "Falha ao enviar comando de start. Veja o Body para detalhes."
            });
        }

        if (!esperarOnline)
        {
            return Accepted(new
            {
                started = started.Success,
                started.StatusCode,
                started.Body,
                message = "Comando de start enviado.",
                comfyUiBaseUrl = _runPod.GetComfyUiBaseUrl()
            });
        }

        var online = await _runPod.WaitUntilOnlineAsync(
            maxEsperaSegundos <= 0 ? null : TimeSpan.FromSeconds(maxEsperaSegundos),
            cancellationToken);

        return Ok(new
        {
            started = started.Success,
            started.StatusCode,
            started.Body,
            message = online ? "ComfyUI online." : "ComfyUI ainda não respondeu dentro do tempo.",
            online,
            comfyUiBaseUrl = _runPod.GetComfyUiBaseUrl()
        });
    }
}

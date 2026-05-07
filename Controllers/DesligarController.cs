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

        if (!stopped.Success)
        {
            return StatusCode(stopped.StatusCode, new
            {
                stopped = false,
                stopped.StatusCode,
                stopped.Body,
                message = "Falha ao enviar comando de stop. Veja o Body para detalhes."
            });
        }

        return Ok(new { stopped = true, stopped.StatusCode, stopped.Body, message = "Comando de stop enviado." });
    }
}

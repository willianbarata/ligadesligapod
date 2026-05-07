using System.Text.Json.Nodes;
using LigaDesligaPod.Services;
using Microsoft.AspNetCore.Mvc;

namespace LigaDesligaPod.Controllers;

[ApiController]
[Route("api/videos")]
public sealed class VideosController : ControllerBase
{
    private readonly ComfyUiService _comfy;
    private readonly RunPodService _runPod;

    public VideosController(ComfyUiService comfy, RunPodService runPod)
    {
        _comfy = comfy;
        _runPod = runPod;
    }

    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> CriarAsync(
        [FromForm] IFormFile image,
        [FromForm] string prompt,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length <= 0)
        {
            return BadRequest(new { message = "Envie o arquivo no campo multipart 'image'." });
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return BadRequest(new { message = "Envie o texto no campo multipart 'prompt'." });
        }

        var upload = await _comfy.UploadImageAsync(image, cancellationToken);
        if (!upload.Success || string.IsNullOrWhiteSpace(upload.FileName))
        {
            return StatusCode(upload.StatusCode, new
            {
                message = "Falha no upload da imagem no ComfyUI.",
                upload.StatusCode,
                upload.Body
            });
        }

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "template-short-reels.json");
        if (!System.IO.File.Exists(templatePath))
        {
            return StatusCode(500, new
            {
                message = "Template não encontrado no container.",
                expectedPath = templatePath
            });
        }

        var templateText = await System.IO.File.ReadAllTextAsync(templatePath, cancellationToken);
        if (JsonNode.Parse(templateText) is not JsonObject graph)
        {
            return StatusCode(500, new { message = "Template inválido: não é um objeto JSON." });
        }

        // 1) injetar imagem no nó 269 -> inputs.image
        if (graph["269"]?["inputs"] is JsonObject loadImageInputs)
        {
            loadImageInputs["image"] = upload.FileName;
        }

        // 2) injetar prompt no nó 267:266 -> inputs.value
        if (graph["267:266"]?["inputs"] is JsonObject promptInputs)
        {
            promptInputs["value"] = prompt;
        }

        var queued = await _comfy.QueuePromptAsync(graph, cancellationToken);
        if (!queued.Success)
        {
            return StatusCode(queued.StatusCode, new
            {
                message = "Falha ao enfileirar prompt no ComfyUI.",
                queued.StatusCode,
                queued.Body
            });
        }

        return Accepted(new
        {
            promptId = queued.PromptId,
            number = queued.Number,
            comfyUiBaseUrl = _runPod.GetComfyUiBaseUrl()
        });
    }

    [HttpGet("{promptId}/status")]
    public async Task<IActionResult> StatusAsync([FromRoute] string promptId, CancellationToken cancellationToken)
    {
        var found = await _comfy.TryFindFirstVideoFileAsync(promptId, cancellationToken);
        return Ok(new
        {
            promptId,
            ready = found.Found,
            file = found.Found ? new { found.FileName, found.Subfolder, found.Type } : null,
            comfyUiBaseUrl = _runPod.GetComfyUiBaseUrl()
        });
    }

    // GET que "consulta e baixa": se ainda não pronto -> 202, se pronto -> stream do vídeo
    [HttpGet("{promptId}")]
    public async Task<IActionResult> BaixarAsync([FromRoute] string promptId, CancellationToken cancellationToken)
    {
        var found = await _comfy.TryFindFirstVideoFileAsync(promptId, cancellationToken);
        if (!found.Found || string.IsNullOrWhiteSpace(found.FileName))
        {
            return Accepted(new
            {
                promptId,
                ready = false,
                message = "Ainda processando (ou não há arquivo de vídeo no history).",
                comfyUiBaseUrl = _runPod.GetComfyUiBaseUrl()
            });
        }

        var download = await _comfy.DownloadFileAsync(found.FileName!, found.Subfolder, found.Type, cancellationToken);
        if (!download.Success || download.Bytes is null)
        {
            return StatusCode(download.StatusCode, new { message = "Falha ao baixar arquivo no ComfyUI.", download.StatusCode, download.Body });
        }

        return File(download.Bytes, download.ContentType ?? "application/octet-stream", fileDownloadName: found.FileName);
    }
}


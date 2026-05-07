using System.ComponentModel.DataAnnotations;

namespace LigaDesligaPod.Services.Options;

public sealed class RunPodOptions
{
    public const string SectionName = "RunPod";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string PodId { get; init; } = string.Empty;

    [Required]
    public string RestBaseUrl { get; init; } = "https://rest.runpod.io/v1";

    [Range(1, 65535)]
    public int ComfyPort { get; init; } = 8188;

    [Range(1, 60)]
    public int OnlineCheckTimeoutSeconds { get; init; } = 5;
}


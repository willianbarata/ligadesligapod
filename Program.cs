using LigaDesligaPod.Services;
using LigaDesligaPod.Services.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddOptions<RunPodOptions>()
    .Bind(builder.Configuration.GetSection(RunPodOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<RunPodService>();

var app = builder.Build();

app.MapControllers();

app.Run();

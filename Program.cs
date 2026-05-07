using LigaDesligaPod.Services;
using LigaDesligaPod.Services.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddOptions<RunPodOptions>()
    .Bind(builder.Configuration.GetSection(RunPodOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<RunPodService>();
builder.Services.AddSingleton<ComfyUiService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();

app.Run();

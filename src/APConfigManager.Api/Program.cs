using APConfigManager.Api;
using APConfigManager.Api.Hubs;
using APConfigManager.Core.Data;
using APConfigManager.Core.Interfaces.Parsers;
using APConfigManager.Core.Interfaces.Services;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Infrastructure.Data;
using APConfigManager.Infrastructure.Drivers.Ardupilot;
using APConfigManager.Infrastructure.Parsers;
using APConfigManager.Infrastructure.Services;
using APConfigManager.Infrastructure.Transport;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// ─── Controllers & Swagger ──────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ─── SignalR ────────────────────────────────────
builder.Services.AddSignalR();

// ─── CORS ───────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ─── Data ────────────────────────────────
var dbFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "APConfigManager");

if (!Directory.Exists(dbFolder))
    Directory.CreateDirectory(dbFolder);

var dbPath = Path.Combine(dbFolder, "app.db");

builder.Services.AddSingleton(new LiteDbContext(dbPath));
builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
builder.Services.AddSingleton<IDeviceProfileRepository, DeviceProfileRepository>();

// ─── Transport ──────────────────────────────────
builder.Services.AddSingleton<IPortScanner, PortScanner>();

// ─── Parsers ────────────────────────────────────
builder.Services.AddSingleton<IFirmwareParser, ApjFirmwareParser>();
builder.Services.AddSingleton<IParamFileParser, ParamFileParser>();

// ─── Services ───────────────────────────────────
builder.Services.AddSingleton<IFirmwareValidator, FirmwareValidator>();

builder.Services.AddSingleton<ISessionManager>(sp =>
{
    var portScanner = sp.GetRequiredService<IPortScanner>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    SessionManager? manager = null;

    manager = new SessionManager(
        loggerFactory.CreateLogger<SessionManager>(),
        () =>
    {
        var port = new SerialPortAdapter(loggerFactory.CreateLogger<SerialPortAdapter>());
        var bootloader = new StmBootloaderProtocol(port, loggerFactory.CreateLogger<StmBootloaderProtocol>());
        var telemetry = new MavLinkProtocol(port, loggerFactory.CreateLogger<MavLinkProtocol>());
        return new ArduPilotDriver(port, bootloader, telemetry, portScanner,
            loggerFactory.CreateLogger<ArduPilotDriver>(),
            excludeId => manager!.GetOccupiedPorts(excludeId));
    });

    return manager;
});

builder.Services.AddSingleton<IProfileFileService, ProfileFileService>();

builder.Services.AddScoped<IFlashService, FlashService>();
builder.Services.AddScoped<IEraseService, EraseService>();
builder.Services.AddScoped<IParamService, ParamService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ─── Build ──────────────────────────────────────
var app = builder.Build();

// ─── Middleware ──────────────────────────────────
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    _ = app.UseSwagger();
    _ = app.UseSwaggerUI();
}

app.UseCors("AllowLocalhost");
app.UseRouting();

var uiPath = FindUiPath(app.Environment);
if (uiPath is not null)
{
    _ = app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(uiPath)
    });
    _ = app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uiPath)
    });
}

if (uiPath is not null)
{
    _ = app.MapFallback(async context =>
    {
        var requestPath = context.Request.Path.Value ?? string.Empty;
        if (!HttpMethods.IsGet(context.Request.Method) ||
            requestPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            requestPath.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(uiPath, "index.html"));
    });
}

// ─── Endpoints ──────────────────────────────────
app.MapControllers();
app.MapHub<DeviceHub>("/hubs/device");

await app.RunAsync();


static string? FindUiPath(IHostEnvironment environment)
{
    return EnumerateUiPathCandidates(environment)
        .FirstOrDefault(path => Directory.Exists(path) && File.Exists(Path.Combine(path, "index.html")));
}

static IEnumerable<string> EnumerateUiPathCandidates(IHostEnvironment environment)
{
    var candidatePaths = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        Path.Combine(environment.ContentRootPath, "wwwroot"),
        FindSourceUiDistPath(AppContext.BaseDirectory),
        FindSourceUiDistPath(environment.ContentRootPath)
    };

    return candidatePaths
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Cast<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase);
}

static string? FindSourceUiDistPath(string startDirectory)
{
    var dir = new DirectoryInfo(startDirectory);
    while (dir is not null)
    {
        var path = Path.Combine(dir.FullName, "src", "APConfigManager.UI", "dist");
        if (Directory.Exists(path))
        {
            return path;
        }

        dir = dir.Parent;
    }

    return null;
}

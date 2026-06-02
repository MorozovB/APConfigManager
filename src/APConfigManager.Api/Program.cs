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
        policy.SetIsOriginAllowed(_ => true)    // WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ─── Persistence ────────────────────────────────
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
// builder.Services.AddTransient<ISerialPortAdapter, SerialPortAdapter>();
builder.Services.AddSingleton<IPortScanner, PortScanner>();

// ─── Parsers ────────────────────────────────────
builder.Services.AddSingleton<IFirmwareParser, ApjFirmwareParser>();
builder.Services.AddSingleton<IParamFileParser, ParamFileParser>();

// ─── Protocols ──────────────────────────────────
// builder.Services.AddTransient<IBootloaderProtocol, StmBootloaderProtocol>();
// builder.Services.AddTransient<ITelemetryProtocol, MavLinkProtocol>();

// ─── Driver ─────────────────────────────────────
// builder.Services.AddTransient<IAutopilotDriver, ArduPilotDriver>();

builder.Services.AddSingleton<ISessionManager>(sp =>
{
    var portScanner = sp.GetRequiredService<IPortScanner>();

    return new SessionManager(() =>
    {
        var port = new SerialPortAdapter();
        var bootloader = new StmBootloaderProtocol(port);
        var telemetry = new MavLinkProtocol(port);
        return new ArduPilotDriver(port, bootloader, telemetry, portScanner);
    });
});

// ─── Services ───────────────────────────────────
builder.Services.AddSingleton<IFirmwareValidator, FirmwareValidator>();

//builder.Services.AddSingleton<ISessionManager>(sp =>
//    new SessionManager(() => sp.GetRequiredService<IAutopilotDriver>()));

builder.Services.AddSingleton<IProfileFileService, ProfileFileService>();

builder.Services.AddScoped<IFlashService, FlashService>();
builder.Services.AddScoped<IEraseService, EraseService>();
builder.Services.AddScoped<IParamService, ParamService>();

// ─── Build ──────────────────────────────────────
var app = builder.Build();

// ─── Middleware ──────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowLocalhost");
app.UseRouting();

// ─── Endpoints ──────────────────────────────────
app.MapControllers();
app.MapHub<DeviceHub>("/hubs/device");

await app.RunAsync();

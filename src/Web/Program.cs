using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AnalisisSentimiento.Infrastructure.Configuration;
using AnalisisSentimiento.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Scalar.AspNetCore;

const int preferredPort = 5388;
var shouldOpenBrowser = !args.Contains("--no-open", StringComparer.OrdinalIgnoreCase);
var isDesktopMode = string.IsNullOrWhiteSpace(
    Environment.GetEnvironmentVariable("DOTNET_RESOURCE_SERVICE_ENDPOINT_URL")
);

var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = isDesktopMode ? AppContext.BaseDirectory : null,
        EnvironmentName = isDesktopMode ? Environments.Development : null,
    }
);

// Keep the legacy application name so existing encrypted credentials remain readable
// after the product rebrand.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("EmocionesULEAM");
var keysDirectory = new DirectoryInfo(LocalAppStorage.GetKeysDirectory());
dataProtection.PersistKeysToFileSystem(keysDirectory);

if (isDesktopMode)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
    if (OperatingSystem.IsWindows())
    {
        dataProtection.ProtectKeysWithDpapi();
    }
}

builder.AddServiceDefaults(includeTelemetry: !isDesktopMode);

if (!isDesktopMode)
{
    builder.AddKeyVaultIfConfigured();
}

builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices(includeApiDocumentation: !isDesktopMode);

var app = builder.Build();

var desktopUrl = $"http://127.0.0.1:{preferredPort}";
if (isDesktopMode)
{
    var port = FindAvailablePort(preferredPort);
    desktopUrl = $"http://127.0.0.1:{port}";
    app.Urls.Clear();
    app.Urls.Add(desktopUrl);
    Console.WriteLine($"Sentia Global → {desktopUrl}");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync();
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!isDesktopMode)
{
    app.UseHttpsRedirection();
}
app.UseCors(static builder => builder.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin());

if (app.Environment.IsDevelopment())
{
    // Durante las pruebas evita que el navegador reutilice HTML/CSS/JS de una
    // instancia anterior (especialmente importante al actualizar el frontend).
    app.Use(
        async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            await next();
        }
    );
}

app.UseFileServer();

if (!isDesktopMode)
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler(options => { });

app.MapDefaultEndpoints();
app.MapEndpoints(typeof(Program).Assembly);

if (isDesktopMode && shouldOpenBrowser)
{
    // Puerto nuevo + query evita reabrir la caché de Emociones ULEAM en :5176.
    var urlToOpen = $"{desktopUrl}/?app=sentia&v=20260918";
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            Process.Start(new ProcessStartInfo(urlToOpen) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            app.Logger.LogWarning(exception, "No se pudo abrir el navegador automáticamente.");
            Console.WriteLine($"Abre manualmente: {urlToOpen}");
        }
    });
}

try
{
    app.Run();
}
catch (IOException exception) when (isDesktopMode)
{
    Console.Error.WriteLine($"No se pudo iniciar Sentia Global: {exception.Message}");
    Console.Error.WriteLine("Cierra otras copias de la app o libera el puerto e inténtalo de nuevo.");
    Environment.ExitCode = 1;
}

static int FindAvailablePort(int startPort)
{
    for (var port = startPort; port < startPort + 20; port++)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return port;
        }
        catch (SocketException)
        {
            // Puerto ocupado; probar el siguiente.
        }
    }

    return startPort;
}

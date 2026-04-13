using FileTransferServiceApi;
using FileTransferServiceApi.Models;
using FileTransferServiceApi.Options;
using FileTransferServiceApi.Services;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

if (ClientCommand.TryParse(args, out var clientCommand, out var parseError))
{
    if (parseError is not null)
    {
        Console.Error.WriteLine(parseError);
        ClientCommand.WriteUsage();
        return;
    }

    await RunCommandAsync(clientCommand!);
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FileTransferOptions>(
    builder.Configuration.GetSection(FileTransferOptions.SectionName));

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddSingleton<IApiKeyProtectionService, ApiKeyProtectionService>();

var transferOptions = builder.Configuration
    .GetSection(FileTransferOptions.SectionName)
    .Get<FileTransferOptions>() ?? new FileTransferOptions();

builder.WebHost.UseUrls($"http://0.0.0.0:{transferOptions.Port}");

var app = builder.Build();

app.MapGet("/health", (HttpContext httpContext, IOptions<FileTransferOptions> options) =>
{
    httpContext.Response.Headers.Append(
        "X-Ip-Key-Encryption-Enabled",
        options.Value.EnableIpBasedKeyEncryption.ToString());

    return Results.Ok(new { status = "ok" });
});

app.MapPost("/api/v1/files", async (
    FileTransferRequest request,
    HttpRequest httpRequest,
    IOptions<FileTransferOptions> options,
    IFileStorageService fileStorageService,
    IApiKeyProtectionService apiKeyProtectionService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var authorizationResult = AuthorizeRequest(httpRequest, options.Value, apiKeyProtectionService, logger);

    if (authorizationResult is not null)
    {
        return authorizationResult;
    }

    if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.ContentBase64))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.FileName)] = ["FileName is required."],
            [nameof(request.ContentBase64)] = ["ContentBase64 is required."]
        });
    }

    byte[] fileBytes;

    try
    {
        fileBytes = Convert.FromBase64String(request.ContentBase64);
    }
    catch (FormatException)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.ContentBase64)] = ["ContentBase64 must be a valid base64 string."]
        });
    }

    var storedFile = await fileStorageService.SaveAsync(request.FileName, fileBytes, cancellationToken);

    logger.LogInformation("Stored transferred file {FileName} at {Path}.", storedFile.FileName, storedFile.StoredPath);

    return Results.Ok(new FileTransferResponse(
        storedFile.FileName,
        storedFile.StoredPath,
        storedFile.SizeInBytes,
        DateTimeOffset.UtcNow));
});

app.MapGet("/api/v1/files", async (
    HttpRequest httpRequest,
    IOptions<FileTransferOptions> options,
    IFileStorageService fileStorageService,
    IApiKeyProtectionService apiKeyProtectionService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var authorizationResult = AuthorizeRequest(httpRequest, options.Value, apiKeyProtectionService, logger);

    if (authorizationResult is not null)
    {
        return authorizationResult;
    }

    var storedFiles = await fileStorageService.ListAsync(cancellationToken);
    var response = storedFiles
        .Select(static file => new StoredFileDescriptor(file.FileName, file.SizeInBytes, file.LastModifiedUtc))
        .ToArray();

    return Results.Ok(response);
});

app.MapGet("/api/v1/files/{fileName}", async (
    string fileName,
    HttpRequest httpRequest,
    IOptions<FileTransferOptions> options,
    IFileStorageService fileStorageService,
    IApiKeyProtectionService apiKeyProtectionService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var authorizationResult = AuthorizeRequest(httpRequest, options.Value, apiKeyProtectionService, logger);

    if (authorizationResult is not null)
    {
        return authorizationResult;
    }

    var storedFile = await fileStorageService.GetAsync(fileName, cancellationToken);

    if (storedFile is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new StoredFileContentResponse(
        storedFile.FileName,
        Convert.ToBase64String(storedFile.Content),
        storedFile.SizeInBytes,
        storedFile.LastModifiedUtc));
});

app.MapDelete("/api/v1/files/{fileName}", async (
    string fileName,
    HttpRequest httpRequest,
    IOptions<FileTransferOptions> options,
    IFileStorageService fileStorageService,
    IApiKeyProtectionService apiKeyProtectionService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var authorizationResult = AuthorizeRequest(httpRequest, options.Value, apiKeyProtectionService, logger);

    if (authorizationResult is not null)
    {
        return authorizationResult;
    }

    var deleted = await fileStorageService.DeleteAsync(fileName, cancellationToken);

    if (!deleted)
    {
        return Results.NotFound();
    }

    logger.LogInformation("Deleted transferred file {FileName}.", fileName);

    return Results.Ok(new DeleteFileResponse(fileName, true, DateTimeOffset.UtcNow));
});

app.Run();
return;

static async Task RunCommandAsync(ClientCommand clientCommand)
{
    using var httpClient = new HttpClient
    {
        BaseAddress = new Uri(clientCommand.TargetUrl, UriKind.Absolute)
    };

    if (!await ConfigureAuthenticationAsync(httpClient, clientCommand.SharedKey))
    {
        return;
    }

    switch (clientCommand)
    {
        case SendFileCommand sendFileCommand:
            await RunSendAsync(httpClient, sendFileCommand);
            break;
        case ListFilesCommand:
            await RunListAsync(httpClient);
            break;
        case GetFileCommand getFileCommand:
            await RunGetAsync(httpClient, getFileCommand);
            break;
        case DeleteFileCommand deleteFileCommand:
            await RunDeleteAsync(httpClient, deleteFileCommand);
            break;
    }
}

static async Task RunSendAsync(HttpClient httpClient, SendFileCommand senderCommand)
{
    if (!File.Exists(senderCommand.FilePath))
    {
        Console.Error.WriteLine($"File not found: {senderCommand.FilePath}");
        Environment.ExitCode = 1;
        return;
    }

    var fileBytes = await File.ReadAllBytesAsync(senderCommand.FilePath);
    var request = new FileTransferRequest(
        Path.GetFileName(senderCommand.FilePath),
        Convert.ToBase64String(fileBytes));
    var response = await httpClient.PostAsJsonAsync("/api/v1/files", request);

    await WriteResponseOrFailAsync(response, "Transfer completed successfully.", "Transfer failed");
}

static async Task RunListAsync(HttpClient httpClient)
{
    var response = await httpClient.GetAsync("/api/v1/files");

    if (!response.IsSuccessStatusCode)
    {
        await WriteFailureAsync(response, "List failed");
        return;
    }

    var files = await response.Content.ReadFromJsonAsync<StoredFileDescriptor[]>() ?? [];

    if (files.Length == 0)
    {
        Console.WriteLine("No files available.");
        return;
    }

    foreach (var file in files)
    {
        Console.WriteLine($"{file.FileName}\t{file.SizeInBytes}\t{file.LastModifiedUtc:O}");
    }
}

static async Task RunGetAsync(HttpClient httpClient, GetFileCommand getFileCommand)
{
    var response = await httpClient.GetAsync($"/api/v1/files/{Uri.EscapeDataString(getFileCommand.RemoteFileName)}");

    if (!response.IsSuccessStatusCode)
    {
        await WriteFailureAsync(response, "Download failed");
        return;
    }

    var payload = await response.Content.ReadFromJsonAsync<StoredFileContentResponse>();

    if (payload is null)
    {
        Console.Error.WriteLine("Download failed: response payload was empty.");
        Environment.ExitCode = 1;
        return;
    }

    var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(getFileCommand.OutputPath));

    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    var fileBytes = Convert.FromBase64String(payload.ContentBase64);
    await File.WriteAllBytesAsync(getFileCommand.OutputPath, fileBytes);

    Console.WriteLine($"Downloaded {payload.FileName} to {Path.GetFullPath(getFileCommand.OutputPath)}");
}

static async Task RunDeleteAsync(HttpClient httpClient, DeleteFileCommand deleteFileCommand)
{
    var response = await httpClient.DeleteAsync($"/api/v1/files/{Uri.EscapeDataString(deleteFileCommand.RemoteFileName)}");
    await WriteResponseOrFailAsync(response, "Delete completed successfully.", "Delete failed");
}

static async Task<bool> ConfigureAuthenticationAsync(HttpClient httpClient, string sharedKey)
{
    if (!await IsIpBasedEncryptionEnabledAsync(httpClient))
    {
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", sharedKey);
        return true;
    }

    var clientIp = GetLocalIPv4Address();

    if (string.IsNullOrWhiteSpace(clientIp))
    {
        Console.Error.WriteLine("Could not determine a local IPv4 address for client-side IP-based key encryption.");
        Environment.ExitCode = 1;
        return false;
    }

    var apiKeyProtectionService = new ApiKeyProtectionService();
    var encryptedApiKey = apiKeyProtectionService.Protect(sharedKey, clientIp);
    httpClient.DefaultRequestHeaders.Add("X-Encrypted-Api-Key", encryptedApiKey);
    return true;
}

static async Task<bool> IsIpBasedEncryptionEnabledAsync(HttpClient httpClient)
{
    try
    {
        using var response = await httpClient.GetAsync("/health");

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        if (!response.Headers.TryGetValues("X-Ip-Key-Encryption-Enabled", out var values))
        {
            return false;
        }

        return values.Any(value => bool.TryParse(value, out var enabled) && enabled);
    }
    catch
    {
        return false;
    }
}

static async Task WriteResponseOrFailAsync(
    HttpResponseMessage response,
    string successMessage,
    string failureMessage)
{
    if (!response.IsSuccessStatusCode)
    {
        await WriteFailureAsync(response, failureMessage);
        return;
    }

    Console.WriteLine(successMessage);
    Console.WriteLine(await response.Content.ReadAsStringAsync());
}

static async Task WriteFailureAsync(HttpResponseMessage response, string operationName)
{
    var responseText = await response.Content.ReadAsStringAsync();
    Console.Error.WriteLine($"{operationName} ({(int)response.StatusCode}): {responseText}");
    Environment.ExitCode = 1;
}

static IResult? AuthorizeRequest(
    HttpRequest httpRequest,
    FileTransferOptions configuredOptions,
    IApiKeyProtectionService apiKeyProtectionService,
    ILogger logger)
{
    var plainApiKey = httpRequest.Headers["X-Api-Key"].FirstOrDefault();
    var encryptedApiKey = httpRequest.Headers["X-Encrypted-Api-Key"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(configuredOptions.SharedKey))
    {
        logger.LogError("No shared key is configured for the API service.");
        return Results.Problem("API shared key is not configured.", statusCode: StatusCodes.Status500InternalServerError);
    }

    if (configuredOptions.EnableIpBasedKeyEncryption &&
        configuredOptions.AuthorizedClientIpAddresses.Count == 0)
    {
        logger.LogError("IP-based key encryption is enabled, but no client IP addresses are configured.");
        return Results.Problem("IP-based key encryption is enabled without configured client IP addresses.", statusCode: StatusCodes.Status500InternalServerError);
    }

    var isAuthorized = configuredOptions.EnableIpBasedKeyEncryption
        ? apiKeyProtectionService.TryMatchEncrypted(
            encryptedApiKey ?? string.Empty,
            configuredOptions.SharedKey,
            configuredOptions.AuthorizedClientIpAddresses)
        : string.Equals(plainApiKey, configuredOptions.SharedKey, StringComparison.Ordinal);

    if (!isAuthorized)
    {
        logger.LogWarning("Rejected incoming request because the API key did not match.");
        return Results.Unauthorized();
    }

    return null;
}

static string? GetLocalIPv4Address()
{
    foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (networkInterface.OperationalStatus != OperationalStatus.Up ||
            networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
        {
            continue;
        }

        var ipProperties = networkInterface.GetIPProperties();

        foreach (var unicastAddress in ipProperties.UnicastAddresses)
        {
            if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(unicastAddress.Address))
            {
                return unicastAddress.Address.ToString();
            }
        }
    }

    return null;
}

public partial class Program;

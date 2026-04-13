using System.Net;
using System.Net.Http.Json;
using System.Text;
using FileTransferServiceApi.Models;
using FileTransferServiceApi.Services;
using FileTransferServiceApi.Tests.Infrastructure;
using NUnit.Framework;

namespace FileTransferServiceApi.Tests;

[TestFixture]
public sealed class FileTransferEndpointTests
{
    private string _storagePath = null!;
    private FileTransferApiFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _storagePath = Path.Combine(Path.GetTempPath(), $"file-transfer-tests-{Guid.NewGuid():N}");
        _factory = new FileTransferApiFactory(_storagePath, "test-shared-key");
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();

        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, recursive: true);
        }
    }

    [Test]
    public async Task PostFile_WithValidKey_StoresFileAndReturnsOk()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-shared-key");

        var request = new FileTransferRequest(
            "notes.txt",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("hello from nunit")));

        var response = await client.PostAsJsonAsync("/api/v1/files", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<FileTransferResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.FileName, Is.EqualTo("notes.txt"));

        var storedFiles = Directory.GetFiles(_storagePath);
        Assert.That(storedFiles, Has.Length.EqualTo(1));
        Assert.That(Path.GetFileName(storedFiles[0]), Is.EqualTo("notes.txt"));
        Assert.That(await File.ReadAllTextAsync(storedFiles[0]), Is.EqualTo("hello from nunit"));
    }

    [Test]
    public async Task PostFile_WithWrongKey_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var request = new FileTransferRequest(
            "notes.txt",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("hello from nunit")));

        var response = await client.PostAsJsonAsync("/api/v1/files", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(Directory.Exists(_storagePath), Is.False);
    }

    [Test]
    public async Task PostFile_WithEncryptedKeyAndMatchingSenderIp_StoresFileAndReturnsOk()
    {
        _factory.Dispose();
        _factory = new FileTransferApiFactory(
            _storagePath,
            "test-shared-key",
            enableIpBasedKeyEncryption: true,
            clientIpAddresses: ["10.10.10.25", "10.10.10.26"]);

        using var client = _factory.CreateClient();
        var apiKeyProtectionService = new ApiKeyProtectionService();
        var encryptedKey = apiKeyProtectionService.Protect("test-shared-key", "10.10.10.26");
        client.DefaultRequestHeaders.Add("X-Encrypted-Api-Key", encryptedKey);

        var request = new FileTransferRequest(
            "encrypted.txt",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("encrypted mode")));

        var response = await client.PostAsJsonAsync("/api/v1/files", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(Directory.GetFiles(_storagePath), Has.Length.EqualTo(1));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(_storagePath, "encrypted.txt")), Is.EqualTo("encrypted mode"));
    }

    [Test]
    public async Task ListFiles_WithValidKey_ReturnsStoredFiles()
    {
        Directory.CreateDirectory(_storagePath);
        await File.WriteAllTextAsync(Path.Combine(_storagePath, "a.txt"), "alpha");
        await File.WriteAllTextAsync(Path.Combine(_storagePath, "b.txt"), "beta");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-shared-key");

        var response = await client.GetAsync("/api/v1/files");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<StoredFileDescriptor[]>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!, Has.Length.EqualTo(2));
        Assert.That(payload.Select(static item => item.FileName), Is.EqualTo(new[] { "a.txt", "b.txt" }));
    }

    [Test]
    public async Task GetFile_WithValidKey_ReturnsBase64Payload()
    {
        Directory.CreateDirectory(_storagePath);
        await File.WriteAllTextAsync(Path.Combine(_storagePath, "download.txt"), "download me");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-shared-key");

        var response = await client.GetAsync("/api/v1/files/download.txt");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadFromJsonAsync<StoredFileContentResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.FileName, Is.EqualTo("download.txt"));
        Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(payload.ContentBase64)), Is.EqualTo("download me"));
    }

    [Test]
    public async Task DeleteFile_WithValidKey_RemovesStoredFile()
    {
        Directory.CreateDirectory(_storagePath);
        var path = Path.Combine(_storagePath, "delete-me.txt");
        await File.WriteAllTextAsync(path, "delete me");

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-shared-key");

        var response = await client.DeleteAsync("/api/v1/files/delete-me.txt");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(File.Exists(path), Is.False);

        var payload = await response.Content.ReadFromJsonAsync<DeleteFileResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Deleted, Is.True);
        Assert.That(payload.FileName, Is.EqualTo("delete-me.txt"));
    }

    [Test]
    public async Task DeleteFile_WithEncryptedKeyAndMatchingSenderIp_RemovesStoredFile()
    {
        Directory.CreateDirectory(_storagePath);
        var path = Path.Combine(_storagePath, "secured.txt");
        await File.WriteAllTextAsync(path, "secured");

        _factory.Dispose();
        _factory = new FileTransferApiFactory(
            _storagePath,
            "test-shared-key",
            enableIpBasedKeyEncryption: true,
            clientIpAddresses: ["10.10.10.25"]);

        using var client = _factory.CreateClient();
        var apiKeyProtectionService = new ApiKeyProtectionService();
        client.DefaultRequestHeaders.Add(
            "X-Encrypted-Api-Key",
            apiKeyProtectionService.Protect("test-shared-key", "10.10.10.25"));

        var response = await client.DeleteAsync("/api/v1/files/secured.txt");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(File.Exists(path), Is.False);
    }
}

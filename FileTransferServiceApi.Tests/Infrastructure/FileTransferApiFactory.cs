using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FileTransferServiceApi.Tests.Infrastructure;

public sealed class FileTransferApiFactory : WebApplicationFactory<Program>
{
    public FileTransferApiFactory(
        string storagePath,
        string sharedKey,
        bool enableIpBasedKeyEncryption = false,
        IEnumerable<string>? clientIpAddresses = null)
    {
        StoragePath = storagePath;
        SharedKey = sharedKey;
        EnableIpBasedKeyEncryption = enableIpBasedKeyEncryption;
        ClientIpAddresses = clientIpAddresses?.ToArray() ?? [];
    }

    public string StoragePath { get; }

    public string SharedKey { get; }

    public bool EnableIpBasedKeyEncryption { get; }

    public IReadOnlyList<string> ClientIpAddresses { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["FileTransfer:Port"] = "5050",
                ["FileTransfer:SharedKey"] = SharedKey,
                ["FileTransfer:StoragePath"] = StoragePath,
                ["FileTransfer:EnableIpBasedKeyEncryption"] = EnableIpBasedKeyEncryption.ToString()
            };

            for (var i = 0; i < ClientIpAddresses.Count; i++)
            {
                settings[$"FileTransfer:ClientIpAddresses:{i}"] = ClientIpAddresses[i];
            }

            configurationBuilder.AddInMemoryCollection(settings);
        });
    }
}

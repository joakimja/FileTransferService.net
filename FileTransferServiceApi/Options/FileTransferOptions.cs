namespace FileTransferServiceApi.Options;

public sealed class FileTransferOptions
{
    public const string SectionName = "FileTransfer";

    public int Port { get; set; } = 5050;

    public string SharedKey { get; set; } = "change-this-key";

    public string StoragePath { get; set; } = "ReceivedFiles";

    public bool EnableIpBasedKeyEncryption { get; set; }

    public List<string> ClientIpAddresses { get; set; } = [];

    public List<string> SenderIpAddresses { get; set; } = [];

    public IReadOnlyList<string> AuthorizedClientIpAddresses =>
        ClientIpAddresses.Count > 0
            ? ClientIpAddresses
            : SenderIpAddresses;
}

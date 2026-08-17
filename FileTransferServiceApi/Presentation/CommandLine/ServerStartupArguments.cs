using FileTransferServiceApi.Options;

namespace FileTransferServiceApi.Presentation.CommandLine;

public sealed record ServerStartupArguments(int? Port, string? StoragePath, string? SharedKey)
{
    public bool HasOverrides =>
        Port is not null ||
        !string.IsNullOrWhiteSpace(StoragePath) ||
        !string.IsNullOrWhiteSpace(SharedKey);

    public static bool TryParse(string[] args, out ServerStartupArguments arguments, out string? parseError)
    {
        arguments = new ServerStartupArguments(null, null, null);
        parseError = null;

        int? port = null;
        string? storagePath = null;
        string? sharedKey = null;

        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];

            if (!IsRecognizedArgument(current))
            {
                continue;
            }

            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                parseError = $"Missing value for {current}.";
                return false;
            }

            var value = args[++i];

            if (IsPortArgument(current))
            {
                if (!int.TryParse(value, out var parsedPort) || parsedPort is < 1 or > 65535)
                {
                    parseError = "Port must be a number between 1 and 65535.";
                    return false;
                }

                port = parsedPort;
                continue;
            }

            if (IsStoragePathArgument(current))
            {
                storagePath = value;
                continue;
            }

            sharedKey = value;
        }

        arguments = new ServerStartupArguments(port, storagePath, sharedKey);
        return true;
    }

    public IReadOnlyDictionary<string, string?> ToConfigurationOverrides()
    {
        var overrides = new Dictionary<string, string?>();

        if (Port is not null)
        {
            overrides[$"{FileTransferOptions.SectionName}:{nameof(FileTransferOptions.Port)}"] = Port.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(StoragePath))
        {
            overrides[$"{FileTransferOptions.SectionName}:{nameof(FileTransferOptions.StoragePath)}"] = StoragePath;
        }

        if (!string.IsNullOrWhiteSpace(SharedKey))
        {
            overrides[$"{FileTransferOptions.SectionName}:{nameof(FileTransferOptions.SharedKey)}"] = SharedKey;
        }

        return overrides;
    }

    public static void WriteUsage()
    {
        Console.WriteLine("API startup arguments:");
        Console.WriteLine("  FileTransferServiceApi [--port <port>] [--folder <path>] [--password <shared-key>]");
        Console.WriteLine("  FileTransferServiceApi [--port <port>] [--storage-path <path>] [--key <shared-key>]");
    }

    private static bool IsRecognizedArgument(string argument) =>
        IsPortArgument(argument) || IsStoragePathArgument(argument) || IsSharedKeyArgument(argument);

    private static bool IsPortArgument(string argument) =>
        string.Equals(argument, "--port", StringComparison.OrdinalIgnoreCase);

    private static bool IsStoragePathArgument(string argument) =>
        string.Equals(argument, "--folder", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(argument, "--storage-path", StringComparison.OrdinalIgnoreCase);

    private static bool IsSharedKeyArgument(string argument) =>
        string.Equals(argument, "--password", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(argument, "--key", StringComparison.OrdinalIgnoreCase);
}

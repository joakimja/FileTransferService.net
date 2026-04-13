namespace FileTransferServiceApi;

public abstract record ClientCommand(string TargetUrl, string SharedKey)
{
    public static bool TryParse(string[] args, out ClientCommand? command, out string? parseError)
    {
        command = null;
        parseError = null;

        if (args.Length == 0)
        {
            return false;
        }

        var verb = args[0];

        if (!IsSupportedVerb(verb))
        {
            return false;
        }

        var arguments = ParseArguments(args);

        if (!TryGetRequiredArgument(arguments, "--url", out var targetUrl) ||
            !TryGetRequiredArgument(arguments, "--key", out var sharedKey))
        {
            parseError = $"Missing required arguments for {verb} mode.";
            return true;
        }

        if (string.Equals(verb, "send", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetRequiredArgument(arguments, "--file", out var filePath))
            {
                parseError = "Missing required arguments for send mode.";
                return true;
            }

            command = new SendFileCommand(filePath, NormalizeUrl(targetUrl), sharedKey);
            return true;
        }

        if (string.Equals(verb, "list", StringComparison.OrdinalIgnoreCase))
        {
            command = new ListFilesCommand(NormalizeUrl(targetUrl), sharedKey);
            return true;
        }

        if (string.Equals(verb, "get", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetRequiredArgument(arguments, "--file", out var remoteFileName))
            {
                parseError = "Missing required arguments for get mode.";
                return true;
            }

            var outputPath = arguments.TryGetValue("--output", out var configuredOutput)
                ? configuredOutput
                : remoteFileName;

            command = new GetFileCommand(remoteFileName, outputPath, NormalizeUrl(targetUrl), sharedKey);
            return true;
        }

        if (string.Equals(verb, "delete", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetRequiredArgument(arguments, "--file", out var remoteFileName))
            {
                parseError = "Missing required arguments for delete mode.";
                return true;
            }

            command = new DeleteFileCommand(remoteFileName, NormalizeUrl(targetUrl), sharedKey);
            return true;
        }

        return false;
    }

    public static void WriteUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  FileTransferServiceApi send --file <path> --url <http://host:port> --key <shared-key>");
        Console.WriteLine("  FileTransferServiceApi list --url <http://host:port> --key <shared-key>");
        Console.WriteLine("  FileTransferServiceApi get --file <remote-name> --url <http://host:port> --key <shared-key> [--output <path>]");
        Console.WriteLine("  FileTransferServiceApi delete --file <remote-name> --url <http://host:port> --key <shared-key>");
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < args.Length; i++)
        {
            var current = args[i];

            if (!current.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                continue;
            }

            arguments[current] = args[++i];
        }

        return arguments;
    }

    private static bool TryGetRequiredArgument(
        IReadOnlyDictionary<string, string> arguments,
        string name,
        out string value)
    {
        if (arguments.TryGetValue(name, out value!) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string NormalizeUrl(string targetUrl) => targetUrl.TrimEnd('/');

    private static bool IsSupportedVerb(string verb) =>
        string.Equals(verb, "send", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(verb, "list", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(verb, "get", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(verb, "delete", StringComparison.OrdinalIgnoreCase);
}

public sealed record SendFileCommand(string FilePath, string TargetUrl, string SharedKey)
    : ClientCommand(TargetUrl, SharedKey);

public sealed record ListFilesCommand(string TargetUrl, string SharedKey)
    : ClientCommand(TargetUrl, SharedKey);

public sealed record GetFileCommand(string RemoteFileName, string OutputPath, string TargetUrl, string SharedKey)
    : ClientCommand(TargetUrl, SharedKey);

public sealed record DeleteFileCommand(string RemoteFileName, string TargetUrl, string SharedKey)
    : ClientCommand(TargetUrl, SharedKey);

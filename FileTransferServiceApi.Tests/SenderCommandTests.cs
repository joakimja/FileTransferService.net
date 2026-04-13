using NUnit.Framework;

namespace FileTransferServiceApi.Tests;

[TestFixture]
public sealed class ClientCommandTests
{
    [Test]
    public void TryParse_WithCompleteArguments_ReturnsCommand()
    {
        var args = new[]
        {
            "send",
            "--file", @"C:\temp\report.txt",
            "--url", "http://api:5050/",
            "--key", "shared-key"
        };

        var handled = ClientCommand.TryParse(args, out var command, out var parseError);

        Assert.That(handled, Is.True);
        Assert.That(parseError, Is.Null);
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Is.TypeOf<SendFileCommand>());

        var sendCommand = (SendFileCommand)command!;
        Assert.That(sendCommand.FilePath, Is.EqualTo(@"C:\temp\report.txt"));
        Assert.That(sendCommand.TargetUrl, Is.EqualTo("http://api:5050"));
        Assert.That(sendCommand.SharedKey, Is.EqualTo("shared-key"));
    }

    [Test]
    public void TryParse_WithMissingArguments_ReturnsHelpfulError()
    {
        var args = new[]
        {
            "send",
            "--file", @"C:\temp\report.txt"
        };

        var handled = ClientCommand.TryParse(args, out var command, out var parseError);

        Assert.That(handled, Is.True);
        Assert.That(command, Is.Null);
        Assert.That(parseError, Is.EqualTo("Missing required arguments for send mode."));
    }

    [Test]
    public void TryParse_ListCommand_ReturnsCommand()
    {
        var args = new[]
        {
            "list",
            "--url", "http://api:5050/",
            "--key", "shared-key"
        };

        var handled = ClientCommand.TryParse(args, out var command, out var parseError);

        Assert.That(handled, Is.True);
        Assert.That(parseError, Is.Null);
        Assert.That(command, Is.TypeOf<ListFilesCommand>());
        Assert.That(((ListFilesCommand)command!).TargetUrl, Is.EqualTo("http://api:5050"));
    }

    [Test]
    public void TryParse_GetCommand_WithoutOutput_UsesRemoteNameAsLocalPath()
    {
        var args = new[]
        {
            "get",
            "--file", "report.txt",
            "--url", "http://api:5050/",
            "--key", "shared-key"
        };

        var handled = ClientCommand.TryParse(args, out var command, out var parseError);

        Assert.That(handled, Is.True);
        Assert.That(parseError, Is.Null);
        Assert.That(command, Is.TypeOf<GetFileCommand>());

        var getCommand = (GetFileCommand)command!;
        Assert.That(getCommand.RemoteFileName, Is.EqualTo("report.txt"));
        Assert.That(getCommand.OutputPath, Is.EqualTo("report.txt"));
    }

    [Test]
    public void TryParse_DeleteCommand_ReturnsCommand()
    {
        var args = new[]
        {
            "delete",
            "--file", "report.txt",
            "--url", "http://api:5050/",
            "--key", "shared-key"
        };

        var handled = ClientCommand.TryParse(args, out var command, out var parseError);

        Assert.That(handled, Is.True);
        Assert.That(parseError, Is.Null);
        Assert.That(command, Is.TypeOf<DeleteFileCommand>());
        Assert.That(((DeleteFileCommand)command!).RemoteFileName, Is.EqualTo("report.txt"));
    }
}

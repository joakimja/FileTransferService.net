using FileTransferServiceApi.Options;
using Microsoft.Extensions.Options;

namespace FileTransferServiceApi.Services;

public interface IFileStorageService
{
    Task<StoredFileResult> SaveAsync(string incomingFileName, byte[] content, CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredFileEntry>> ListAsync(CancellationToken cancellationToken);

    Task<StoredFileContent?> GetAsync(string fileName, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken);
}

public sealed class FileStorageService(IOptions<FileTransferOptions> options) : IFileStorageService
{
    public async Task<StoredFileResult> SaveAsync(string incomingFileName, byte[] content, CancellationToken cancellationToken)
    {
        var sanitizedFileName = Path.GetFileName(incomingFileName);

        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            throw new InvalidOperationException("Incoming file name is invalid.");
        }

        var storageRoot = Path.GetFullPath(options.Value.StoragePath);
        Directory.CreateDirectory(storageRoot);

        var targetPath = GetUniquePath(storageRoot, sanitizedFileName);
        await File.WriteAllBytesAsync(targetPath, content, cancellationToken);

        return new StoredFileResult(sanitizedFileName, targetPath, content.LongLength);
    }

    public Task<IReadOnlyList<StoredFileEntry>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var storageRoot = EnsureStorageRoot();
        var files = Directory
            .EnumerateFiles(storageRoot)
            .Select(path =>
            {
                var fileInfo = new FileInfo(path);
                return new StoredFileEntry(
                    fileInfo.Name,
                    fileInfo.Length,
                    new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));
            })
            .OrderBy(static file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<StoredFileEntry>>(files);
    }

    public async Task<StoredFileContent?> GetAsync(string fileName, CancellationToken cancellationToken)
    {
        var fullPath = TryResolveExistingFilePath(fileName);

        if (fullPath is null)
        {
            return null;
        }

        var fileInfo = new FileInfo(fullPath);
        var content = await File.ReadAllBytesAsync(fullPath, cancellationToken);

        return new StoredFileContent(
            fileInfo.Name,
            fullPath,
            content,
            fileInfo.Length,
            new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero));
    }

    public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = TryResolveExistingFilePath(fileName);

        if (fullPath is null)
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    private string EnsureStorageRoot()
    {
        var storageRoot = Path.GetFullPath(options.Value.StoragePath);
        Directory.CreateDirectory(storageRoot);
        return storageRoot;
    }

    private static string GetUniquePath(string storageRoot, string sanitizedFileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sanitizedFileName);
        var extension = Path.GetExtension(sanitizedFileName);
        var candidatePath = Path.Combine(storageRoot, sanitizedFileName);
        var counter = 1;

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(storageRoot, $"{fileNameWithoutExtension}_{counter}{extension}");
            counter++;
        }

        return candidatePath;
    }

    private string? TryResolveExistingFilePath(string incomingFileName)
    {
        var sanitizedFileName = Path.GetFileName(incomingFileName);

        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            return null;
        }

        var storageRoot = EnsureStorageRoot();
        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, sanitizedFileName));

        if (!fullPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(fullPath))
        {
            return null;
        }

        return fullPath;
    }
}

public sealed record StoredFileResult(string FileName, string StoredPath, long SizeInBytes);

public sealed record StoredFileEntry(string FileName, long SizeInBytes, DateTimeOffset LastModifiedUtc);

public sealed record StoredFileContent(
    string FileName,
    string StoredPath,
    byte[] Content,
    long SizeInBytes,
    DateTimeOffset LastModifiedUtc);

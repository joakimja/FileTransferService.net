namespace FileTransferServiceApi.Models;

public sealed record StoredFileContentResponse(
    string FileName,
    string ContentBase64,
    long SizeInBytes,
    DateTimeOffset LastModifiedUtc);

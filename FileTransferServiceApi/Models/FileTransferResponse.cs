namespace FileTransferServiceApi.Models;

public sealed record FileTransferResponse(
    string FileName,
    string StoredPath,
    long SizeInBytes,
    DateTimeOffset ReceivedAtUtc);

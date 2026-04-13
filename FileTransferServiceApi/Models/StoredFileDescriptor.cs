namespace FileTransferServiceApi.Models;

public sealed record StoredFileDescriptor(
    string FileName,
    long SizeInBytes,
    DateTimeOffset LastModifiedUtc);

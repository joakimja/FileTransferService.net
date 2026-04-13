namespace FileTransferServiceApi.Models;

public sealed record DeleteFileResponse(
    string FileName,
    bool Deleted,
    DateTimeOffset DeletedAtUtc);

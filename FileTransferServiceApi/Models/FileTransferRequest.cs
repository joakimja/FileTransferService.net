namespace FileTransferServiceApi.Models;

public sealed record FileTransferRequest(string FileName, string ContentBase64);

# FileTransferService.net
 
Self-hosted file transfer in .NET for sending files between two computers without FTP, Samba, or external third-party dependencies.

The application is built around two roles:

- an API service that exposes the file transfer endpoints
- a client that can upload, list, download, and delete files by talking to that API

The API listens on a fixed HTTP port. The client reads local files, converts them to base64 for upload, and can later list, download, and acknowledge files through the same API. Both sides use the same shared key to authorize the operations.

## Documentation

Full documentation is available here:

- [Operations and configuration guide](./docs/filetransferservice-guide.md)

## Quick Start

1. Configure the API in `FileTransferServiceApi/appsettings.json`.
2. Use the same key in the client when connecting to the API.
3. Start the API.
4. Send a file from the other computer.
5. Use `list` to inspect available files, `get` to download them, and `delete` to acknowledge and remove them from the API queue.

## Target Frameworks

The solution can be built for both .NET 8 LTS and .NET 10:

```cmd
dotnet build FileTransferServiceApi.sln -f net8.0
dotnet build FileTransferServiceApi.sln -f net10.0
```

Publish a Windows executable for the runtime you want to deploy:

```cmd
dotnet publish FileTransferServiceApi\FileTransferServiceApi.csproj -c Release -f net8.0 -r win-x64 --self-contained false
dotnet publish FileTransferServiceApi\FileTransferServiceApi.csproj -c Release -f net10.0 -r win-x64 --self-contained false
```

Publish self-contained Windows executables when the target computer should not need a separately installed .NET runtime:

```cmd
dotnet publish FileTransferServiceApi\FileTransferServiceApi.csproj -c Release -f net8.0 -r win-x64 --self-contained true --ignore-failed-sources -o FileTransferServiceApi\Releases\win-x64-selfcontained-net8
dotnet publish FileTransferServiceApi\FileTransferServiceApi.csproj -c Release -f net10.0 -r win-x64 --self-contained true --ignore-failed-sources -o FileTransferServiceApi\Releases\win-x64-selfcontained-net10
```

Use `net8.0` when you want the LTS target. Use `net10.0` when the target computer has the .NET 10 runtime installed.

## Example Configuration

```json
{
  "FileTransfer": {
    "Port": 5050,
    "SharedKey": "my-secret-key",
    "StoragePath": "ReceivedFiles",
    "EnableIpBasedKeyEncryption": false,
    "ClientIpAddresses": []
  }
}
```

## Start API

Example for a normal user in `cmd.exe` with a published application:

```cmd
cd C:\FileTransferService
FileTransferServiceApi.exe
```

## Send File

```cmd
cd C:\FileTransferService
FileTransferServiceApi.exe send --file C:\temp\example.txt --url http://api-host:5050 --key my-secret-key
```

If you want to use IP-based key encryption, use the same command:

```cmd
FileTransferServiceApi.exe send --file C:\temp\example.txt --url http://api-host:5050 --key my-secret-key
```

The client automatically detects a local IPv4 address when IP-based key encryption is enabled.

## List Files

```cmd
cd C:\FileTransferService
FileTransferServiceApi.exe list --url http://api-host:5050 --key my-secret-key
```

## Download File

```cmd
cd C:\FileTransferService
FileTransferServiceApi.exe get --file example.txt --url http://api-host:5050 --key my-secret-key --output C:\temp\example.txt
```

If `--output` is provided, the file is saved exactly at that path on the client.

If `--output` is omitted, the file is saved in the current working directory using the remote file name.

## Delete File After Verification

```cmd
cd C:\FileTransferService
FileTransferServiceApi.exe delete --file example.txt --url http://api-host:5050 --key my-secret-key
```

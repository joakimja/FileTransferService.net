# Operations and Configuration Guide

## Overview

The solution is built around two roles:

- an API service that exposes the file transfer endpoints
- a client that talks to the API for upload, list, download, and delete operations

The API runs as a self-hosted HTTP service on a fixed port. The client runs the same program in command mode and calls the API endpoints for each operation.

Uploaded files are sent as base64 in JSON format to the API.

## How the Flow Works

There are two common flows in the service.

### Scenario 1: Send a File to the API

1. The API starts and listens on the configured port.
2. The client reads a local file from disk.
3. The client converts the file bytes to a base64 string.
4. The client sends an HTTP `POST` request to the API at `/api/v1/files`.
5. The client includes a shared key in the HTTP header `X-Api-Key`.
6. The API compares the key with its local configuration.
7. If the key matches, the base64 payload is decoded into bytes.
8. The API saves the file in the configured directory.
9. The API responds with information about the file that was received.

### Scenario 2: List, Download, and Confirm Files

1. The API starts and listens on the configured port.
2. The client calls the API to get the list of files currently available in the storage folder.
3. The client sends an HTTP `GET` request to `/api/v1/files`.
4. The client includes the shared key in the request header.
5. The API validates the key and responds with information about the files in the folder.
6. The client selects a file and downloads it with `GET /api/v1/files/{fileName}`.
7. The API returns the requested file content and metadata.
8. The client verifies the file locally after download.
9. When the file has been successfully verified, the client confirms it by sending `DELETE /api/v1/files/{fileName}`.
10. The API removes the file from the folder so it is no longer returned by `list`.

## API

### Health Check

Used to verify that the service is running.

```http
GET /health
```

Response:

```json
{
  "status": "ok"
}
```

### Receive File

```http
POST /api/v1/files
X-Api-Key: <shared-key>
Content-Type: application/json
```

Request body:

```json
{
  "FileName": "example.txt",
  "ContentBase64": "SGVsbG8gd29ybGQ="
}
```

### List Available Files

```http
GET /api/v1/files
X-Api-Key: <shared-key>
```

Response:

```json
[
  {
    "FileName": "example.txt",
    "SizeInBytes": 11,
    "LastModifiedUtc": "2026-04-13T10:15:30+00:00"
  }
]
```

### Download a File

```http
GET /api/v1/files/example.txt
X-Api-Key: <shared-key>
```

Response:

```json
{
  "FileName": "example.txt",
  "ContentBase64": "SGVsbG8gd29ybGQ=",
  "SizeInBytes": 11,
  "LastModifiedUtc": "2026-04-13T10:15:30+00:00"
}
```

### Delete a File After Verification

```http
DELETE /api/v1/files/example.txt
X-Api-Key: <shared-key>
```

Response:

```json
{
  "FileName": "example.txt",
  "Deleted": true,
  "DeletedAtUtc": "2026-04-13T10:20:00+00:00"
}
```

## What to Configure

All core configuration is stored in:

- `FileTransferServiceApi/appsettings.json`

The section that controls the feature is:

```json
{
  "FileTransfer": {
    "Port": 5050,
    "SharedKey": "change-this-key",
    "StoragePath": "ReceivedFiles",
    "EnableIpBasedKeyEncryption": false,
    "ClientIpAddresses": []
  }
}
```

### `Port`

The port the API should listen on.

Examples:

- `5050`
- `8080`
- `9000`

This must also be the port the client calls in its `--url`.

### `SharedKey`

The shared secret key used to authorize the transfer.

Important:

- the same key must be used on both sides
- if the key does not match, the API returns `401 Unauthorized`
- always replace the default value before using the solution in production

Example:

```json
"SharedKey": "my-secret-key-2026"
```

### `StoragePath`

The directory where the API stores incoming files.

It can be:

- a relative path, for example `ReceivedFiles`
- an absolute path, for example `C:\\FileTransfer\\Inbox`

Example:

```json
"StoragePath": "C:\\FileTransfer\\Inbox"
```

If the directory does not exist, it is created automatically.

If a file with the same name already exists, the next file is saved with a suffix, for example:

- `report.txt`
- `report_1.txt`
- `report_2.txt`

### `EnableIpBasedKeyEncryption`

Enables an optional mode where the client does not transmit the shared key in plain text, but instead sends an IP-based encrypted variant in the `X-Encrypted-Api-Key` header.

Example:

```json
"EnableIpBasedKeyEncryption": true
```

When this mode is enabled, the client automatically detects a local IPv4 address on the computer where the program is running and uses it to encrypt the key.

### `ClientIpAddresses`

List of client IP addresses the API is allowed to try when decrypting the key.

Example:

```json
"ClientIpAddresses": [
  "192.168.1.20",
  "192.168.1.21"
]
```

The API iterates through the list until a valid decryption is found.

## Configuration for the API Service

On the computer that will host the API, you should:

1. Publish or build the application.
2. Update `appsettings.json`.
3. Choose a port.
4. Choose a storage folder.
5. Set a strong shared key.
6. Open the firewall for the selected port if the computer must be reachable from the network.
7. Start the application as a process or service.

Example:

```json
{
  "FileTransfer": {
    "Port": 5050,
    "SharedKey": "super-secret-key",
    "StoragePath": "C:\\FileTransfer\\Inbox",
    "EnableIpBasedKeyEncryption": true,
    "ClientIpAddresses": [
      "192.168.1.20",
      "192.168.1.21"
    ]
  }
}
```

Start:

```powershell
dotnet run --project .\FileTransferServiceApi
```

Then verify that the API responds:

```powershell
Invoke-RestMethod http://localhost:5050/health
```

## Configuration for the Client

On the computer that will act as a client, you do not need to host the API unless you want that machine to expose the service too. You do need to know:

- the API address or IP
- the API port
- the same shared key used by the API
- the path to the file to send

The same shared key and optional IP-based key encryption are also required when listing, downloading, and deleting files from the API.

Example:

- API: `http://192.168.1.50:5050`
- key: `super-secret-key`
- file: `C:\Temp\report.pdf`

## Send a File

Example command:

```powershell
dotnet run --project .\FileTransferServiceApi -- send --file C:\Temp\report.pdf --url http://192.168.1.50:5050 --key super-secret-key
```

With IP-based key encryption:

```powershell
dotnet run --project .\FileTransferServiceApi -- send --file C:\Temp\report.pdf --url http://192.168.1.50:5050 --key super-secret-key
```

Parameters:

- `send`: starts the program in client upload mode
- `--file`: local path to the file to send
- `--url`: API base URL including port
- `--key`: shared key that must match the API configuration

When IP-based key encryption is enabled, the program tries to find an active local IPv4 address automatically. That address must be included in the API's `ClientIpAddresses`. The legacy config name `SenderIpAddresses` is still accepted for backward compatibility.

## List Available Files

Example command:

```powershell
dotnet run --project .\FileTransferServiceApi -- list --url http://192.168.1.50:5050 --key super-secret-key
```

This command is the first step on the client side when you want to see what is currently waiting in the API queue.

The command returns one row per file in this format:

```text
<FileName><tab><SizeInBytes><tab><LastModifiedUtc>
```

Example output:

```text
report.pdf	24576	2026-04-13T10:15:30.0000000+00:00
invoice.xml	982	2026-04-13T10:18:04.0000000+00:00
```

Use the exact file name from the first column when you later run `get` or `delete`.

If the queue is empty, the client prints:

```text
No files available.
```

## Download a File

Example command:

```powershell
dotnet run --project .\FileTransferServiceApi -- get --file report.pdf --url http://192.168.1.50:5050 --key super-secret-key --output C:\Temp\report.pdf
```

Parameters:

- `get`: starts download mode
- `--file`: remote file name to fetch from the API
- `--url`: API base URL including port
- `--key`: shared key that must match the API configuration
- `--output`: optional local output path; if omitted, the remote file name is used in the current directory

Where the file is stored on the client:

- if `--output` is provided, the file is written exactly to that path
- if `--output` is omitted, the file is written in the current working directory using the remote file name

Examples:

```powershell
dotnet run --project .\FileTransferServiceApi -- get --file report.pdf --url http://192.168.1.50:5050 --key super-secret-key --output C:\Temp\report.pdf
```

This stores the file at:

```text
C:\Temp\report.pdf
```

```powershell
dotnet run --project .\FileTransferServiceApi -- get --file report.pdf --url http://192.168.1.50:5050 --key super-secret-key
```

If this command is run from `C:\Tools\FileTransfer`, the file is stored at:

```text
C:\Tools\FileTransfer\report.pdf
```

What happens during `get`:

1. The client calls `GET /api/v1/files/{fileName}` on the API.
2. The service returns the file content as base64 together with file metadata.
3. The client creates the local output directory if it does not already exist.
4. The client writes the downloaded file to disk.
5. The file remains in the API storage until you explicitly confirm it with `delete`.

Example output:

```text
Downloaded report.pdf to C:\Temp\report.pdf
```

Recommended use:

- use `get` to copy the file to a local working folder
- verify the file content, size, or downstream processing
- only remove it from the service after the verification is complete

## Delete a File After Successful Validation

Example command:

```powershell
dotnet run --project .\FileTransferServiceApi -- delete --file report.pdf --url http://192.168.1.50:5050 --key super-secret-key
```

Parameters:

- `delete`: removes a previously transferred file from the API
- `--file`: remote file name to delete after verification
- `--url`: API base URL including port
- `--key`: shared key that must match the API configuration

This command is the acknowledgement step. In operational terms, "confirming" a file means that you have successfully downloaded and validated it and can now remove it from the API queue.

Example output:

```text
Delete completed successfully.
{"FileName":"report.pdf","Deleted":true,"DeletedAtUtc":"2026-04-13T10:20:00+00:00"}
```

Important:

- `delete` permanently removes the file from the service storage folder
- if you call `delete` before running `get`, the file will no longer be available for download
- if the file was already removed earlier, the service returns `404 Not Found`

## Recommended Client Workflow

When you work as a client against the API, the normal sequence is:

1. Run `list` to see which files are available.
2. Run `get` for the file you want to process.
3. Validate the downloaded file locally.
4. Run `delete` to acknowledge the file and remove it from the service.

Example session:

```powershell
dotnet run --project .\FileTransferServiceApi -- list --url http://192.168.1.50:5050 --key super-secret-key
dotnet run --project .\FileTransferServiceApi -- get --file report.pdf --url http://192.168.1.50:5050 --key super-secret-key --output C:\Temp\report.pdf
dotnet run --project .\FileTransferServiceApi -- delete --file report.pdf --url http://192.168.1.50:5050 --key super-secret-key
```

This gives you a predictable inbox-style flow:

- `list` shows what is waiting
- `get` copies the file out of the service
- `delete` confirms that the file has been handled

## Example Deployment Setups

### Example 1: Two Computers on the Same Network

- Computer B runs the service on `http://192.168.1.50:5050`
- Computer A sends files to that address
- The same `SharedKey` is used on both sides

### Example 2: API Running as a Windows Service

The application is self-hosted and can run in the background as a service through standard Windows service registration or a wrapper for the published executable.

What matters for functionality is:

- that the process runs continuously
- that the correct `appsettings.json` is used
- that the firewall allows the selected port

## Error Handling

### `401 Unauthorized`

Cause:

- wrong key in `X-Api-Key`
- the client uses a different key than the API

Action:

- verify that `SharedKey` is identical on both sides
- verify that the correct IP is included in `ClientIpAddresses`

### `404 Not Found`

Cause:

- the requested file does not exist in the API storage
- the file has already been deleted after a previous acknowledgement

Action:

- run `list` again to confirm the current file name
- verify that the file has not already been confirmed and deleted

### `400 Bad Request`

Cause:

- `FileName` is missing
- `ContentBase64` is missing
- the base64 string is invalid

Action:

- verify that the client sends a complete payload

### `500 Internal Server Error`

Cause:

- the API has invalid configuration
- `SharedKey` is empty in the API configuration
- write access fails for the storage folder

Action:

- check `appsettings.json`
- check file system permissions for `StoragePath`

## Security Recommendations

- Use a strong and long `SharedKey`.
- Always replace the default value before deployment.
- Open only the port that is required.
- Prefer running the service on an internal network or behind additional network protection.
- Give the service account write access only to the directory that is required.
- Treat IP-based key encryption as an extra layer of protection, not as a replacement for HTTPS/TLS.

## Files in the Project

The most important files for the functionality are:

- `FileTransferServiceApi/Program.cs`
- `FileTransferServiceApi/SenderCommand.cs` containing the `ClientCommand` model
- `FileTransferServiceApi/Services/FileStorageService.cs`
- `FileTransferServiceApi/appsettings.json`

## Tests

The project includes NUnit tests that verify:

- file upload works with the correct key
- an incorrect key is rejected
- command-line arguments for client command mode are parsed correctly

Test project:

- `FileTransferServiceApi.Tests`


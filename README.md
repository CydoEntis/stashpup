# StashPup 🐶

<p align="center">
  <img src="stashpup.png" alt="StashPup Logo" width="200" style="border-radius: 20px;"/>
</p>

**A flexible, provider-agnostic file storage library for .NET with built-in ASP.NET Core integration.**

StashPup simplifies file storage in .NET applications by providing a unified interface for local filesystem, AWS S3, and Azure Blob Storage. It includes advanced features like thumbnail generation, signed URLs, metadata management, and powerful search capabilities.

## ✨ Features

### Core Capabilities
- ✅ **Multi-Provider Support** - Seamlessly switch between Local, S3, and Azure Blob Storage
- ✅ **Railway-Oriented Error Handling** - Type-safe Result<T> pattern instead of exceptions
- ✅ **Advanced Search** - Filter by name patterns, content type, size, dates, and custom metadata
- ✅ **Thumbnail Generation** - Automatic image thumbnail creation with configurable sizes
- ✅ **File Metadata** - Store and query custom key-value metadata with each file
- ✅ **Bulk Operations** - Upload or delete multiple files in a single operation
- ✅ **Signed URLs** - Generate time-limited secure download links
- ✅ **Content Type Detection** - Magic byte detection for accurate MIME types
- ✅ **Pagination** - Built-in paginated listing and search results
- ✅ **SHA-256 Hashing** - Optional file integrity verification

### ASP.NET Core Integration
- ✅ **Dependency Injection** - First-class DI support with fluent configuration
- ✅ **Minimal API Endpoints** - Pre-built REST endpoints for common operations
- ✅ **File Serving Middleware** - Serve local files via HTTP with optional signed URL validation
- ✅ **IFormFile Support** - Direct upload from ASP.NET Core forms
- ✅ **Configuration-Driven** - Configure via appsettings.json or code

## 📦 Installation

```bash
# Core library (required)
dotnet add package StashPup.Core

# Choose your storage provider(s)
dotnet add package StashPup.Storage.Local
dotnet add package StashPup.Storage.S3
dotnet add package StashPup.Storage.Azure

# ASP.NET Core integration (optional)
dotnet add package StashPup.AspNetCore
```

## 🚀 Quick Start

### 1. Basic Setup (Minimal API)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add StashPup with local storage
builder.Services.AddStashPup(stash => stash
    .UseLocalStorage(options =>
    {
        options.BasePath = "./uploads";
        options.BaseUrl = "/files";
        options.MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
        options.AllowedExtensions = [".jpg", ".png", ".pdf"];
    }));

var app = builder.Build();

// Add pre-built endpoints
app.MapStashPupEndpoints("/api/files");

app.Run();
```

### 2. Upload a File

```csharp
app.MapPost("/upload", async (IFormFile file, IFileStorage storage) =>
{
    await using var stream = file.OpenReadStream();
    var result = await storage.SaveAsync(
        stream,
        file.FileName,
        folder: "documents",
        metadata: new Dictionary<string, string>
        {
            ["uploaded-by"] = "john@example.com",
            ["category"] = "invoice"
        });

    return result.Success
        ? Results.Ok(result.Data)
        : Results.BadRequest(result.ErrorMessage);
});
```

### 3. Download a File

```csharp
app.MapGet("/download/{id:guid}", async (Guid id, IFileStorage storage) =>
{
    var result = await storage.GetAsync(id);
    var metadata = await storage.GetMetadataAsync(id);

    return result.Success
        ? Results.File(result.Data!, metadata.Data!.ContentType, metadata.Data.Name)
        : Results.NotFound();
});
```

## 🎯 Core Concepts

### Result Pattern

StashPup uses a railway-oriented programming approach. All operations return `Result<T>`:

```csharp
public class Result<T>
{
    public bool Success { get; }
    public T? Data { get; }
    public string? ErrorMessage { get; }
    public string? ErrorCode { get; }
}
```

**Usage:**

```csharp
var result = await storage.SaveAsync(stream, "photo.jpg");

if (result.Success)
{
    Console.WriteLine($"File saved with ID: {result.Data!.Id}");
}
else
{
    Console.WriteLine($"Error: {result.ErrorMessage} (Code: {result.ErrorCode})");
}

// Or use implicit bool conversion
if (result)
{
    // Success!
}
```

### File Records

Every stored file has an associated `FileRecord` with rich metadata:

```csharp
public class FileRecord
{
    public Guid Id { get; set; }                    // Unique identifier
    public string Name { get; set; }                // Current name
    public string OriginalName { get; set; }        // Original upload name
    public string Extension { get; set; }           // File extension
    public string ContentType { get; set; }         // MIME type
    public long SizeBytes { get; set; }             // File size
    public DateTime CreatedAtUtc { get; set; }      // Creation timestamp
    public DateTime UpdatedAtUtc { get; set; }      // Last modified
    public string? Hash { get; set; }               // SHA-256 hash (optional)
    public string? Folder { get; set; }             // Folder/prefix
    public string StoragePath { get; set; }         // Provider-specific path
    public Dictionary<string, string>? Metadata { get; set; } // Custom metadata
}
```

## 🔧 Storage Providers

### Local Filesystem

```csharp
builder.Services.AddStashPup(stash => stash
    .UseLocalStorage(options =>
    {
        options.BasePath = "./uploads";
        options.BaseUrl = "/files";
        options.OverwriteExisting = false;
        options.AutoCreateDirectories = true;
        options.EnableSignedUrls = true;
        options.SigningKey = "your-secret-key-here";
    }));

// Enable file serving middleware
app.UseStashPup();
```

### AWS S3

```csharp
builder.Services.AddStashPup(stash => stash
    .UseS3(options =>
    {
        options.BucketName = "my-bucket";
        options.Region = "us-east-1";
        options.AccessKeyId = "AKIA...";
        options.SecretAccessKey = "secret";
        options.PublicRead = false;
        options.EnableEncryption = true;
        options.StorageClass = "STANDARD";
    }));
```

### S3-Compatible Services (MinIO, Garage, etc.)

```csharp
builder.Services.AddStashPup(stash => stash
    .UseS3(options =>
    {
        options.BucketName = "my-bucket";
        options.Region = "garage";              // Your service's region
        options.AccessKeyId = "AKIA...";
        options.SecretAccessKey = "secret";
        options.ServiceUrl = "https://s3.example.com"; // Custom endpoint
        options.ForcePathStyle = true;          // Required for most S3-compatible services
    }));
```

### Azure Blob Storage

```csharp
builder.Services.AddStashPup(stash => stash
    .UseAzureBlob(options =>
    {
        options.ConnectionString = "DefaultEndpointsProtocol=https;...";
        options.ContainerName = "uploads";
        options.AccessTier = "Hot";
        options.PublicAccess = false;
        options.CreateContainerIfNotExists = true;
    }));
```

### Configuration via appsettings.json

```json
{
  "StashPup": {
    "Provider": "S3",
    "S3": {
      "BucketName": "my-bucket",
      "Region": "us-east-1",
      "EnableEncryption": true
    }
  }
}
```

```csharp
builder.Services.AddStashPup(builder.Configuration);
```

## 📚 Common Operations

### Search Files

```csharp
var searchParams = new SearchParameters
{
    NamePattern = "invoice*.pdf",
    ContentType = "application/pdf",
    MinSizeBytes = 1024,
    CreatedAfter = DateTime.UtcNow.AddDays(-30),
    Metadata = new Dictionary<string, string>
    {
        ["category"] = "invoice"
    },
    SortBy = SearchSortField.CreatedAt,
    SortDirection = SearchSortDirection.Descending,
    Page = 1,
    PageSize = 20
};

var result = await storage.SearchAsync(searchParams);
```

### Generate Thumbnails

```csharp
var thumbnailResult = await storage.GetThumbnailAsync(
    fileId,
    ThumbnailSize.Medium);

if (thumbnailResult.Success)
{
    return Results.File(thumbnailResult.Data!, "image/jpeg");
}
```

### Move Files Between Folders

```csharp
var result = await storage.MoveAsync(
    id: fileId,
    newFolder: "archive/2024");
```

### Copy Files

```csharp
var result = await storage.CopyAsync(
    id: fileId,
    newFolder: "backups");

// Returns a new FileRecord with a new ID
Console.WriteLine($"Copied to new ID: {result.Data!.Id}");
```

### Bulk Operations

```csharp
// Bulk upload
var items = new[]
{
    new BulkSaveItem(stream1, "file1.jpg", "images"),
    new BulkSaveItem(stream2, "file2.jpg", "images"),
    new BulkSaveItem(stream3, "file3.jpg", "images")
};

var results = await storage.BulkSaveAsync(items);

// Bulk delete
var idsToDelete = new[] { id1, id2, id3 };
await storage.BulkDeleteAsync(idsToDelete);

// Bulk move to new folder
var idsToMove = new[] { id1, id2, id3 };
var movedFiles = await storage.BulkMoveAsync(idsToMove, "archive/2024");
```

### Folder Operations

**StashPup uses a fully virtual folder model** - folders are just path prefixes on files. This means:
- ✅ Folders are created automatically when you upload files to a path
- ✅ StashPup handles all folder operations (list, move, delete, search)
- ✅ Your app manages "empty folder" state in its own database if needed
- ✅ Works consistently across Local, S3, and Azure storage

```csharp
// List all unique folder paths (folders that have files)
var foldersResult = await storage.ListFoldersAsync();
foreach (var folder in foldersResult.Data!)
{
    Console.WriteLine($"Folder: {folder}");
}

// List immediate children of a parent folder
var childFolders = await storage.ListFoldersAsync(parentFolder: "projects");

// Delete folder and all contents (recursive)
var deleteResult = await storage.DeleteFolderAsync(
    folder: "temp/uploads",
    recursive: true);
Console.WriteLine($"Deleted {deleteResult.Data} files");

// Delete only files in exact folder (non-recursive)
var deleteExact = await storage.DeleteFolderAsync(
    folder: "temp",
    recursive: false);
```

#### Implementing Empty Folders in Your App

**StashPup doesn't manage empty folders** - that's your app's responsibility! Here's how:

```csharp
// 1. Create database table for empty folders
public class EmptyFolder
{
    public string Path { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UserId { get; set; }
}

// 2. When user creates folder
db.EmptyFolders.Add(new EmptyFolder { Path = "Photos/2024", UserId = currentUserId });
await db.SaveChangesAsync();

// 3. Display merged list of folders
var realFolders = await storage.ListFoldersAsync();
var emptyFolders = await db.EmptyFolders.Where(f => f.UserId == currentUserId).ToListAsync();
var allFolders = realFolders.Data.Union(emptyFolders.Select(f => f.Path));

// 4. When file uploaded to empty folder, remove it
if (await db.EmptyFolders.AnyAsync(f => f.Path == uploadFolder))
{
    db.EmptyFolders.RemoveRange(db.EmptyFolders.Where(f => f.Path == uploadFolder));
    await db.SaveChangesAsync();
}
```

This keeps **clean separation**: StashPup handles files, your app handles empty folder UI!

### Advanced Folder Search

```csharp
// Search with enhanced folder filtering
var searchParams = new SearchParameters
{
    FolderStartsWith = "projects/2024", // Match folders starting with prefix
    IncludeSubfolders = true,          // Include nested subfolders (default)
    Page = 1,
    PageSize = 50
};

var result = await storage.SearchAsync(searchParams);

// Search only in immediate folder (no subfolders)
var exactFolderSearch = new SearchParameters
{
    Folder = "documents",
    IncludeSubfolders = false, // Only files in "documents", not "documents/2024"
};
```

## 🔐 Signed URLs

### Generate Signed URLs (S3 & Azure)

```csharp
// Time-limited download URL
var urlResult = await storage.GetSignedUrlAsync(
    fileId,
    expiry: TimeSpan.FromHours(1));

if (urlResult.Success)
{
    // Share this URL with users
    var downloadUrl = urlResult.Data;
}
```

### Local Storage Signed URLs

```csharp
// Configure
options.EnableSignedUrls = true;
options.SigningKey = "your-secret-key";

// Generate
var url = storage.GetSignedUrl(fileId, TimeSpan.FromMinutes(30));
// Returns: /files/{id}?expires=...&signature=...

// Middleware automatically validates signatures
app.UseStashPup();
```

## 🎨 Validation & Security

### Configure File Restrictions

```csharp
options.MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB
options.AllowedExtensions = [".jpg", ".png", ".gif", ".webp"];
options.AllowedContentTypes = ["image/*"]; // Supports wildcards
options.ComputeHash = true; // Enable SHA-256 hashing
```

### Content Type Detection

StashPup automatically detects content types using magic byte analysis:

```csharp
// Detects based on file signature, not just extension
var result = await storage.SaveAsync(stream, "photo.jpg");
// result.Data.ContentType = "image/jpeg" (verified via magic bytes)
```

## 🌐 ASP.NET Core Integration

### Pre-built Endpoints

```csharp
app.MapStashPupEndpoints("/api/files", options =>
{
    options.RequireAuthorization = true;
    options.EnableUpload = true;
    options.EnableDownload = true;
    options.EnableDelete = true;
    options.EnableMetadata = true;
    options.EnableList = false; // Disabled by default for security
    options.EnableFolderList = false; // Disabled by default for security
    options.EnableFolderDelete = true;
    options.EnableBulkMove = true;
});
```

**Available Endpoints:**
- `POST /api/files` - Upload file
- `GET /api/files/{id}` - Download file
- `DELETE /api/files/{id}` - Delete file
- `GET /api/files/{id}/metadata` - Get metadata
- `GET /api/files?folder=...&page=1&pageSize=20` - List files (opt-in)
- `GET /api/files/folders?parent=...` - List all folder paths (opt-in)
- `DELETE /api/files/folders/{path}?recursive=true` - Delete folder and contents
- `POST /api/files/bulk-move` - Move multiple files to new folder

### Custom Endpoints

```csharp
app.MapPost("/api/upload-avatar", async (
    IFormFile file,
    IFileStorage storage,
    ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    await using var stream = file.OpenReadStream();
    var result = await storage.SaveAsync(
        stream,
        file.FileName,
        folder: $"avatars/{userId}",
        metadata: new Dictionary<string, string>
        {
            ["user-id"] = userId!,
            ["upload-date"] = DateTime.UtcNow.ToString("O")
        });

    return result.Success
        ? Results.Ok(new { fileId = result.Data!.Id, url = $"/files/{result.Data.Id}" })
        : Results.BadRequest(new { error = result.ErrorMessage });
});
```

## 📖 Advanced Usage

### Custom Naming Strategy

```csharp
options.NamingStrategy = (originalFileName) =>
{
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var extension = Path.GetExtension(originalFileName);
    return $"{timestamp}_{Guid.NewGuid()}{extension}";
};
```

### Custom Subfolder Strategy

```csharp
options.SubfolderStrategy = (fileRecord) =>
{
    // Organize by year/month
    var now = DateTime.UtcNow;
    return $"{now.Year}/{now.Month:D2}";
};
```

### Direct IFileStorage Usage

```csharp
public class DocumentService
{
    private readonly IFileStorage _storage;

    public DocumentService(IFileStorage storage)
    {
        _storage = storage;
    }

    public async Task<Result<FileRecord>> SaveInvoice(
        Stream pdfStream,
        string customerId)
    {
        return await _storage.SaveAsync(
            content: pdfStream,
            fileName: $"invoice_{customerId}.pdf",
            folder: $"invoices/{DateTime.UtcNow.Year}",
            metadata: new Dictionary<string, string>
            {
                ["customer-id"] = customerId,
                ["document-type"] = "invoice",
                ["processed"] = "false"
            });
    }
}
```

## 🧪 Testing

StashPup's interface-based design makes testing easy:

```csharp
public class MockFileStorage : IFileStorage
{
    private readonly Dictionary<Guid, FileRecord> _files = new();

    public Task<Result<FileRecord>> SaveAsync(...)
    {
        var record = new FileRecord { Id = Guid.NewGuid(), ... };
        _files[record.Id] = record;
        return Task.FromResult(Result<FileRecord>.Ok(record));
    }

    // Implement other methods...
}
```

## 📊 Performance Tips

1. **Use Bulk Operations** for multiple files to reduce round-trips
2. **Enable Hashing Selectively** - only when integrity verification is needed
3. **Cache Thumbnails** - they're automatically cached by storage providers
4. **Use Pagination** - don't load all files at once
5. **Consider Storage Classes** - use S3 GLACIER or Azure Cool tier for archives
6. **Stream Large Files** - don't buffer entire files in memory

## 🔍 Error Handling

```csharp
var result = await storage.SaveAsync(stream, fileName);

if (!result.Success)
{
    switch (result.ErrorCode)
    {
        case FileStorageErrors.MaxFileSizeExceeded:
            return Results.BadRequest("File too large");

        case FileStorageErrors.InvalidFileExtension:
            return Results.BadRequest("File type not allowed");

        case FileStorageErrors.FileAlreadyExists:
            return Results.Conflict("File already exists");

        default:
            logger.LogError("Upload failed: {ErrorMessage}", result.ErrorMessage);
            return Results.StatusCode(500);
    }
}
```

## 📄 License

MIT License - see LICENSE file for details

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## 📚 Additional Documentation

- [Avatar Integration](AVATAR_INTEGRATION.md) - Complete guide for avatar uploads in other projects
- [Git Commit Guide](GIT_COMMIT.md) - How to commit changes and publish packages
- [Start Here](START_HERE.md) - Quick overview and next steps

## 🆘 Support

For questions, issues, or feature requests, please open an issue on GitHub.

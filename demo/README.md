# StashPup Demo Application

A working demo of the StashPup file storage library showcasing all major features including the NEW v0.2.0 folder operations.

## Features Demonstrated

### Core Features (v0.1.0)
- ✅ **File Upload** - Upload files with validation (size, extension, content type)
- ✅ **File Download** - Download files via direct links
- ✅ **File Management** - List, view metadata, and delete files
- ✅ **Folder Organization** - Organize files into folders
- ✅ **API Endpoints** - RESTful API for file operations
- ✅ **Error Handling** - Proper error messages and validation

### NEW Folder Operations (v0.2.0)
- ✨ **Create Empty Folders** - Create folders that don't have files yet
- ✨ **Folder Navigation** - Browse through folder hierarchies with breadcrumbs
- ✨ **List Folders** - See all folders (both with files and empty)
- ✨ **Bulk Move** - Move multiple files to a different folder at once
- ✨ **Delete Folder** - Delete entire folders and their contents recursively
- ✨ **Bulk Delete** - Delete multiple files at once
- ✨ **Subfolder Filtering** - Toggle between including/excluding subfolders in search

> **How Empty Folders Work:**  
> **StashPup** handles files and their folder paths - folders are just string prefixes (virtual folders).  
> **Demo App** tracks empty folders in memory using a `HashSet<string>`.  
> **In Your Real App**, store empty folders in your database table.  
> 
> When you upload a file to an empty folder, it becomes a "real" folder in StashPup, and the demo removes it from the empty folders list. This is the **clean separation of concerns** approach!

## Quick Start

1. **Run the demo:**
   ```bash
   cd demo
   dotnet run
   ```
   
   Or run with a specific port:
   ```bash
   dotnet run --urls "https://localhost:5001;http://localhost:5000"
   ```

2. **Open your browser:**
   - Navigate to the URL shown in console (typically `https://localhost:5001` or `http://localhost:5000`)
   - Click "Go to File Manager" or use the "Files" navigation link

3. **Test the features:**
   - Click "New Folder" to create empty folders for organization
   - Upload various file types (images, PDFs, documents)
   - Try organizing files into folders by specifying folder paths during upload
   - Navigate through folders using breadcrumbs
   - Select multiple files and move them to a different folder
   - Delete entire folders and their contents
   - Search with subfolder filtering
   - Download files by clicking the download buttons
   - Delete individual files or bulk delete

## Configuration

The demo is configured in `Program.cs` with:

- **Storage Provider:** Local filesystem
- **Upload Directory:** `./uploads` (created automatically)
- **Max File Size:** 50MB
- **Allowed Extensions:** `.jpg`, `.jpeg`, `.png`, `.gif`, `.pdf`, `.txt`, `.doc`, `.docx`
- **Allowed Content Types:** `image/*`, `application/pdf`, `text/*`, `application/msword`
- **Empty Folders:** Tracked in-memory (use a database in production)

## API Endpoints

The demo exposes RESTful API endpoints at `/api/files`:

### Core Endpoints
- `POST /api/files/` - Upload file
- `GET /api/files/{id}` - Download file
- `DELETE /api/files/{id}` - Delete file
- `GET /api/files/{id}/metadata` - Get file metadata
- `GET /api/files/?folder=...&page=1&pageSize=20` - List files (disabled by default for security)

### NEW Folder Operation Endpoints (v0.2.0)
- `GET /api/files/folders?parent=...` - List all unique folder paths
- `DELETE /api/files/folders/{path}?recursive=true` - Delete folder and contents
- `POST /api/files/bulk-move` - Move multiple files to new folder

### Testing API with curl

```bash
# Upload a file
curl -X POST http://localhost:5037/api/files/ \
  -F "file=@photo.jpg" \
  -F "folder=test"

# Get file metadata
curl http://localhost:5037/api/files/{file-id}/metadata

# Download file
curl http://localhost:5037/api/files/{file-id} --output downloaded.jpg

# Delete file
curl -X DELETE http://localhost:5037/api/files/{file-id}
```

## Switching Storage Providers

To test different storage providers, modify `Program.cs`:

### Azure Blob Storage
```csharp
builder.Services.AddStashPup(stash => stash
    .UseAzureBlob(options =>
    {
        options.ConnectionString = "your-connection-string";
        options.ContainerName = "files";
        options.MaxFileSizeBytes = 50 * 1024 * 1024;
    }));
```

### AWS S3
```csharp
builder.Services.AddStashPup(stash => stash
    .UseS3(options =>
    {
        options.BucketName = "your-bucket";
        options.Region = "us-east-1";
        options.MaxFileSizeBytes = 50 * 1024 * 1024;
    }));
```

## Project Structure

- `Pages/Files.cshtml` - Main file management UI
- `Pages/Index.cshtml` - Landing page with feature overview
- `Program.cs` - App configuration and StashPup setup
- `uploads/` - Local storage directory (created when first file is uploaded)
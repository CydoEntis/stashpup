using StashPup.Core.Interfaces;
using StashPup.Core.Models;

namespace StashPup.AspNetCore.Features.Folders;

/// <summary>
/// Handler for folder operations.
/// </summary>
public static class FoldersHandler
{
    /// <summary>
    /// Lists all unique folder paths in storage.
    /// </summary>
    public static async Task<FoldersListResponse> HandleListFolders(
        IFileStorage storage,
        string? parentFolder,
        CancellationToken ct)
    {
        var result = await storage.ListFoldersAsync(parentFolder, ct);
        
        if (!result.Success)
        {
            return new FoldersListResponse
            {
                Success = false,
                ErrorMessage = result.ErrorMessage,
                ErrorCode = result.ErrorCode,
                Folders = Array.Empty<string>()
            };
        }

        return new FoldersListResponse
        {
            Success = true,
            Folders = result.Data!.ToArray()
        };
    }

    /// <summary>
    /// Deletes a folder and all its contents.
    /// </summary>
    public static async Task<FolderDeleteResponse> HandleDeleteFolder(
        IFileStorage storage,
        string folderPath,
        bool recursive,
        CancellationToken ct)
    {
        var result = await storage.DeleteFolderAsync(folderPath, recursive, ct);
        
        if (!result.Success)
        {
            return new FolderDeleteResponse
            {
                Success = false,
                ErrorMessage = result.ErrorMessage,
                ErrorCode = result.ErrorCode,
                DeletedCount = 0
            };
        }

        return new FolderDeleteResponse
        {
            Success = true,
            DeletedCount = result.Data,
            FolderPath = folderPath
        };
    }

    /// <summary>
    /// Moves multiple files to a new folder.
    /// </summary>
    public static async Task<BulkMoveResponse> HandleBulkMove(
        IFileStorage storage,
        Guid[] fileIds,
        string newFolder,
        CancellationToken ct)
    {
        var result = await storage.BulkMoveAsync(fileIds, newFolder, ct);
        
        if (!result.Success)
        {
            return new BulkMoveResponse
            {
                Success = false,
                ErrorMessage = result.ErrorMessage,
                ErrorCode = result.ErrorCode,
                MovedFiles = Array.Empty<FileRecord>()
            };
        }

        return new BulkMoveResponse
        {
            Success = true,
            MovedFiles = result.Data!.ToArray(),
            NewFolder = newFolder
        };
    }

    /// <summary>
    /// Creates a new empty folder.
    /// </summary>
    public static async Task<CreateFolderResponse> HandleCreateFolder(
        IFileStorage storage,
        string folderPath,
        CancellationToken ct)
    {
        var result = await storage.CreateFolderAsync(folderPath, ct);
        
        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to create folder");
        }

        return new CreateFolderResponse(
            result.Data!,
            $"Folder '{result.Data}' created successfully"
        );
    }
}

/// <summary>
/// Response for listing folders.
/// </summary>
public class FoldersListResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string[] Folders { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Response for deleting a folder.
/// </summary>
public class FolderDeleteResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public int DeletedCount { get; set; }
    public string? FolderPath { get; set; }
}

/// <summary>
/// Response for bulk moving files.
/// </summary>
public class BulkMoveResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public FileRecord[] MovedFiles { get; set; } = Array.Empty<FileRecord>();
    public string? NewFolder { get; set; }
}

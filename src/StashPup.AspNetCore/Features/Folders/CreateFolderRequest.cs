namespace StashPup.AspNetCore.Features.Folders;

/// <summary>
/// Request model for creating a new folder.
/// </summary>
/// <param name="FolderPath">The path of the folder to create (e.g., "documents/2024").</param>
public record CreateFolderRequest(string FolderPath);

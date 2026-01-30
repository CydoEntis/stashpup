namespace StashPup.AspNetCore.Features.Folders;

/// <summary>
/// Response model for folder creation.
/// </summary>
/// <param name="FolderPath">The created folder path.</param>
/// <param name="Message">Success message.</param>
public record CreateFolderResponse(string FolderPath, string Message);

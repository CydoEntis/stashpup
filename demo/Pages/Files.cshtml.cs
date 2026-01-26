using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StashPup.Core.Interfaces;
using StashPup.Core.Models;

namespace StashPup.Demo.Pages;

public class FilesModel : PageModel
{
    private readonly IFileStorage _fileStorage;

    public FilesModel(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    // File list and pagination
    public List<FileRecord> Files { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    // Search and filter parameters
    public string? SearchQuery { get; set; }
    public string? Folder { get; set; }
    public string? ContentType { get; set; }
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public string? Extension { get; set; }

    // Sorting
    public string SortBy { get; set; } = "CreatedAt";
    public string SortDirection { get; set; } = "Descending";

    // View mode
    public string ViewMode { get; set; } = "list"; // list or grid

    // Messages
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(
        string? searchQuery = null,
        string? folder = null,
        string? contentType = null,
        long? minSize = null,
        long? maxSize = null,
        string? extension = null,
        string sortBy = "CreatedAt",
        string sortDirection = "Descending",
        string viewMode = "list",
        int page = 1,
        int pageSize = 20)
    {
        // Store parameters for view
        SearchQuery = searchQuery;
        Folder = folder;
        ContentType = contentType;
        MinSize = minSize;
        MaxSize = maxSize;
        Extension = extension;
        SortBy = sortBy;
        SortDirection = sortDirection;
        ViewMode = viewMode;
        CurrentPage = page;
        PageSize = pageSize;

        // Build search parameters
        var searchParams = new SearchParameters
        {
            NamePattern = searchQuery,
            Folder = folder,
            ContentType = contentType,
            MinSizeBytes = minSize,
            MaxSizeBytes = maxSize,
            Extension = extension,
            SortBy = ParseSortField(sortBy),
            SortDirection = ParseSortDirection(sortDirection),
            Page = page,
            PageSize = pageSize
        };

        // Execute search
        var result = await _fileStorage.SearchAsync(searchParams);

        if (result.Success)
        {
            Files = result.Data!.Items.ToList();
            TotalItems = result.Data.TotalItems;
            TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile file, string? folder = null)
    {
        if (file == null || file.Length == 0)
        {
            ErrorMessage = "Please select a file to upload.";
            return Page();
        }

        await using var stream = file.OpenReadStream();
        var result = await _fileStorage.SaveAsync(stream, file.FileName, folder, null);
        
        if (result.Success)
        {
            SuccessMessage = $"File '{result.Data!.Name}' uploaded successfully!";
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage(new { folder });
    }

    public async Task<IActionResult> OnPostBulkUploadAsync(List<IFormFile> files, string? folder = null)
    {
        if (files == null || !files.Any())
        {
            ErrorMessage = "Please select at least one file to upload.";
            return Page();
        }

        var bulkItems = files.Select(f => new BulkSaveItem(
            f.OpenReadStream(),
            f.FileName
        )).ToList();

        var result = await _fileStorage.BulkSaveAsync(bulkItems, folder);

        if (result.Success)
        {
            SuccessMessage = $"{result.Data!.Count} file(s) uploaded successfully!";
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
        }

        return RedirectToPage(new { folder });
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid id)
    {
        var result = await _fileStorage.GetAsync(id);
        if (!result.Success)
            return NotFound();

        var metadata = await _fileStorage.GetMetadataAsync(id);
        return File(result.Data!, metadata.Data?.ContentType ?? "application/octet-stream", metadata.Data?.Name);
    }

    public async Task<IActionResult> OnGetThumbnailAsync(Guid id, string size = "medium")
    {
        var thumbnailSize = size.ToLower() switch
        {
            "small" => ThumbnailSize.Small,
            "large" => ThumbnailSize.Large,
            _ => ThumbnailSize.Medium
        };

        var result = await _fileStorage.GetThumbnailAsync(id, thumbnailSize);
        if (!result.Success)
            return NotFound();

        return File(result.Data!, "image/jpeg");
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, string? folder = null)
    {
        var result = await _fileStorage.DeleteAsync(id);
        if (result.Success && result.Data)
        {
            SuccessMessage = "File deleted successfully.";
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to delete file.";
        }

        return RedirectToPage(new { folder });
    }

    private SearchSortField ParseSortField(string sortBy)
    {
        return sortBy switch
        {
            "Name" => SearchSortField.Name,
            "Size" => SearchSortField.Size,
            "CreatedAt" => SearchSortField.CreatedAt,
            "UpdatedAt" => SearchSortField.UpdatedAt,
            "Extension" => SearchSortField.Extension,
            "ContentType" => SearchSortField.ContentType,
            _ => SearchSortField.CreatedAt
        };
    }

    private SearchSortDirection ParseSortDirection(string direction)
    {
        return direction?.ToLower() == "ascending"
            ? SearchSortDirection.Ascending
            : SearchSortDirection.Descending;
    }
}

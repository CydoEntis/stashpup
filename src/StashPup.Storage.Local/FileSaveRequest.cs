namespace StashPup.Storage.Local;

public class FileSaveRequest
{
    public Stream Content { get; set; } = default!;
    public string OriginalFileName { get; set; } = default!;
    public string? FinalFileName { get; set; }
    public LocalStorageOptions Options { get; set; } = default!;
    public string SubFolder { get; set; } = string.Empty;
    public string? Hash { get; set; }
    public string? ContentType { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
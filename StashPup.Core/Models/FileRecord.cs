namespace StashPup.Core.Models;

public class FileRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Extensions { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Hash { get; set; }
}
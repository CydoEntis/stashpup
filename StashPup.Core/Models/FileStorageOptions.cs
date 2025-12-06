namespace StashPup.Core.Models;

public class FileStorageOptions
{
    public long? MaxFileSizeBytes { get; set; }
    public List<string> AllowableExtensions { get; set; } = [];
    public bool HashFileName { get; set; }
    public Func<string, string>? NamingStrategy { get; set; }
}
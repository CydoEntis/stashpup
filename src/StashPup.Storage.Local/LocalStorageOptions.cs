using StashPup.Core.Models;

namespace StashPup.Storage.Local;

public class LocalStorageOptions : FileStorageOptions
{
    public string BasePath { get; set; } = "/uploads";
    public bool OverwriteExisting { get; set; } = false;
    public bool AutoCreateDirectories { get; set; } = true;
    public Func<FileRecord, string>? SubFolderStrategy { get; set; }
}


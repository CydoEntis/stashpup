namespace StashPup.Core.Models;

/// <summary>
/// Standard thumbnail sizes for image files.
/// </summary>
public enum ThumbnailSize
{
    /// <summary>Small thumbnail: 150x150 pixels (max dimension).</summary>
    Small = 150,

    /// <summary>Medium thumbnail: 300x300 pixels (max dimension).</summary>
    Medium = 300,

    /// <summary>Large thumbnail: 600x600 pixels (max dimension).</summary>
    Large = 600
}

/// <summary>
/// Options for thumbnail generation.
/// </summary>
public class ThumbnailOptions
{
    /// <summary>
    /// Thumbnail size to generate.
    /// </summary>
    public ThumbnailSize Size { get; set; } = ThumbnailSize.Medium;

    /// <summary>
    /// JPEG quality (1-100) for thumbnail compression.
    /// </summary>
    public int Quality { get; set; } = 85;

    /// <summary>
    /// Whether to maintain aspect ratio (true) or crop to exact size (false).
    /// </summary>
    public bool MaintainAspectRatio { get; set; } = true;

    /// <summary>
    /// Background color for letterboxing when maintaining aspect ratio.
    /// Format: "#RRGGBB" or color name like "white", "transparent".
    /// </summary>
    public string BackgroundColor { get; set; } = "white";
}
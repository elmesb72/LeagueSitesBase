using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

/// <summary>
/// Output format for processed image uploads. Shared across Social Links
/// icons, league logo / favicon, and (future) team logos.
/// </summary>
public enum ImageOutputFormat
{
    Webp,
    Png
}

/// <summary>
/// Result of an image processing attempt. Controllers call
/// ToActionResult() or inspect Success/ErrorMessage directly.
/// </summary>
public record ImageProcessingResult(bool Success, string? ErrorMessage = null)
{
    public static ImageProcessingResult Ok() => new(true);
    public static ImageProcessingResult Error(string message) => new(false, message);
}

/// <summary>
/// Shared pipeline for converting user-uploaded images to a target format
/// and size on disk. Handles SVG rejection (ImageSharp can't rasterize it)
/// and maps ImageSharp exceptions to caller-friendly error messages.
/// </summary>
public static class ImageProcessor
{
    public static async Task<ImageProcessingResult> ConvertAsync(
        IFormFile file,
        string destinationPath,
        ImageOutputFormat format,
        int maxDimension,
        int quality = 90)
    {
        if (file is null || file.Length == 0)
            return ImageProcessingResult.Error("No file uploaded.");

        if (LooksLikeSvg(file))
            return ImageProcessingResult.Error(
                "SVG uploads are not supported. Please upload a raster image (PNG, JPG, or WebP).");

        // Ensure the destination directory exists.
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        try
        {
            using var source = file.OpenReadStream();
            using var image = await Image.LoadAsync(source);

            // Downscale to fit within maxDimension x maxDimension while
            // preserving aspect ratio. Skip the resize entirely when the
            // source is already within the cap — ImageSharp's ResizeMode.Max
            // otherwise upscales to fit the target box, which we don't want.
            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(maxDimension, maxDimension),
                    Mode = ResizeMode.Max
                }));
            }

            await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
            switch (format)
            {
                case ImageOutputFormat.Webp:
                    await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = quality });
                    break;
                case ImageOutputFormat.Png:
                    await image.SaveAsPngAsync(output, new PngEncoder());
                    break;
            }
        }
        catch (UnknownImageFormatException)
        {
            return ImageProcessingResult.Error("Unsupported or unrecognized image format.");
        }
        catch (InvalidImageContentException)
        {
            return ImageProcessingResult.Error("Image file is corrupted or could not be decoded.");
        }

        return ImageProcessingResult.Ok();
    }

    /// <summary>
    /// Heuristic SVG check: XML/SVG content is text-based and ImageSharp
    /// throws a generic decode error if we try to load it, so we catch it
    /// up-front for a clearer user-facing message.
    /// </summary>
    static bool LooksLikeSvg(IFormFile file)
    {
        var type = file.ContentType ?? string.Empty;
        if (type.Contains("svg", StringComparison.OrdinalIgnoreCase)) return true;

        var name = file.FileName ?? string.Empty;
        return name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace LeagueSitesBackend.Tests;

public class ImageProcessorTests : IDisposable
{
    readonly string _workDir;

    public ImageProcessorTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "ImageProcessorTests-" + Guid.NewGuid());
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDir, recursive: true); }
        catch { /* best-effort cleanup */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>Creates an in-memory IFormFile backed by a real PNG of the given size.</summary>
    static IFormFile MakePngFormFile(int width, int height, string filename = "source.png")
    {
        var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(width, height))
        {
            image.SaveAsPng(stream, new PngEncoder());
        }
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", filename)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    static IFormFile MakeSvgFormFile()
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"));
        return new FormFile(stream, 0, stream.Length, "file", "icon.svg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/svg+xml"
        };
    }

    [Fact]
    public async Task ConvertAsync_WebpOutput_ProducesValidWebp()
    {
        var src = MakePngFormFile(200, 200);
        var dest = Path.Combine(_workDir, "out.webp");

        var result = await ImageProcessor.ConvertAsync(
            src, dest, ImageOutputFormat.Webp, 128);

        result.Success.Should().BeTrue();
        File.Exists(dest).Should().BeTrue();

        // Round-trip: ImageSharp should re-load what we wrote, and it
        // should be downscaled to fit within 128x128.
        using var img = await Image.LoadAsync(dest);
        img.Width.Should().BeLessThanOrEqualTo(128);
        img.Height.Should().BeLessThanOrEqualTo(128);
    }

    [Fact]
    public async Task ConvertAsync_PngOutput_ProducesValidPng()
    {
        var src = MakePngFormFile(96, 96);
        var dest = Path.Combine(_workDir, "out.png");

        var result = await ImageProcessor.ConvertAsync(
            src, dest, ImageOutputFormat.Png, 64);

        result.Success.Should().BeTrue();
        using var img = await Image.LoadAsync(dest);
        img.Width.Should().BeLessThanOrEqualTo(64);
        img.Height.Should().BeLessThanOrEqualTo(64);
    }

    [Fact]
    public async Task ConvertAsync_SmallerThanMax_NoUpscaling()
    {
        var src = MakePngFormFile(32, 32);
        var dest = Path.Combine(_workDir, "out.webp");

        await ImageProcessor.ConvertAsync(src, dest, ImageOutputFormat.Webp, 128);

        using var img = await Image.LoadAsync(dest);
        img.Width.Should().Be(32);
        img.Height.Should().Be(32);
    }

    [Fact]
    public async Task ConvertAsync_SvgByContentType_Rejected()
    {
        var src = MakeSvgFormFile();
        var dest = Path.Combine(_workDir, "out.webp");

        var result = await ImageProcessor.ConvertAsync(
            src, dest, ImageOutputFormat.Webp, 128);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SVG");
        File.Exists(dest).Should().BeFalse();
    }

    [Fact]
    public async Task ConvertAsync_EmptyFile_Rejected()
    {
        var empty = new FormFile(new MemoryStream(), 0, 0, "file", "empty.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
        var dest = Path.Combine(_workDir, "out.webp");

        var result = await ImageProcessor.ConvertAsync(
            empty, dest, ImageOutputFormat.Webp, 128);

        result.Success.Should().BeFalse();
        File.Exists(dest).Should().BeFalse();
    }

    [Fact]
    public async Task ConvertAsync_CorruptData_Rejected()
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("not an image"));
        var src = new FormFile(stream, 0, stream.Length, "file", "broken.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
        var dest = Path.Combine(_workDir, "out.webp");

        var result = await ImageProcessor.ConvertAsync(
            src, dest, ImageOutputFormat.Webp, 128);

        result.Success.Should().BeFalse();
    }
}

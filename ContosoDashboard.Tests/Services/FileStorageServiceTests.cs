using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public class FileStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_UsesGuidPathAndCanCleanUp()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var service = CreateService(root);
        await using var input = new MemoryStream("hello"u8.ToArray());

        var stored = await service.SaveAsync(input, "4", null, ".pdf");

        Assert.Matches(@"^4/personal/[0-9a-f]{32}\.pdf$", stored.RelativePath);
        Assert.StartsWith(Path.GetFullPath(root), service.GetAbsolutePath(stored.RelativePath), StringComparison.OrdinalIgnoreCase);
        await service.DeleteAsync(stored.RelativePath);
        Assert.False(File.Exists(service.GetAbsolutePath(stored.RelativePath)));
        Directory.Delete(root, true);
    }

    [Fact]
    public void GetAbsolutePath_RejectsTraversal()
    {
        var service = CreateService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Throws<InvalidDataException>(() => service.GetAbsolutePath("../outside.txt"));
    }

    [Fact]
    public async Task SaveAsync_RejectsOversizedContent()
    {
        var service = CreateService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        await using var input = new MemoryStream(new byte[11]);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.SaveAsync(input, "4", 1, ".pdf"));
    }

    [Fact]
    public async Task Scanner_FailsClosedForUnavailableContent()
    {
        var scanner = new TrainingFileScanService();
        var result = await scanner.ScanAsync(Stream.Null, "file.pdf");
        Assert.Equal(FileScanStatus.Unavailable, result.Status);
    }

    private static FileStorageService CreateService(string root) =>
        new(Options.Create(new DocumentStorageOptions
        {
            UploadRoot = root,
            MaxFileSizeBytes = 10
        }), new TestEnvironment());

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

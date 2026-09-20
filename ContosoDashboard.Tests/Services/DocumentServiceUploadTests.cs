using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public class DocumentServiceUploadTests
{
    [Fact]
    public async Task UploadAsync_RequiresProjectMembershipAndPersistsMetadataAfterScan()
    {
        await using var context = CreateContext();
        var storage = CreateStorage();
        var service = CreateService(context, storage, new SafeScanner());
        await using var content = new MemoryStream("pdf"u8.ToArray());

        var document = await service.UploadAsync(new DocumentUploadRequest
        {
            Content = content,
            FileName = "plan.pdf",
            ContentType = "application/pdf",
            Title = "Project plan",
            Category = DocumentCategories.ProjectDocuments,
            ProjectId = 1,
            Tags = [" Planning "]
        }, 2);

        Assert.Equal("project plan", document.Title.ToLowerInvariant());
        Assert.Equal(1, await context.Documents.CountAsync());
        Assert.Equal("planning", await context.DocumentTags.Select(t => t.Value).SingleAsync());
        await storage.DeleteAsync(document.FilePath);
    }

    [Fact]
    public async Task UploadAsync_RejectsUnauthorizedAssociationAndUnavailableScan()
    {
        await using var context = CreateContext();
        var storage = CreateStorage();
        var service = CreateService(context, storage, new UnavailableScanner());
        await using var content = new MemoryStream("pdf"u8.ToArray());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(new DocumentUploadRequest
        {
            Content = content, FileName = "plan.pdf", ContentType = "application/pdf", Title = "Plan", Category = DocumentCategories.Other, ProjectId = 1
        }, 5));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.UploadAsync(new DocumentUploadRequest
        {
            Content = content, FileName = "plan.pdf", ContentType = "application/pdf", Title = "Plan", Category = DocumentCategories.Other
        }, 1));
        Assert.Empty(await context.Documents.ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new ApplicationDbContext(options);
        context.Users.AddRange(new User { UserId = 1, Email = "owner", DisplayName = "Owner" }, new User { UserId = 2, Email = "member", DisplayName = "Member" }, new User { UserId = 5, Email = "outsider", DisplayName = "Outsider" });
        context.Projects.Add(new Project { ProjectId = 1, Name = "Project", ProjectManagerId = 1 });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = 1, UserId = 2 });
        context.SaveChanges();
        return context;
    }

    private static FileStorageService CreateStorage() => new(Options.Create(new DocumentStorageOptions { UploadRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), MaxFileSizeBytes = 25 * 1024 * 1024, AllowedExtensions = [".pdf"], AllowedContentTypes = ["application/pdf"] }), new TestEnvironment());

    private static DocumentService CreateService(ApplicationDbContext context, FileStorageService storage, IFileScanService scanner) => new(context, storage, scanner, new DocumentAuditService(context), Options.Create(new DocumentStorageOptions { MaxFileSizeBytes = 25 * 1024 * 1024, AllowedExtensions = [".pdf"], AllowedContentTypes = ["application/pdf"] }), new NotificationService(context));
    private sealed class SafeScanner : IFileScanService { public Task<FileScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(new FileScanResult(FileScanStatus.Safe)); }
    private sealed class UnavailableScanner : IFileScanService { public Task<FileScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(new FileScanResult(FileScanStatus.Unavailable)); }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests"; public string EnvironmentName { get; set; } = "Development"; public string WebRootPath { get; set; } = Path.GetTempPath(); public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!; public string ContentRootPath { get; set; } = Path.GetTempPath(); public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

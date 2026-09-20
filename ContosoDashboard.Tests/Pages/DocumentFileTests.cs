using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Pages;

public class DocumentFileTests
{
    [Fact]
    public async Task FileAccessServiceDoesNotExposeUnauthorizedDocument()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        context.Users.AddRange(new User { UserId = 1, Email = "owner", DisplayName = "Owner" }, new User { UserId = 2, Email = "other", DisplayName = "Other" });
        context.Documents.Add(new Document { DocumentId = 7, Title = "Private", Category = DocumentCategories.Other, OriginalFileName = "private.pdf", FilePath = "1/personal/private.pdf", FileType = "application/pdf", FileSize = 1, UploadedByUserId = 1 });
        await context.SaveChangesAsync();
        var service = new DocumentService(context, null!, null!, null!, Options.Create(new DocumentStorageOptions()), null!);
        Assert.False(await service.CanAccessAsync(7, 2));
    }
}

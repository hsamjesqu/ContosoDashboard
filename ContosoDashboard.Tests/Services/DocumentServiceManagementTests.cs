using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Services;

public class DocumentServiceManagementTests
{
    [Fact]
    public async Task DeletedAndUnauthorizedDocumentsAreNotAccessible()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        context.Users.AddRange(new User { UserId = 1, Email = "owner", DisplayName = "Owner" }, new User { UserId = 2, Email = "other", DisplayName = "Other" });
        context.Documents.Add(new Document { DocumentId = 1, Title = "Deleted", Category = DocumentCategories.Other, OriginalFileName = "x.pdf", FilePath = "1/personal/x.pdf", FileType = "application/pdf", FileSize = 1, UploadedByUserId = 1, IsDeleted = true });
        await context.SaveChangesAsync();
        var service = new DocumentService(context, null!, null!, null!, Options.Create(new DocumentStorageOptions()), null!);
        Assert.Null(await service.GetAccessibleAsync(1, 1));
        Assert.Null(await service.GetAccessibleAsync(1, 2));
    }
}

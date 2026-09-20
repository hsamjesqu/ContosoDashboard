using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Tests.Fixtures;

public sealed class DocumentTestFixture
{
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        context.Users.AddRange(
            new User { UserId = 1, Email = "owner@test", DisplayName = "Owner", Role = UserRole.Employee },
            new User { UserId = 2, Email = "member@test", DisplayName = "Member", Role = UserRole.Employee },
            new User { UserId = 3, Email = "manager@test", DisplayName = "Manager", Role = UserRole.ProjectManager },
            new User { UserId = 4, Email = "admin@test", DisplayName = "Admin", Role = UserRole.Administrator },
            new User { UserId = 5, Email = "outsider@test", DisplayName = "Outsider", Role = UserRole.Employee });
        context.Projects.Add(new Project { ProjectId = 1, Name = "Test Project", ProjectManagerId = 3 });
        context.ProjectMembers.Add(new ProjectMember { ProjectMemberId = 1, ProjectId = 1, UserId = 2, Role = "Member" });
        context.Tasks.Add(new TaskItem { TaskId = 1, Title = "Test Task", AssignedUserId = 2, CreatedByUserId = 3, ProjectId = 1 });
        context.SaveChanges();
        return context;
    }
}

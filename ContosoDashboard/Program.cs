using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add authentication state provider for Blazor
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<DocumentStorageOptions>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IFileScanService, TrainingFileScanService>();
builder.Services.AddScoped<IDocumentAuditService, DocumentAuditService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

// Configure Mock Authentication (Cookie-based for training purposes)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Employee", policy => policy.RequireRole("Employee", "TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("TeamLead", policy => policy.RequireRole("TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("ProjectManager", policy => policy.RequireRole("ProjectManager", "Administrator"));
    options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator"));
});

// Register application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Add HttpContextAccessor for accessing user claims
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated(); // For development - use migrations in production
        EnsureDocumentSchema(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the database.");
    }
}

static void EnsureDocumentSchema(ApplicationDbContext context)
{
    context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[Documents]', N'U') IS NULL
BEGIN
    CREATE TABLE [Documents]
    (
        [DocumentId] int NOT NULL IDENTITY,
        [Title] nvarchar(255) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Category] nvarchar(100) NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [FilePath] nvarchar(1000) NOT NULL,
        [FileType] nvarchar(255) NOT NULL,
        [FileSize] bigint NOT NULL,
        [UploadedByUserId] int NOT NULL,
        [UploadedDate] datetime2 NOT NULL,
        [ProjectId] int NULL,
        [TaskId] int NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([DocumentId]),
        CONSTRAINT [FK_Documents_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([UserId]),
        CONSTRAINT [FK_Documents_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]),
        CONSTRAINT [FK_Documents_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [Tasks] ([TaskId])
    );
END;

IF OBJECT_ID(N'[DocumentShares]', N'U') IS NULL
BEGIN
    CREATE TABLE [DocumentShares]
    (
        [DocumentShareId] int NOT NULL IDENTITY,
        [DocumentId] int NOT NULL,
        [UserId] int NULL,
        [ProjectId] int NULL,
        [SharedByUserId] int NOT NULL,
        [SharedDate] datetime2 NOT NULL,
        [RevokedDate] datetime2 NULL,
        CONSTRAINT [PK_DocumentShares] PRIMARY KEY ([DocumentShareId]),
        CONSTRAINT [FK_DocumentShares_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([DocumentId]),
        CONSTRAINT [FK_DocumentShares_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]),
        CONSTRAINT [FK_DocumentShares_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([ProjectId]),
        CONSTRAINT [FK_DocumentShares_Users_SharedByUserId] FOREIGN KEY ([SharedByUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF OBJECT_ID(N'[DocumentActivities]', N'U') IS NULL
BEGIN
    CREATE TABLE [DocumentActivities]
    (
        [DocumentActivityId] int NOT NULL IDENTITY,
        [DocumentId] int NULL,
        [UserId] int NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Detail] nvarchar(1000) NULL,
        [OccurredDate] datetime2 NOT NULL,
        CONSTRAINT [PK_DocumentActivities] PRIMARY KEY ([DocumentActivityId]),
        CONSTRAINT [FK_DocumentActivities_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([DocumentId]) ON DELETE SET NULL,
        CONSTRAINT [FK_DocumentActivities_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF OBJECT_ID(N'[DocumentTags]', N'U') IS NULL
BEGIN
    CREATE TABLE [DocumentTags]
    (
        [DocumentTagId] int NOT NULL IDENTITY,
        [DocumentId] int NOT NULL,
        [Value] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_DocumentTags] PRIMARY KEY ([DocumentTagId]),
        CONSTRAINT [FK_DocumentTags_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([DocumentId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DocumentTags_DocumentId_Value' AND object_id = OBJECT_ID(N'[DocumentTags]'))
    CREATE UNIQUE INDEX [IX_DocumentTags_DocumentId_Value] ON [DocumentTags] ([DocumentId], [Value]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DocumentActivities_Action_OccurredDate' AND object_id = OBJECT_ID(N'[DocumentActivities]'))
    CREATE INDEX [IX_DocumentActivities_Action_OccurredDate] ON [DocumentActivities] ([Action], [OccurredDate]);");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    // Use HSTS even in development for training purposes
    app.UseHsts();
}

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    
    // Content Security Policy for Blazor Server
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' wss: ws:;";
    
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapRazorPages();
app.MapFallbackToPage("/_Host");

app.Run();

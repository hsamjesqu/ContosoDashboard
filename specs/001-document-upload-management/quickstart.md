# Quickstart: Document Upload and Management

## Prerequisites

- .NET 8 SDK and SQL Server LocalDB.
- A clean local database for first-run validation.
- Repository root at `C:\ME\Repos\ContosoDashboard`.

## Start the application

```powershell
Set-Location .\ContosoDashboard
dotnet restore
dotnet run
```

Open the HTTPS URL printed by the application and sign in through `/login` using a seeded
training user.

## Automated checks

```powershell
dotnet test .\ContosoDashboard.Tests
dotnet build .\ContosoDashboard\ContosoDashboard.csproj
```

The tests must cover allow-list and 25 MB validation, fail-closed scan behavior, generated
relative paths, cleanup after failed persistence, owner/project-member/administrator access,
and denial of direct unauthorized document access.

## End-to-end validation scenarios

1. Upload one PDF and one image with distinct titles/categories. Confirm progress, success,
   one record per file, captured MIME type/size/uploader, and files outside `wwwroot`.
2. Try an unsupported extension, a file over 25 MB, and a configured unavailable scanner.
   Confirm each upload is rejected with a clear message and leaves no available record or
   orphaned file.
3. Upload a project document as a project member. Confirm another project member can list,
   preview, and download it, while a non-member cannot access it by URL or ID.
4. Search, sort, and filter a document set by title, date, category, size, project, tags,
   uploader, and date range. Confirm results remain permission-filtered.
5. Preview a PDF/image, download another file, edit metadata, replace a file, and delete it
   as owner. Confirm replacement failure keeps the previous available file and deletion
   removes it from normal lists while retaining audit history.
6. Share a personal document with a selected user and a project document with its existing
   project team. Confirm notifications and “Shared with Me”; confirm revoked/deleted access
   is no longer visible.
7. Open the new task-detail page, attach a document, and confirm it inherits the task's
   project. Confirm the dashboard shows five recent documents and the document count.
8. Log in as Administrator and generate activity reports. Repeat as Employee and confirm
   report access is denied.

## Performance checks

Measure representative runs against the feature targets: valid 25 MB upload under 30
seconds, 500-document list and authorized search under 2 seconds, and PDF/image preview
under 3 seconds. Record environment and result with the validation output.
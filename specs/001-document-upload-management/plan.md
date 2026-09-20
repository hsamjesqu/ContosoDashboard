# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-09-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command; its definition describes the execution workflow.

## Summary

Add secure, offline document storage and management to ContosoDashboard while preserving
the existing Blazor Server, EF Core, service-layer authorization, and notification patterns.
Use local storage and a replaceable file-scanning abstraction; save validated files outside
`wwwroot`, persist integer-key metadata only after a successful scan and file write, and
expose authorized preview/download operations through server endpoints.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# on .NET 8.0

**Primary Dependencies**: ASP.NET Core Blazor Server, Razor Pages, Entity Framework Core 8,
SQL Server provider, existing cookie authentication and notification services; add a test
project using xUnit and EF Core InMemory where service isolation is needed.

**Storage**: SQL Server LocalDB for metadata and local filesystem under `AppData/uploads`,
outside `wwwroot`; storage is accessed through `IFileStorageService`.

**Testing**: Focused service authorization and upload-order tests with xUnit; component and
manual end-to-end validation through the scenarios in `quickstart.md`; performance checks
for the stated 25 MB, 500-document, and response-time targets.

**Target Platform**: Local Windows development and offline training deployment hosting the
ASP.NET Core 8 Blazor Server application.

**Project Type**: Single web application with Razor Pages endpoints and Blazor Server UI.

**Performance Goals**: Upload valid files up to 25 MB within 30 seconds under typical
conditions; lists and search within 2 seconds for 500 documents; previews within 3 seconds.

**Constraints**: Fail closed when scanning is unavailable or incomplete; never expose files
from `wwwroot`; use GUID-based relative paths; enforce authorization in every service and
file endpoint; preserve integer IDs and text categories; remain cloud-independent.

**Scale/Scope**: One document record per uploaded file; initial list/search target is 500
accessible documents per user; four existing roles; project-team sharing reuses ProjectMember.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The design passes the constitution gate:

- **Layered, Testable Design**: Models, EF configuration, storage/scanning abstractions,
  services, endpoints, and Blazor pages remain separate.
- **Authorization at Every Data Boundary**: `DocumentService` owns access decisions and
  every preview/download endpoint calls it before opening a stream.
- **Offline-First and Abstraction-Friendly Infrastructure**: Local filesystem and scanner
  implementations are behind interfaces; business logic does not depend on Azure SDKs.
- **Requirements Drive Verifiable Delivery**: User-story tasks, xUnit checks, and the
  quickstart validation scenarios trace to the specification.
- **Training Scope and Explicit Simplicity**: The implementation remains local and records
  that the scanner and authentication are training-grade replacements.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
ContosoDashboard/
├── Data/ApplicationDbContext.cs
├── Models/Document.cs
├── Models/DocumentShare.cs
├── Models/DocumentActivity.cs
├── Models/DocumentCategory.cs
├── Services/DocumentService.cs
├── Services/FileStorageService.cs
├── Services/FileScanService.cs
├── Services/DocumentAuditService.cs
├── Pages/Documents.razor
├── Pages/TaskDetails.razor
├── Pages/DocumentFile.cshtml
├── Pages/DocumentFile.cshtml.cs
├── Pages/Reports.razor
├── Shared/NavMenu.razor
└── Program.cs

ContosoDashboard.Tests/
├── Services/DocumentServiceTests.cs
├── Services/FileStorageServiceTests.cs
└── Components/DocumentWorkflowTests.cs
```

**Structure Decision**: Extend the existing single application rather than split frontend
and backend projects. Domain entities and business rules stay in `ContosoDashboard/Models`
and `Services`; Razor Pages provide authorized file responses, while Blazor pages provide
interactive upload, listing, search, task, dashboard, sharing, and administrator-report UI.
The focused test project isolates service authorization, file ordering, path safety, and
workflow behavior without changing production storage.

## Complexity Tracking

No constitution violations. The additional test project and two server-side file endpoints
are justified by the constitution's testability and authorization requirements.
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |

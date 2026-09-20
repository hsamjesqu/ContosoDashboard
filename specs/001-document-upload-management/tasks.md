---

description: "Task list for document upload and management"

---

# Tasks: Document Upload and Management

**Input**: Design documents from `specs/001-document-upload-management/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/document-access.md`, `quickstart.md`

**Tests**: Included because the plan and constitution require focused regression coverage for authorization, storage, and shared data behavior.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated as an independent increment after foundational work.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the test project, configuration, and storage conventions needed by the feature.

- [ ] T001 Create the `ContosoDashboard.Tests/ContosoDashboard.Tests.csproj` xUnit test project targeting `net8.0` with EF Core InMemory and a project reference to `ContosoDashboard/ContosoDashboard.csproj`.
- [ ] T002 [P] Add the test project to `ContosoDashboard.sln` or create the solution file at `ContosoDashboard.sln` if the repository has no solution file.
- [ ] T003 [P] Add document storage and upload limits to `ContosoDashboard/appsettings.json` and `ContosoDashboard/appsettings.Development.json`, including an upload root outside `wwwroot`, a 25 MB per-file limit, and the allowed extensions/content types.
- [ ] T004 [P] Add `AppData/uploads/` to `ContosoDashboard/.gitignore` and document the local storage directory in `ContosoDashboard/README.md`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Implement shared entities, EF relationships, storage/scanning abstractions, and authorization primitives before user-story work.

**Checkpoint**: The foundation is ready when the database model builds, storage paths are safe, scanning can fail closed, and service tests can create authorized test users and projects.

- [ ] T005 [P] Create `ContosoDashboard/Models/Document.cs` with integer `DocumentId`, required title/category, optional description/project/task fields, original filename, relative file path, MIME type limited to 255 characters, file size capped at 25 MB, uploader, UTC upload date, and deletion state.
- [ ] T006 [P] Create `ContosoDashboard/Models/DocumentShare.cs` with nullable user/project targets, sharing actor, UTC shared date, and revocation date, enforcing exactly one target in the service validation path.
- [ ] T007 [P] Create `ContosoDashboard/Models/DocumentActivity.cs` with nullable document reference for retained deletion history, actor, action, detail, and UTC occurrence date.
- [ ] T008 [P] Create `ContosoDashboard/Models/DocumentTag.cs` with document reference, normalized bounded tag value, and duplicate-prevention fields.
- [ ] T009 [P] Create `ContosoDashboard/Models/DocumentCategory.cs` with the text values `Project Documents`, `Team Resources`, `Personal Files`, `Reports`, `Presentations`, and `Other`.
- [ ] T010 Configure `ContosoDashboard/Data/ApplicationDbContext.cs` for Document, DocumentShare, DocumentActivity, and DocumentTag relationships, integer keys, indexes for uploader/project/task/category/date/search fields, and audit retention-safe delete behavior.
- [ ] T011 Create `ContosoDashboard/Services/FileStorageService.cs` with `IFileStorageService`, local upload/delete/download operations, GUID-based relative paths in `{userId}/{projectId-or-personal}/{guid}.{extension}` format, root-bound path validation, and no user-supplied filename use in storage paths.
- [ ] T012 Create `ContosoDashboard/Services/FileScanService.cs` with `IFileScanService` and a local training implementation that returns an explicit safe, rejected, or unavailable result; unavailable or incomplete scanning must fail closed.
- [ ] T013 Create `ContosoDashboard/Services/DocumentAuditService.cs` with audit-record creation and retention-aware query methods for uploads, downloads, previews, replacements, deletions, and shares.
- [ ] T014 Register document entities, `IFileStorageService`, `IFileScanService`, and `DocumentAuditService` in `ContosoDashboard/Program.cs` and preserve the existing authorization/security-header middleware.
- [ ] T015 Add foundational storage and scan tests in `ContosoDashboard.Tests/Services/FileStorageServiceTests.cs` covering path traversal rejection, GUID path generation, root containment, cleanup, allowed file limits, and fail-closed unavailable scanning.
- [ ] T016 Add shared authorized-user/project fixtures in `ContosoDashboard.Tests/Fixtures/DocumentTestFixture.cs` for owner, project member, project manager, administrator, non-member, and task-linked project scenarios.

---

## Phase 3: User Story 1 - Upload and Organize Documents (Priority: P1)

**Goal**: Allow authenticated users to upload one or more files with per-file metadata, validation, scanning, project/task associations, and safe persistence.

**Independent Test**: As a seeded employee, upload two supported files with distinct titles/categories and confirm both records and files are available; repeat with an unsupported file, an oversized file, unavailable scanning, unauthorized project association, and forced persistence failure to confirm clear rejection and cleanup.

### Tests for User Story 1

- [ ] T017 [P] [US1] Add upload validation and metadata tests in `ContosoDashboard.Tests/Services/DocumentServiceUploadTests.cs` for per-file title/category, six text categories, optional metadata, MIME types up to 255 characters, and 25 MB size rejection.
- [ ] T018 [P] [US1] Add upload authorization and ordering tests in `ContosoDashboard.Tests/Services/DocumentServiceUploadTests.cs` proving project membership is required and the sequence is validate -> scan -> save file -> save metadata, with cleanup on failure.

### Implementation for User Story 1

- [ ] T019 [US1] Create `ContosoDashboard/Services/DocumentService.cs` with upload DTOs and validation for allow-listed PDF, Word, Excel, PowerPoint, text, JPEG, and PNG files; enforce 25 MB per file and required per-file title/category.
- [ ] T020 [US1] Implement the authorized upload workflow in `ContosoDashboard/Services/DocumentService.cs`: validate associations, scan and fail closed, generate a unique path, save the file, persist metadata, create audit activity, and remove partial files/records on failure.
- [ ] T021 [US1] Add project/task authorization checks and project-member notification creation to `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Services/NotificationService.cs`, honoring existing in-app notification preferences.
- [ ] T022 [US1] Create `ContosoDashboard/Pages/Documents.razor` with authenticated multi-file selection, per-file title/category fields, editable batch defaults, optional metadata, project selection, task selection, progress feedback, success/error states, and an upload action calling `DocumentService`.
- [ ] T023 [US1] Add `Documents` navigation to `ContosoDashboard/Shared/NavMenu.razor` and protect the page with the existing authorization route pattern.
- [ ] T024 [US1] Add upload configuration binding and service registration in `ContosoDashboard/Program.cs`, including the local upload root outside `wwwroot` and a replaceable scanner implementation.

**Checkpoint**: User Story 1 is independently functional when valid multi-file uploads appear in the user's list, all invalid/scanner-unavailable uploads fail without accessible records or orphaned files, and project associations enforce membership.

---

## Phase 4: User Story 2 - Find and Manage Accessible Documents (Priority: P1)

**Goal**: Provide authorized document listing, sorting, filtering, search, preview, download, metadata editing, replacement, and permanent deletion.

**Independent Test**: Seed accessible and inaccessible documents, then verify list/search authorization, sorting/filtering, PDF/image preview, downloads, owner edits/replacements, project-manager deletion, and denial of guessed IDs or paths.

### Tests for User Story 2

- [ ] T025 [P] [US2] Add authorized list/search/sort/filter tests in `ContosoDashboard.Tests/Services/DocumentServiceQueryTests.cs` for title, description, tags, uploader, project, category, date range, file size, and permission-filtered results.
- [ ] T026 [P] [US2] Add update/replace/delete authorization tests in `ContosoDashboard.Tests/Services/DocumentServiceManagementTests.cs` for owners, project managers, administrators, non-members, replacement failure preservation, audit retention, and permanent file removal.
- [ ] T027 [P] [US2] Add authorized file-response tests in `ContosoDashboard.Tests/Pages/DocumentFileTests.cs` for inline PDF/image preview, attachment download, missing/deleted files, and access-safe unauthorized responses.

### Implementation for User Story 2

- [ ] T028 [US2] Extend `ContosoDashboard/Services/DocumentService.cs` with a permission predicate for owner, project member, project manager, administrator, and active explicit share access used consistently by list, search, stream, update, replacement, and delete operations.
- [ ] T029 [US2] Extend `ContosoDashboard/Services/DocumentService.cs` with paged list/search methods supporting title, description, normalized tags, uploader, project, category, date range, title/date/category/size sorting, and empty-result behavior for up to 500 documents.
- [ ] T030 [US2] Implement metadata update, replacement, and delete workflows in `ContosoDashboard/Services/DocumentService.cs`, preserving the previous available file when replacement validation/scan/storage fails and retaining `DocumentActivity` records for at least 12 months after activity.
- [ ] T031 [US2] Create `ContosoDashboard/Pages/DocumentFile.cshtml` and `ContosoDashboard/Pages/DocumentFile.cshtml.cs` with authenticated preview/download handlers that authorize through `DocumentService` before opening a stream, never expose filesystem paths, and select inline versus attachment disposition safely.
- [ ] T032 [US2] Complete `ContosoDashboard/Pages/Documents.razor` with list/search/filter/sort controls, metadata edit and replacement UI, delete confirmation, preview/download links, empty/loading/error states, and accessible-action visibility.
- [ ] T033 [US2] Add document count and recent-document query methods to `ContosoDashboard/Services/DashboardService.cs`, then add the five most recent documents and the document count summary to `ContosoDashboard/Pages/Index.razor`.

**Checkpoint**: User Story 2 is independently functional when all document queries are permission-filtered, supported files preview/download only after authorization, owners can manage their documents, and project managers/admins receive only their allowed elevated actions.

---

## Phase 5: User Story 3 - Share and Use Documents in Workflows (Priority: P2)

**Goal**: Connect documents to projects/tasks, support project-team or selected-user sharing, notify recipients, and provide task/dashboard workflow integration.

**Independent Test**: Share a personal document with a selected user and a project document with its existing project team, verify notifications and “Shared with Me,” attach a document from task details, and confirm project/dashboard document visibility.

### Tests for User Story 3

- [ ] T034 [P] [US3] Add sharing authorization and recipient tests in `ContosoDashboard.Tests/Services/DocumentServiceSharingTests.cs` for selected-user sharing, existing project-team sharing, duplicate/revoked shares, notification creation, and deleted-document visibility.
- [ ] T035 [P] [US3] Add task-detail document workflow tests in `ContosoDashboard.Tests/Components/TaskDetailsWorkflowTests.cs` for authorized task access, inherited project association, related-document listing, and unauthorized task access.

### Implementation for User Story 3

- [ ] T036 [US3] Extend `ContosoDashboard/Services/DocumentService.cs` with share, revoke, shared-with-me, project-document, and task-document methods enforcing owner/project membership rules and creating audit entries.
- [ ] T037 [US3] Add share-recipient notification creation in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Services/NotificationService.cs`, including project-member notifications only when in-app preferences permit them.
- [ ] T038 [US3] Create `ContosoDashboard/Pages/TaskDetails.razor` with route `/tasks/{taskId:int}`, authorized task loading, related-document list, upload flow inheriting the task's project, and access-denied/not-found behavior.
- [ ] T039 [US3] Extend `ContosoDashboard/Pages/ProjectDetails.razor` with an authorized project-documents section and project-manager upload/delete actions.
- [ ] T040 [US3] Extend `ContosoDashboard/Pages/Documents.razor` with “Shared with Me,” selected-user sharing, project-team sharing for project documents, revoke actions, and recipient notification states.
- [ ] T041 [US3] Add task-detail navigation from `ContosoDashboard/Pages/Tasks.razor` and add the task-detail route to the workflow links without removing existing task status behavior.

**Checkpoint**: User Story 3 is independently functional when project/team permissions, explicit shares, notifications, task attachments, project documents, and dashboard recent documents all work without bypassing service authorization.

---

## Phase 6: User Story 4 - Audit Document Activity (Priority: P3)

**Goal**: Preserve document activity and provide administrator-only document reports.

**Independent Test**: Generate upload/download/preview/replace/delete/share activity as multiple users, then verify administrator reports and confirm employees cannot access report data.

### Tests for User Story 4

- [ ] T042 [P] [US4] Add audit event tests in `ContosoDashboard.Tests/Services/DocumentAuditServiceTests.cs` for all required actions, actor/document/timestamp fields, deletion retention, and 12-month minimum availability.
- [ ] T043 [P] [US4] Add administrator report authorization and aggregation tests in `ContosoDashboard.Tests/Services/DocumentReportTests.cs` for document types, active uploaders, access patterns, empty reports, and non-administrator denial.

### Implementation for User Story 4

- [ ] T044 [US4] Complete audit calls in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Pages/DocumentFile.cshtml.cs` for uploads, downloads, previews, replacements, deletions, and shares without exposing sensitive filesystem details.
- [ ] T045 [US4] Add administrator-only report query methods to `ContosoDashboard/Services/DocumentAuditService.cs` for document types, active uploaders, access patterns, and retained activity with empty-result behavior.
- [ ] T046 [US4] Create `ContosoDashboard/Pages/Reports.razor` with administrator authorization, report filters, aggregate display, empty/error states, and denial behavior for non-administrators.
- [ ] T047 [US4] Add the administrator reports link to `ContosoDashboard/Shared/NavMenu.razor` with role-aware visibility while retaining server-side authorization enforcement.

**Checkpoint**: User Story 4 is independently functional when all required activities are retained and report data is available only to administrators.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validate the complete feature, performance, security, documentation, and training limitations.

- [ ] T048 [P] Add end-to-end document workflow coverage in `ContosoDashboard.Tests/Integration/DocumentWorkflowTests.cs` for the upload, browse/manage, share/task, and audit story checkpoints.
- [ ] T049 [P] Add accessibility and responsive-state checks for upload/list/modals in `ContosoDashboard/Pages/Documents.razor`, `ContosoDashboard/Pages/TaskDetails.razor`, and `ContosoDashboard/Pages/Reports.razor`.
- [ ] T050 [P] Add performance validation scripts or documented benchmark cases in `ContosoDashboard.Tests/Performance/DocumentPerformanceTests.cs` for 25 MB upload under 30 seconds, 500-document list/search under 2 seconds, and preview under 3 seconds.
- [ ] T051 Review authorization, path traversal, content-type, MIME-length, cleanup, audit-retention, and direct-ID/file-location protections in `ContosoDashboard/Services/DocumentService.cs`, `ContosoDashboard/Services/FileStorageService.cs`, and `ContosoDashboard/Pages/DocumentFile.cshtml.cs`.
- [ ] T052 Update `ContosoDashboard/README.md` with document-management setup, local upload directory, clean-database guidance, training-only scanner/authentication limitations, and the `quickstart.md` validation scenarios.
- [ ] T053 Run `dotnet build ContosoDashboard/ContosoDashboard.csproj` and `dotnet test ContosoDashboard.Tests/ContosoDashboard.Tests.csproj`, then execute every scenario in `specs/001-document-upload-management/quickstart.md` and record any environment-specific limitations.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: No dependencies; T001-T004 can begin immediately, with T002 depending only on repository solution state.
- **Phase 2 Foundational**: Depends on Phase 1; T005-T009 can run in parallel, then T010-T016 complete the shared model, services, registrations, and fixtures.
- **Phase 3 User Story 1**: Depends on Phase 2; T017-T018 should be written first, then T019-T024 implement the upload slice.
- **Phase 4 User Story 2**: Depends on the upload foundation and document entities; T025-T027 should be written first, then T028-T033 implement query and management behavior.
- **Phase 5 User Story 3**: Depends on DocumentService and Documents.razor from Phases 3-4; T034-T035 can begin after shared fixtures, then T036-T041 implement collaboration and workflow integration.
- **Phase 6 User Story 4**: Depends on audit infrastructure and document operations from Phases 2-5; T042-T043 can begin after audit contracts, then T044-T047 implement reporting.
- **Phase 7 Polish**: Depends on all desired user stories; T048-T053 validate the integrated feature.

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational; delivers the upload MVP.
- **US2 (P1)**: Depends on US1's DocumentService and document list surface, but remains independently testable with seeded document records.
- **US3 (P2)**: Depends on US1/US2 document access contracts and the existing project/task/notification services.
- **US4 (P3)**: Depends on the audit records emitted by US1-US3 operations.

### Parallel Opportunities

- T005-T009 can run in parallel because each creates a separate model file.
- T015-T016 can run in parallel after the foundational contracts exist.
- T017-T018, T025-T027, T034-T035, and T042-T043 can each run in parallel as story-specific tests.
- T048-T050 can run in parallel after all story implementations are complete.
- Different user stories can be staffed in parallel only after Foundational and the shared DocumentService contracts are stable; sequential P1 delivery is recommended for this repository.

## Parallel Example: User Story 1

```text
Task: "T017 [US1] Add upload validation and metadata tests in ContosoDashboard.Tests/Services/DocumentServiceUploadTests.cs"
Task: "T018 [US1] Add upload authorization and ordering tests in ContosoDashboard.Tests/Services/DocumentServiceUploadTests.cs"
```

## Parallel Example: User Story 2

```text
Task: "T025 [US2] Add authorized list/search/sort/filter tests in ContosoDashboard.Tests/Services/DocumentServiceQueryTests.cs"
Task: "T027 [US2] Add authorized file-response tests in ContosoDashboard.Tests/Pages/DocumentFileTests.cs"
```

## Implementation Strategy

### MVP First (User Stories 1 and 2)

1. Complete Phase 1 setup and Phase 2 foundational work.
2. Complete Phase 3 upload and organize documents.
3. Complete Phase 4 browse and manage accessible documents.
4. Stop and validate the P1 workflows independently with the story tests and quickstart scenarios.

### Incremental Delivery

1. Add Phase 5 project/task/share integration after P1 document access is stable.
2. Add Phase 6 administrator audit reports after activity events are emitted consistently.
3. Complete Phase 7 security, performance, accessibility, documentation, and full quickstart validation.
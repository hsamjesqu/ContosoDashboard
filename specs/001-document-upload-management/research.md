# Research: Document Upload and Management

## Decision: Use local storage behind `IFileStorageService`

**Rationale**: The feature must work offline and files must remain outside `wwwroot`. A
relative path such as `{userId}/{projectId-or-personal}/{guid}.{extension}` prevents direct
web exposure, avoids user-controlled filenames, and maps cleanly to a future blob name.
The service will create directories as needed, write streams asynchronously, and delete only
validated paths beneath the configured upload root.

**Alternatives considered**: Storing files in `wwwroot` was rejected because it bypasses
authorization. Storing file bytes in SQL was rejected because it complicates the training
database and does not match the stated migration path.

## Decision: Fail closed through an `IFileScanService` abstraction

**Rationale**: A document is not available until scanning succeeds. A local training
implementation can support known-safe test content or a configured scanner result, while a
future production implementation can call a malware-scanning service. Unavailable,
incomplete, or failed scans reject the upload and leave no completed metadata.

**Alternatives considered**: Allowing unscanned files with a warning was rejected because it
violates the clarified security requirement. A hard dependency on a cloud scanner was
rejected because the app must operate offline.

## Decision: Put all access decisions in `DocumentService`

**Rationale**: Existing project and task services already perform service-level membership
checks. A single document service can compose owner, administrator, project-manager,
project-member, task, and explicit-share rules consistently for list, search, stream,
update, delete, and share operations. UI and file endpoints pass the requesting user ID and
never make authorization decisions from route values alone.

**Alternatives considered**: UI-only checks were rejected because direct requests could
bypass them. Duplicating rules in each page and endpoint was rejected because it increases
IDOR risk and makes policy drift likely.

## Decision: Use integer-key EF entities and normalized associations

**Rationale**: Integer document IDs match the existing User, Project, and TaskItem keys.
Document shares and audit records are separate entities so access grants and retention can
be queried without putting collections or serialized tags in the document row. Categories
remain text values as required; tags use a normalized child entity.

**Alternatives considered**: GUID database keys and enum-backed categories were rejected by
the feature constraints. A single JSON metadata column was rejected because filtering and
reporting need queryable values.

## Decision: Use server-side Razor Page responses for preview and download

**Rationale**: Files are outside public content and require authorization before streaming.
Dedicated handlers can call `DocumentService`, set the stored content type only after
authorization, use inline disposition for PDF/image preview, and attachment disposition
for downloads. No file path or storage root is exposed to the browser.

**Alternatives considered**: Static file middleware was rejected because it cannot enforce
document membership. Client-side direct filesystem access is unavailable in the browser.

## Decision: Use focused xUnit and manual workflow validation

**Rationale**: The repository has no test project, so a focused test project will cover the
highest-risk service and storage behaviors without imposing a broad test framework migration.
The quickstart remains the end-to-end proof for LocalDB, mock login, Blazor upload, project
membership, task attachments, sharing, deletion, and administrator reports.

**Alternatives considered**: Manual testing alone was rejected for authorization and cleanup
paths. A full UI automation framework was deferred because it would add disproportionate
training-project setup before the core service contracts are stable.
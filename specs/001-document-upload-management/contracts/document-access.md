# Document Access Contract

The application exposes document access through authorized Razor Page handlers. These are
server-side contracts for the Blazor UI; no public external API is introduced.

## Upload

**Caller**: authenticated employee, team lead, project manager, or administrator.

**Input**: one or more file streams plus per-file title, category, optional description,
tags, project ID, and task ID.

**Success**: each accepted file returns its integer document ID and captured metadata.

**Failure**: validation, scan, authorization, storage, or persistence failure returns a
clear user-facing error; no rejected file is available and partial storage is cleaned up.

## List and search

**Caller**: authenticated user.

**Input**: optional search text, category, project ID, date range, sort field, and direction.

**Success**: returns only documents authorized for the caller, with title, category, upload
date, size, project, uploader, and allowed actions.

## Preview/download

**Route shape**: `/document-file/{documentId}?mode=preview|download`.

**Caller**: authenticated user with access to the document.

**Success**: server streams the file after authorization. Preview is inline only for PDF and
image content; downloads use attachment disposition. The storage path is never returned.

**Failure**: unauthorized, missing, deleted, or unavailable files return an access-safe
failure without revealing filesystem details.

## Update/replace/delete/share

**Caller**: owner, authorized project manager, or administrator according to the operation.

**Success**: service applies the operation, records a DocumentActivity entry, and emits
required notifications.

**Failure**: operation is rejected without changing accessible data when validation or
authorization fails.

## Administrator reports

**Caller**: administrator only.

**Success**: returns document-type, uploader, and access-pattern aggregates from retained
activity records.

**Failure**: non-administrator callers receive access denied without report data.
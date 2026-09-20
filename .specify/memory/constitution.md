<!--
Sync Impact Report
- Version change: unratified template -> 1.0.0
- Modified principles: five template placeholders -> five ContosoDashboard principles
- Added sections: Project Constraints; Development Workflow
- Removed sections: none
- Follow-up TODO: confirm the original ratification date
-->

# ContosoDashboard Constitution

## Core Principles

### I. Layered, Testable Design
Application behavior MUST remain separated into models, data access, services, and UI
pages. New business rules MUST live in services or other independently testable units
rather than being duplicated in components. This keeps the training application easy to
understand and preserves a migration path for infrastructure changes.

### II. Authorization at Every Data Boundary
Every protected page MUST require authentication, and every service operation that reads
or changes user-scoped data MUST verify the requesting user's authorization. Project,
task, profile, and future document access MUST enforce membership, ownership, or role
rules before returning data. This prevents insecure direct object references and teaches
defense in depth.

### III. Offline-First and Abstraction-Friendly Infrastructure
The training application MUST run locally without cloud service dependencies. Infrastructure
that may change during production migration MUST be accessed through interfaces and
dependency injection where practical. Local implementations MUST remain replaceable by
cloud implementations without changing business rules or UI behavior.

### IV. Requirements Drive Verifiable Delivery
Feature work MUST begin with a testable specification and an implementation plan before
tasks are generated. Requirements MUST use measurable acceptance criteria, and each task
MUST identify its owning user story or cross-cutting purpose. Changes MUST be validated
with the narrowest relevant automated check available before completion.

### V. Training Scope and Explicit Simplicity
The project MUST favor clear, minimal implementations that support the training objective
over production-scale complexity. Features MUST document security, operational, or
production limitations when they use mock behavior. Production deployment claims MUST NOT
be made for code that relies on mock authentication, development database initialization,
or other training-only mechanisms.

## Project Constraints

The application MUST target ASP.NET Core 8 with Blazor Server and Entity Framework Core,
using SQL Server LocalDB for the offline training environment. Seed data and mock cookie
authentication are permitted for training but MUST NOT be treated as production identity
or data-protection solutions. Protected routes MUST use authorization-aware routing, and
security-sensitive response headers MUST remain enabled unless a documented exception is
approved.

## Development Workflow

Feature work MUST follow the repository's Spec Kit flow: specify the user need, clarify
material ambiguity, plan the technical approach, generate dependency-ordered tasks, and
implement against those tasks. Before implementation, the specification, plan, and tasks
MUST be reviewed for consistency. Changes that affect authentication, authorization,
storage, or shared data models MUST include focused regression coverage or a documented
manual validation path when automated coverage is unavailable.

## Governance

This constitution is the governing project guidance for Spec Kit planning and implementation.
Amendments MUST describe the affected principles or sections, explain the rationale, update
the version and amendment date, and preserve a migration note when existing work no longer
conforms. Versioning follows semantic versioning: MAJOR for incompatible governance changes,
MINOR for new or materially expanded principles, and PATCH for clarifications or wording-only
changes. Every feature review MUST check compliance with the constitution, and unresolved
violations MUST be recorded before implementation proceeds.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): confirm original adoption date | **Last Amended**: 2026-09-19

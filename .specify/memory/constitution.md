<!--
Sync Impact Report
==================
Version change: (template/unversioned) → 1.0.0
Bump rationale: Initial ratification. All template placeholders replaced with
  concrete, project-specific principles and governance. First adoption ⇒ MAJOR 1.0.0.

Modified principles:
  [PRINCIPLE_1_NAME] → I. Monolithic Architecture
  [PRINCIPLE_2_NAME] → II. SOLID Design Principles
  [PRINCIPLE_3_NAME] → III. Simplicity First (YAGNI)
  [PRINCIPLE_4_NAME] → IV. EF Core Migrations for All Schema (NON-NEGOTIABLE)
  [PRINCIPLE_5_NAME] → V. MVC Separation of Concerns

Added sections:
  Technology Constraints (replaces [SECTION_2_NAME])
  Development Workflow & Quality Gates (replaces [SECTION_3_NAME])

Removed sections: none

Templates requiring updates:
  ✅ .specify/templates/plan-template.md — Constitution Check gate is dynamic; no edit needed
  ✅ .specify/templates/spec-template.md — no principle-specific references; no edit needed
  ✅ .specify/templates/tasks-template.md — task categories align with principles; no edit needed

Follow-up TODOs: none
-->

# Beta Platform Constitution

## Core Principles

### I. Monolithic Architecture

The system MUST be built and deployed as a single ASP.NET Core .NET 9 MVC
application (one solution, one deployable unit). Distributed patterns
(microservices, separate service processes, message brokers, service meshes)
MUST NOT be introduced. Internal modularity is achieved through folders,
namespaces, and clear layer boundaries (Controllers → Services → Data) within
the same process — never through network hops.

**Rationale**: A monolith keeps deployment, debugging, and transactions simple
for a project of this scope. Splitting into services adds operational cost that
is not justified by current requirements.

### II. SOLID Design Principles

All production code MUST honor SOLID:
- **Single Responsibility**: each class/service has one reason to change.
- **Open/Closed**: extend behavior via new types or injected abstractions, not
  by editing stable code paths.
- **Liskov Substitution**: implementations MUST be substitutable for their
  interfaces without surprising callers.
- **Interface Segregation**: prefer small, focused interfaces over broad ones.
- **Dependency Inversion**: depend on abstractions; dependencies MUST be
  supplied through the built-in ASP.NET Core dependency injection container.

**Rationale**: SOLID keeps the monolith maintainable and testable as it grows,
preventing the codebase from decaying into tightly coupled controllers.

### III. Simplicity First (YAGNI)

Solve the problem in front of you with the least machinery that works.
Speculative abstraction, unused configuration knobs, extra layers, and
"just-in-case" generality are PROHIBITED. Do not add a pattern (repository
wrapper, mediator, CQRS, generic base classes) unless a concrete, present need
requires it. When SOLID and simplicity appear to conflict, choose the simplest
design that still isolates the single real responsibility.

**Rationale**: Over-engineering is the primary risk to velocity and clarity.
Simple code is easier to read, test, and change.

### IV. EF Core Migrations for All Schema (NON-NEGOTIABLE)

Every database table, column, index, and constraint MUST be defined through
Entity Framework Core entities and created/altered exclusively via EF Core
migrations. Hand-written or hard-coded raw SQL DDL/DML strings for schema or
seeding are PROHIBITED. Data access MUST go through EF Core (LINQ / DbContext);
raw SQL in application code is disallowed except where EF Core genuinely cannot
express a query, and any such exception MUST be justified in code review.
The database provider is **MySQL**.

**Rationale**: Migrations give a versioned, repeatable, reviewable schema
history. Hard-coded SQL fragments drift from the model and bypass this history.

### V. MVC Separation of Concerns

Controllers MUST stay thin: they coordinate requests and delegate to services;
they MUST NOT contain business logic or direct DbContext queries beyond simple
composition. Business rules live in service classes; persistence lives behind
EF Core. Views MUST NOT contain business logic. This layering MUST be
consistent across every feature.

**Rationale**: Predictable layering makes the monolith navigable and keeps
each concern independently testable.

## Technology Constraints

- **Framework**: ASP.NET Core MVC on .NET 9.
- **Language**: C# (latest version supported by the .NET 9 SDK).
- **ORM / Data**: Entity Framework Core with the MySQL provider.
- **Schema management**: EF Core migrations only (see Principle IV).
- **Dependency injection**: the built-in ASP.NET Core DI container; no
  additional IoC framework unless a concrete need is documented and approved.
- **Architecture**: single monolithic solution (see Principle I).

## Development Workflow & Quality Gates

- Schema changes MUST ship as an EF Core migration in the same change set as the
  code that depends on them.
- Code review MUST verify: SOLID adherence, absence of hard-coded raw SQL,
  absence of speculative/over-engineered abstractions, and correct
  controller → service → data layering.
- New dependencies and any new abstraction layer MUST be justified against
  Principle III before being merged.
- Business logic SHOULD be covered by unit tests that exercise services without
  requiring the web host.

## Governance

This constitution supersedes ad-hoc practices and conventions. All pull
requests and reviews MUST verify compliance with the principles above; any
deviation MUST be documented and justified in the PR description, and
unjustified complexity MUST be rejected.

Amendments require: a written change to this file, a semantic-version bump per
the policy below, and reviewer approval. Versioning policy:
- **MAJOR**: removal or backward-incompatible redefinition of a principle.
- **MINOR**: a new principle or materially expanded guidance.
- **PATCH**: clarifications and wording fixes with no semantic change.

Compliance is reviewed at every code review; the reviewer is the compliance
gate. Runtime and agent development guidance lives in `CLAUDE.md`.

**Version**: 1.0.0 | **Ratified**: 2026-07-06 | **Last Amended**: 2026-07-06

# Architecture Enforcement

## Purpose

This document defines the automated guardrails that protect the approved Clean Architecture and pragmatic DDD boundaries. The authoritative design decisions remain in the earlier approved technical-design documents and `AGENTS.md`.

## Enforced by automated tests

The `tests/Architecture.Tests` project runs in GitHub Actions for every pull request targeting `master` and every push to `master`.

The current checks enforce:

1. **Domain isolation** — Central/Branch Domain project references are limited to BuildingBlocks. Domain source must not use EF Core namespaces.
2. **Application isolation** — Central/Branch Application references are limited to the corresponding Domain, BuildingBlocks, and Contracts projects. Application must not reference Infrastructure, API, Worker, or EF Core.
3. **Infrastructure direction** — Infrastructure may implement inward-facing Application contracts and depend on Domain, BuildingBlocks, and Contracts.
4. **Host boundaries** — API and Worker are the outer composition layers and may reference Application, Infrastructure, and Contracts.
5. **Repository commit boundary** — repository implementations must not call `SaveChanges` or `SaveChangesAsync`.
6. **Aggregate ownership** — ProductUnit must not gain a standalone repository; it remains owned by Product.
7. **Pattern control** — MediatR must not be introduced without explicit architecture approval.

## Pull request enforcement

The architecture test job is a required candidate check for the `master` branch. Repository administrators must configure GitHub branch protection/rulesets to require the `.NET Tests / Architecture tests` status check and, where appropriate, an owner review before merging.

The repository integration used during implementation does not have permission to read or change branch-protection settings. Therefore this document and CI workflow do not claim that GitHub branch protection is already enabled. Until an administrator enables it, CI reports violations but GitHub may still allow a manual merge.

## Extending the guardrails

When a new project or approved architectural dependency is added, update the expected project-reference tests in the same pull request. Do not weaken or remove a rule merely to make a failing build green; explain the approved design decision and update this document first.

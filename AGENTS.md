# AI Coding and Architecture Governance

This file is mandatory project guidance for AI coding agents and human contributors.
Read it before inspecting, planning, generating, or modifying code. If another instruction conflicts with this file, stop and ask the repository owner instead of silently choosing.

## 1. Source of truth and change control
- Treat approved decisions and technical-design documents under `docs/` as authoritative.
- Do not invent, silently reinterpret, or reverse an approved business or architecture decision.
- Before changing an existing design, identify the exact conflicting approved decision and explain why a change is necessary.
- Do not broaden scope, add infrastructure, packages, abstractions, or patterns merely because they are familiar.
- Do not create or apply database migrations until the database schema approval gate is explicitly closed.
- Do not merge pull requests. The repository owner alone authorizes merges.

## 2. Required architecture
- Preserve the Modular Monolith and the established Central/Branch separation.
- Preserve Clean Architecture dependency direction:
  - Domain has no dependency on Application, Infrastructure, API, EF Core, or external frameworks.
  - Application depends on Domain and defines persistence/application abstractions.
  - Infrastructure implements Application abstractions and depends inward on Domain/Application.
  - API and Worker are composition/host layers; they must not contain domain business rules.
- Use the conventional Application Service Layer already selected by the owner.
- Do not introduce CQRS, MediatR, command/query handlers, message buses, or a parallel application pattern unless the owner explicitly approves it.
- Keep all primary keys as `Guid`.

## 3. DDD rules
- Model aggregate roots explicitly by inheriting from `AggregateRoot`; ordinary entities inherit from `EntityBase`.
- A repository is defined for an Aggregate Root, not automatically for every entity or table.
- Child entities and value objects are accessed and changed through their owning aggregate root.
- Do not expose mutable collections from aggregates. Expose read-only views and provide behavior methods that enforce invariants.
- Put business invariants and state transitions in the Domain model, not in controllers, repositories, EF configurations, or application services.
- Application services coordinate use cases, authorization/context, repository calls, and transaction boundaries; they must not become an alternate home for domain rules.
- Do not add generic repositories or a generic Unit of Work implementation without an approved reason. Prefer focused aggregate repository contracts.
- Entity equality must use the same concrete entity type and a valid, non-empty identity. Do not use mutable business fields for entity equality.
- Value Objects must be immutable and compare by value. Do not model a value object as an entity merely for EF convenience.
- Cross-aggregate references use identity; do not navigate and mutate another aggregate's internals as if it were one aggregate.

## 4. Repository and Unit of Work
- Repository interfaces belong in Application (or the already approved abstraction location); EF Core implementations belong in Infrastructure.
- Repositories load/add/update/remove aggregate roots and do not call `SaveChanges` themselves.
- Repositories must not own transaction commit decisions.
- Unit of Work is the single persistence commit boundary for one application use case.
- Application services call `SaveChangesAsync` once after all required changes are staged, unless an explicitly approved multi-transaction workflow requires otherwise.
- Use the same scoped DbContext instance for repositories and Unit of Work within a request/use case.
- Do not wrap EF Core in unnecessary abstractions or expose `IQueryable` outside Infrastructure.
- Use asynchronous EF operations for database I/O and pass the CancellationToken through.
- Database unique constraints/indexes remain the final protection against concurrent duplicate writes; pre-checks alone are not sufficient.

## 5. Persistence and data integrity
- EF Core mappings/configurations and DbContext implementations belong in Infrastructure.
- Do not add EF Core attributes or references to Domain entities unless an approved decision explicitly requires them.
- Keep database constraints aligned with approved business rules, including uniqueness and collation/case-sensitivity rules.
- Never silently normalize user-entered identifiers/codes when an approved decision requires preserving the original input.
- Do not generate migrations or alter schema based on assumptions. First verify the approved schema documents.

## 6. Required AI workflow before coding
Before changing code, the agent must:
1. Read this file and relevant approved design/decision documents.
2. Inspect the current branch, working tree, recent commits, and affected files.
3. State the requested outcome, affected projects/files, and the architecture rules that apply.
4. Identify uncertainties or conflicts and ask the owner before making an irreversible or design-level choice.
5. Make the smallest coherent change; do not perform unrelated refactoring.
6. Explain every new Domain class/type in Arabic in the pull request summary or accompanying response.

## 7. Required verification before proposing a PR
- Build the full solution in Release configuration.
- Run all relevant unit and integration tests; report exact commands and outcomes.
- Add tests for new domain invariants and important failure cases.
- Review the diff for dependency-direction violations, duplicate abstractions, accidental public setters, mutable collection exposure, and persistence calls inside repositories.
- Verify no unapproved migrations, packages, architecture changes, or business-rule changes were introduced.
- Never claim a test, build, review, or runtime check passed unless its output was actually observed.
- If SQL Server or another required dependency is unavailable, state that verification is pending; do not replace it with a false claim.

## 8. Pull request requirements
Every PR must state:
- Which approved requirement/decision it implements.
- Which aggregate/use case is affected.
- Why each repository or abstraction is needed.
- Where the Unit of Work commit occurs.
- Tests/build actually executed and their results.
- Known limitations and checks not run.
- Confirmation that no unapproved migration or architecture pattern was introduced.

## 9. Stop conditions
Stop and ask the owner when:
- Approved documents conflict or do not define the needed behavior.
- A change would split/merge aggregates or alter a business invariant.
- A new package, architectural pattern, database migration, or cross-module dependency appears necessary.
- A request appears to conflict with existing approved decisions.
- The agent cannot verify the current branch or repository state safely.

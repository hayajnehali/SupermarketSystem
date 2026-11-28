# Copilot Instructions

Before changing code, read the repository-root `AGENTS.md` and all relevant approved design/decision documents under `docs/`.

The rules in `AGENTS.md` are mandatory. In particular:
- Preserve Clean Architecture dependency direction and the approved Modular Monolith.
- Use the conventional Application Service Layer. Do not introduce CQRS, MediatR, handlers, or message buses.
- Follow DDD aggregate boundaries. Repositories are for aggregate roots, not every entity/table.
- Keep repository interfaces in Application and EF Core implementations in Infrastructure.
- Repositories stage changes; the scoped Unit of Work commits once at the application use-case boundary.
- Keep business invariants in Domain aggregates.
- Do not add migrations before explicit schema approval.
- Do not change approved business/architecture decisions or perform unrelated refactoring.
- Ask the repository owner when a design decision is unclear or conflicts with approved documents.
- Build the full solution and run relevant tests; report only checks actually completed.
- Never merge a pull request without explicit owner authorization.

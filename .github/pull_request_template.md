## Approved requirement / decision
<!-- Link the approved TD/decision or requirement this PR implements. -->

## Scope
- Affected module / aggregate:
- Affected projects:

## Architecture checklist
- [ ] Read and followed `AGENTS.md` and relevant approved design documents.
- [ ] Domain rules remain inside Domain aggregates.
- [ ] Repositories are only for Aggregate Roots; child entities have no standalone repository without explicit approval.
- [ ] Repository contracts are in Application and EF implementations are in Infrastructure.
- [ ] Repositories do not call SaveChanges or commit transactions.
- [ ] Unit of Work commits once at the application use-case boundary.
- [ ] No CQRS, MediatR, handlers, message bus, generic repository, or unapproved abstraction was introduced.
- [ ] No unapproved business-rule, dependency-direction, package, or schema change was introduced.
- [ ] No migration was added unless the schema approval gate was explicitly closed.

## Verification
- [ ] Full solution Release build.
- [ ] Relevant unit tests.
- [ ] Relevant integration tests (including SQL Server tests when applicable).
- [ ] Diff reviewed for architecture and data-integrity regressions.

Commands and actual results:
<!-- Include exact commands, pass/fail counts, and checks not run. -->

## Limitations / follow-up
<!-- State any environment-dependent verification that remains pending. -->

# Technical Design — Recommended Technology Stack

**Document:** TD-02  
**Version:** 0.1  
**Status:** Approved by project owner — technology baseline accepted (2026-11-06)  
**As-of date:** 2026-11-05  
**Depends on:** TD-01 Architecture Overview and the merged BRS.

## 1. Recommendation summary

| Layer | Recommended technology | Purpose |
|---|---|---|
| Backend language | C# 14 | Main application language |
| Backend framework/runtime | .NET 10 LTS / ASP.NET Core 10 | Central API, Branch API, and both Worker hosts |
| Data access | Entity Framework Core 10 | Relational persistence and migrations |
| Database | Microsoft SQL Server 2025 | Central and branch relational databases |
| Frontend language | TypeScript 6 | Angular applications |
| Frontend framework | Angular 22 | POS, online store, and central administration |
| JavaScript runtime/tooling | Node.js 26 LTS | Angular CLI, package scripts, and build tooling |
| API style | HTTPS REST APIs with JSON | Client-to-backend and controlled application integration |
| API contract | OpenAPI | API documentation and client integration contract |
| Source control | Git + GitHub | Versioning, review, and change history |

This is a recommended baseline, not an approval to begin coding. Exact patch versions must be pinned in repository configuration at implementation start and updated through controlled maintenance.

## 2. Why these versions

### 2.1 Backend — .NET 10 LTS

Recommend .NET 10 LTS for all backend hosts:
- `Central.Api`
- `Central.Worker`
- `Branch.Api`
- `Branch.Worker`

Reasons:
- It is an active Long-Term Support release as of the document date.
- Its published support window extends to November 2028.
- ASP.NET Core and Entity Framework Core follow the .NET release lifecycle.
- One runtime line across APIs and workers reduces compatibility and maintenance complexity.

Use the latest supported .NET 10 servicing patch, not an old fixed patch number. Use C# 14 and EF Core 10 aligned with the selected runtime.

### 2.2 Frontend — Angular 22

Recommend Angular 22 for all three Angular applications:
- `supermarket-pos`
- `supermarket-store`
- `supermarket-admin`

Reasons:
- Angular 22 is actively supported as of November 2026.
- It gives the three clients one framework major version and shared conventions.
- Official compatibility guidance lists supported Node.js and TypeScript ranges for Angular 22.

Use the latest compatible Angular 22 patch and the supported TypeScript 6.0 line. Do not mix Angular major versions between the three applications.

### 2.3 Node.js — 26 LTS

Recommend Node.js 26 LTS for Angular development and CI builds. The official Node release schedule lists 26.x as Active LTS as of October 28, 2026. Use a pinned supported patch version and commit the package-manager lockfile.

### 2.4 Database — SQL Server 2025

Recommend SQL Server 2025 as the database engine for central and branch databases.

Reasons:
- It is a current supported Microsoft SQL Server release.
- Its published lifecycle lists mainstream support through January 2031 and extended support through January 2036.
- It aligns with the project's relational data, inventory, invoice, audit, and reporting requirements.

**Edition decision remains open:** select Express, Standard, or another suitable edition separately for central and branch deployments after measuring database size, resource requirements, availability, backup, and licensing costs. Do not assume one edition for every site.

## 3. Application and API conventions

- Backend endpoints are versioned and documented through OpenAPI.
- Angular applications call backend APIs; they never connect directly to SQL Server.
- Use HTTPS for all non-localhost API traffic.
- Use REST/JSON for ordinary request/response operations.
- Background processing, synchronization retries, scheduled work, and provider callbacks are handled by backend hosts/workers.
- External payment confirmation is verified by the backend; browser redirects are not trusted as proof of payment.
- API DTOs are separate from persistence entities.
- Validation and authorization are enforced server-side; hiding a UI control is not a security boundary.

The exact API versioning convention, authentication protocol, and synchronization transport are separate design decisions.

## 4. Dependency and version policy

1. Keep all backend projects on the same supported .NET major version.
2. Keep EF Core packages on the matching EF Core major version.
3. Keep all Angular applications on the same Angular major version.
4. Use only Node.js versions explicitly supported by the selected Angular version.
5. Pin SDK/runtime/tool versions for reproducible local and CI builds.
6. Commit lockfiles and use deterministic package restore/build settings where supported.
7. Apply security and servicing patches regularly after compatibility checks.
8. Do not upgrade a major framework version automatically without a reviewed change and test run.

## 5. Items intentionally not selected yet

The following are not necessary to approve the language/framework baseline and remain separate decisions:
- SQL Server edition and sizing for central and branch servers.
- Server operating system and hosting provider.
- Physical branch server specifications and POS terminal specifications.
- Angular UI component library and CSS framework.
- State management library, if one is needed.
- Authentication provider, token format, key management, and local authentication implementation.
- Payment gateway, email, push notification, and SMS provider (SMS remains excluded from customer order notifications by the approved BRS).
- Logging, monitoring, tracing, and alerting products.
- CI/CD platform and deployment packaging.
- Exact .NET, Angular, Node.js, SQL Server, and package patch numbers.

These items must be selected based on requirements and compatibility; they must not silently expand the approved business scope.

## 6. Official lifecycle and compatibility references

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- .NET 10 announcement and LTS details: https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/
- Angular release and support schedule: https://angular.dev/reference/releases
- Angular version compatibility: https://angular.dev/reference/versions
- Node.js release schedule: https://github.com/nodejs/Release
- SQL Server 2025 lifecycle: https://learn.microsoft.com/en-us/lifecycle/products/sql-server-2025

Lifecycle information should be rechecked immediately before implementation begins.

## 7. Approval boundary

Approval of TD-02 confirms the recommended language, frameworks, and database engine only. It does not approve server topology, database editions, detailed schema, module code structure, authentication design, integrations, or application implementation.

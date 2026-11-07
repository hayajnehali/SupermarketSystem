# TD-29 — Identity Persistence and Employee/Customer Separation

**Version:** 0.1  
**Status:** Proposed for owner approval  
**Date:** 2026-11-08  
**Depends on:** Approved TD-06, TD-15, TD-27 and TD-28; .NET 10 / ASP.NET Core Identity baseline in TD-02.

## 1. Decision requested

Use **ASP.NET Core Identity with Guid keys**, while keeping employee and customer authentication domains separate at the persistence and authorization boundaries.

Recommended mapping:

- Employee authentication users are stored separately from the employee profile in `org.EmployeeIdentityUsers`; each user has a one-to-one link to `org.Employees`.
- Customer authentication users are stored separately in `crm.CustomerIdentityUsers`; each user has a one-to-one link to the authoritative customer profile.
- Employee roles use the existing `org.Roles` catalogue as the single role authority. Do not create a second competing role catalogue.
- Customer accounts do not participate in employee RBAC and do not receive employee roles.
- All Identity-owned tables use a single Guid primary key, including user-role, user-claim, user-login, role-claim and user-token tables. Use explicit custom Identity entity types where needed to replace framework-default composite join keys with Guid surrogate PKs.
- Central is the only identity authority. Branches receive only the minimum approved local authorization snapshot and a separately designed protected Offline credential-verification artifact.

This is a persistence and application architecture decision. It does not change the approved employee/customer business requirements or offline time limits.

## 2. Why separate employee and customer identity stores

Employees and customers have different trust boundaries, authorization rules, lifecycle operations and exposure risks.

Separate user tables and separate authentication schemes make it harder to accidentally:
- accept a customer token at an employee endpoint;
- apply employee roles to a customer account;
- expose employee credential/session data to customer-facing flows;
- let branch staff query customer authentication records.

The employee profile (`org.Employees`) and customer profile remain business records; authentication credentials belong to Identity user records. Neither profile table stores password hashes.

## 3. Proposed entity and table mapping

### 3.1 Employee identity

- `EmployeeIdentityUser : IdentityUser<Guid>` (or an equivalent custom Identity user type).
- Table: `org.EmployeeIdentityUsers`.
- PK: `EmployeeIdentityUserId` as Guid / uniqueidentifier.
- Required unique FK: `EmployeeId` → `org.Employees.EmployeeId`.
- Identity credential columns (normalized username/email, password hash, security stamp, lockout fields and two-factor fields) remain in the employee identity table as required by the selected Identity configuration.
- Employee display name, employment status, employee number and branch assignments remain in `org.Employees` and related business tables.
- Enforce one identity user per employee; enforce uniqueness of the EmployeeId FK.

### 3.2 Customer identity

- `CustomerIdentityUser : IdentityUser<Guid>` (or an equivalent custom Identity user type).
- Table: `crm.CustomerIdentityUsers`.
- PK: `CustomerIdentityUserId` as Guid / uniqueidentifier.
- Required unique FK: `CustomerId` → the authoritative customer profile PK in `crm.Customers`.
- Customer credentials and lockout/security fields remain here; customer name, phone ownership state, addresses, loyalty and complaint data remain in the business profile/domain tables.
- Enforce one identity user per customer; enforce uniqueness of the CustomerId FK.
- The approved normalized unique phone rule remains enforced on the authoritative customer profile/account identity according to the final TD-22 mapping. Do not assume email is required or globally unique unless the approved requirement says so.

### 3.3 Employee roles and permissions

- Keep `org.Roles` as the single employee role catalogue.
- Map the ASP.NET Core Identity role entity to `org.Roles` using a custom `IdentityRole<Guid>` subtype and explicit EF Core property mapping.
- Map Identity's normalized role name to the stable `RoleCode` (or an explicitly approved normalized-code column); do not duplicate role IDs or role definitions in a separate `AspNetRoles` table.
- Retain `org.Permissions`, `org.RolePermissions` and `org.EmployeePermissionGrants` as the fine-grained authorization catalogue and audited grant/revoke history.
- Identity roles provide the role membership integration; permission evaluation additionally checks branch assignment, explicit grants/revocations, resource ownership, approval authority and Offline policy.
- Customer Identity has no navigation or foreign-key path to employee roles or permissions.

### 3.4 Identity relationship tables

Use explicit custom Identity entity types and Fluent API mappings for the employee Identity store. Any required user-role, user-claim, user-login, role-claim and user-token persistence tables must:
- have a Guid surrogate primary key;
- retain required Identity uniqueness constraints (for example normalized login/provider key and user/role membership);
- use Guid FKs to the owning Identity user/role;
- be mapped under the appropriate module schema;
- be included in the final table inventory and ERD before physical implementation.

Customer Identity uses only the Identity tables/features it actually needs; do not create unused role/claim/login/token tables merely by convention. If external login or token persistence is enabled, document its table and retention requirements first.

## 4. Authentication and token boundaries

- Register separate employee and customer authentication schemes/audiences (for example employee API tokens and customer API tokens).
- Employee APIs accept only employee authentication; customer APIs accept only customer authentication. A token must carry an unambiguous identity type and audience, and authorization policies must validate both.
- Employee authorization uses roles/permissions plus branch/device/session context. Customer authorization is limited to the authenticated customer's own resources and explicitly public capabilities.
- Employee sessions remain represented by `org.EmployeeSessions` with the required identity-user GUID reference.
- Customer session/revocation persistence is designed separately in the CRM/security schema if required by the chosen token/session strategy; do not overload `org.EmployeeSessions`.
- Never accept a customer token as proof of employee identity, even if the underlying user GUID happens to match a value in another table.

## 5. Central and Branch persistence boundary

- Identity credential stores are Central-only. Branch databases must not contain employee or customer password hashes, Identity security stamps, refresh-token secrets or reusable central credentials.
- Branch local authentication must use a dedicated Offline credential-verification design, not a copy of the central Identity user row.
- The Branch authorization snapshot may contain only the minimum employee identifier, active status, allowed branch assignments, role/permission snapshot, device context and policy/version/expiry data needed for local authorization.
- Offline credential-verifier material must be separately protected, purpose-built, revocable, time-bounded and excluded from ordinary synchronization payloads/logs. Its cryptographic construction and at-rest protection require a focused threat review before implementation.
- Customer login and customer account recovery remain Central-only.

## 6. ASP.NET Core Identity implementation constraints

1. Use Guid generic Identity types throughout; never use the default string or integer key configuration.
2. Replace default composite-key Identity join entities with custom entities having Guid surrogate PKs, preserving unique indexes on their natural key combinations.
3. Explicitly map table/schema/column names and constraint/index names; no convention-generated production schema.
4. Keep Identity EF Core mappings in `Central.Infrastructure`; Domain and Application do not reference EF Core or Identity persistence types.
5. Use Identity managers behind Application ports/use cases; controllers must not call DbContext or manipulate password hashes directly.
6. Password hashing uses the supported ASP.NET Core Identity password hasher; no custom password cryptography.
7. Password reset, phone verification, lockout, credential rotation and revocation are audited and centrally governed.
8. Do not persist bearer tokens or refresh tokens in plaintext. Select a revocable token/session design before implementation.
9. Seed stable employee roles and permissions idempotently; never seed production user passwords or hard-coded credentials.
10. All user/role/session/claim/token identifiers and references remain Guid/uniqueidentifier.

## 7. Required schema follow-up

Before generating entities or migrations, update the logical inventory/ERD to include:
- employee Identity user table and its one-to-one relationship to `org.Employees`;
- customer Identity user table and its one-to-one relationship to `crm.Customers`;
- employee Identity role mapping to `org.Roles`;
- only the required custom Identity relationship tables with Guid PKs;
- customer session/revocation table only if required by the selected session/token strategy;
- a separate protected Offline credential-verifier store only after its threat model and data lifecycle are approved.

Do not treat framework-generated Identity schema as invisible infrastructure; every physical table must be inventoried, owned, reviewed and migrated explicitly.

## 8. Acceptance tests

- Customer token is rejected by every employee/branch administration endpoint.
- Employee token is rejected by customer-only endpoints unless an explicitly authorized employee workflow is defined.
- Employee user maps to exactly one employee profile; customer user maps to exactly one customer profile.
- Customer cannot receive employee role membership.
- Role/permission revocation is enforced centrally and propagated according to the approved Offline policy.
- Branch DB contains no central password hashes, Identity security stamps or reusable refresh/access tokens.
- Expired Offline credential material is rejected and cannot be extended locally.
- Every Identity table has a Guid PK and all FKs use uniqueidentifier.
- Identity migrations preserve the single `org.Roles` catalogue and do not create a duplicate role table.
- Account disablement and credential/session revocation are audited.

## 9. Explicitly deferred

This proposal does not select:
- JWT versus cookie/session token format;
- access/refresh token lifetime and revocation implementation;
- MFA policy/provider;
- Offline verifier cryptographic construction, key protection and exact local table fields;
- SMS/OTP delivery provider;
- external social login;
- detailed customer session retention.

These remain focused security/integration decisions and must not be guessed during initial project scaffolding.

## 10. Approval boundary

Approval of TD-29 accepts the recommended Identity persistence boundary and employee/customer separation. It does not authorize generating Identity tables, EF Core entities, DDL or migrations. The logical inventory and ERD must first be updated and approved, then the implementation can be built and tested.

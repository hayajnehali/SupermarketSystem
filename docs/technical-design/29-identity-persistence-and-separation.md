# TD-29 — Unified Identity Account Model

**Version:** 0.2  
**Status:** Approved by owner (2026-11-08)  
**Date:** 2026-11-08  
**Depends on:** Approved TD-06, TD-15, TD-22, TD-27 and TD-28; .NET 10 / ASP.NET Core Identity baseline in TD-02.

## 1. Approved decision

Use one ASP.NET Core Identity user store with Guid keys for both employees and customers. An account must be exactly one account type: Employee or Customer. The same account cannot represent both.

This replaces the earlier proposal for separate employee and customer Identity user tables.

The account type is an explicit AccountType discriminator on the unified Identity user. It is not inferred only from role membership.

- One IdentityUser<Guid>-based record in the unified auth.Users table.
- An Employee account has exactly one linked org.Employees profile and no crm.Customers profile.
- A Customer account has exactly one linked crm.Customers profile and no org.Employees profile.
- The application and database enforce these mutually exclusive relationships.

Changing an account from Employee to Customer (or vice versa) is not an ordinary profile edit. It requires a dedicated, audited lifecycle operation that handles existing relationships, sessions and permissions. The default policy is to create a separate account rather than convert an existing one.

## 2. Rationale

A unified Identity user store is appropriate because both account types share credential management, password hashing, lockout, security stamps, recovery and account lifecycle primitives.

The business profiles remain separate because employees and customers have different attributes and workflows. This is not a reason to duplicate authentication users.

A shared user table does not by itself make the system insecure. Security is enforced through account type, authentication schemes/audiences, authorization policies, resource ownership and database constraints.

## 3. Logical entity and table mapping

### 3.1 Unified Identity user

- Custom Identity user type based on IdentityUser<Guid>.
- Table: auth.Users.
- PK: UserId as Guid / uniqueidentifier.
- Required AccountType discriminator: Employee or Customer.
- Shared credential and security fields (normalized username/email, password hash, security stamp, lockout and configured two-factor fields) are stored once.
- All Identity user identifiers and references use Guid / uniqueidentifier.
- Identity credentials remain Central-only; Branch databases do not store password hashes, security stamps or reusable central credentials.

The final physical column names and schema mapping must be reconciled with TD-13, TD-15 and TD-22 before implementation.

### 3.2 Mutually exclusive business profiles

- org.Employees contains employee business data and has a unique required FK UserId to auth.Users.
- crm.Customers contains customer business data and has a unique required FK UserId to auth.Users.
- Every active Identity account must have exactly one matching profile according to AccountType.
- An Employee account must not have a row in crm.Customers.
- A Customer account must not have a row in org.Employees.
- Enforce one-to-one uniqueness on each profile's UserId, and enforce cross-table exclusivity using a database-supported design (for example a shared account-subtype registry with a unique UserId, or an equivalent transactionally enforced constraint strategy). Do not rely only on UI validation.
- Account creation is atomic: create the Identity user and its one matching profile in one application transaction.

SQL Server CHECK constraints cannot directly validate rows in another table, so the final physical design must choose and test an enforceable cross-table invariant. A simple CHECK constraint is not sufficient.

### 3.3 Roles and permissions

- Keep org.Roles, org.Permissions, org.RolePermissions and org.EmployeePermissionGrants as the employee operational RBAC catalogue already defined by the organization/access design.
- Employee role membership is available only to accounts whose AccountType is Employee.
- Customer access is determined by AccountType = Customer and customer-specific authorization policies; customers do not receive employee operational roles.
- If Identity role membership is mapped to org.Roles, use a custom IdentityRole<Guid> entity and explicit EF Core mapping. Do not create a duplicate employee role catalogue.
- Authorization rejects attempts to grant an employee role to a Customer account in both the application layer and persistence workflow.
- A role assignment cannot change AccountType; account classification and role membership are separate concepts.

### 3.4 Identity relationship tables

Use explicit custom Identity entity types and Fluent API mappings. Any required user-role, user-claim, user-login, role-claim and user-token tables must:
- have Guid surrogate primary keys, in line with the approved Guid-PK standard;
- retain required Identity uniqueness constraints;
- use Guid FKs to the unified user and applicable role;
- be explicitly mapped and included in the final inventory and ERD.

Create only the Identity features actually required. Do not add unused external-login, claim, token or role tables by convention without documenting their purpose.

## 4. Authentication and authorization boundaries

- Use separate employee and customer authentication schemes and/or token audiences even though both authenticate against the same user store.
- Employee endpoints accept only Employee accounts; customer endpoints accept only Customer accounts.
- Tokens carry an unambiguous account type and audience. Server-side policies validate both; a client-supplied account type is never trusted.
- Employee authorization uses employee roles/permissions plus branch, device, session and Offline context.
- Customer authorization is limited to the authenticated customer's own resources and explicitly public capabilities.
- Employee sessions remain represented by org.EmployeeSessions with a Guid reference to auth.Users.
- Customer session/revocation persistence is designed according to the selected token strategy; do not overload org.EmployeeSessions.
- Reject a token for the wrong endpoint even if it was signed by the same issuer.

## 5. Central and Branch persistence boundary

- The unified Identity credential store is Central-only.
- Branch databases must not contain employee or customer password hashes, Identity security stamps, refresh-token secrets or reusable central credentials.
- Branch local employee authentication uses a dedicated Offline credential-verification design, not a copy of the central Identity user row.
- Branch authorization snapshots contain only the minimum approved employee identifier, active status, allowed branch assignments, role/permission snapshot, device context and policy/version/expiry data.
- Customer login and account recovery remain Central-only.
- Offline verifier material remains separately protected, purpose-built, revocable and time-bounded; its cryptographic construction and at-rest protection require a focused threat review before implementation.

## 6. ASP.NET Core Identity implementation constraints

1. Use Guid generic Identity types throughout.
2. Use custom Identity relationship entities with Guid surrogate PKs where required, preserving natural-key uniqueness.
3. Explicitly map table/schema/column names, constraints and indexes.
4. Keep Identity EF Core mappings in Central.Infrastructure; Domain and Application do not reference EF Core persistence types.
5. Use Identity managers behind Application use cases/ports; controllers do not manipulate password hashes directly.
6. Use the supported ASP.NET Core Identity password hasher; do not implement custom password cryptography.
7. Audit password reset, phone verification, lockout, credential rotation, account-type lifecycle operations and revocation.
8. Do not persist bearer or refresh tokens in plaintext.
9. Seed stable employee roles and permissions idempotently; never seed production user passwords or hard-coded credentials.
10. Enforce account-type compatibility for every employee role assignment and every employee/customer endpoint.

## 7. Required schema follow-up

Before generating entities or migrations, update the logical inventory and ERDs to include:
- one unified auth.Users Identity table with AccountType;
- one-to-one employee and customer profile relationships;
- the enforceable database design for cross-profile exclusivity;
- employee role membership mapped to the existing org.Roles catalogue;
- only the required custom Identity relationship tables with Guid PKs;
- session/revocation persistence only where required by the selected token strategy;
- a separate protected Offline credential-verifier store only after its threat model and lifecycle are approved.

Every physical table must be inventoried, owned, reviewed and migrated explicitly.

## 8. Acceptance tests

- An account can be created as Employee or Customer, never both.
- An Employee account has exactly one org.Employees profile and no crm.Customers profile.
- A Customer account has exactly one crm.Customers profile and no org.Employees profile.
- Concurrent or direct persistence attempts cannot attach both profiles to one user.
- Customer accounts cannot receive employee role membership.
- Employee token is rejected by customer-only endpoints; customer token is rejected by employee/branch endpoints.
- Employee user can have only employee-compatible roles; account type cannot be changed by role assignment.
- Branch DB contains no central password hashes, Identity security stamps or reusable central tokens.
- Expired Offline credential material is rejected and cannot be extended locally.
- Every Identity table has a Guid PK and all FKs use uniqueidentifier.
- Identity mapping reuses the approved org.Roles catalogue without creating a duplicate role table.
- Account disablement, role changes and account-type lifecycle operations are audited.

## 9. Explicitly deferred

This document does not select:
- JWT versus cookie/session token format;
- access/refresh token lifetime and revocation implementation;
- MFA policy/provider;
- Offline verifier cryptographic construction, key protection and exact local table fields;
- SMS/OTP delivery provider;
- external social login;
- detailed customer session retention;
- final SQL Server enforcement mechanism for cross-table profile exclusivity (to be selected and tested during physical schema reconciliation).

These must be resolved before implementation; do not guess during project scaffolding.

## 10. Approval boundary

This approved decision establishes one unified Identity account store and separate Employee/Customer business profiles, with strict mutual exclusion. It does not by itself authorize generating Identity tables, EF Core entities, DDL or migrations. The logical inventory and ERDs must first be reconciled with this decision and approved; implementation can then be built and tested.

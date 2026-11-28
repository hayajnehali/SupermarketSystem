# TD-32A — Central Identity and Organization Physical Schema

**Version:** 0.1  
**Status:** Delegated design approval — ready for implementation mapping, subject to the final TD-32 package gate.  
**Date:** 2026-11-28  
**Depends on:** TD-15, TD-26, TD-27, TD-28, TD-29, TD-30 and TD-31.

## 1. Scope and authority

This supplement resolves the physical implementation details for the first Central schema group: Identity, branches, employees, RBAC, devices, sessions and runtime policy. It supersedes conflicting older wording in TD-15. It does not authorize a migration until the complete Central/Branch schema package is approved.

## 2. Reconciliation decisions

1. TD-29 is authoritative over the old TD-15 section 4.4. Employee/Customer profile exclusivity is enforced by Application Layer workflows only. No cross-table XOR CHECK, trigger or shared subtype registry.
2. The Identity store is Central-only. Branch databases contain no Central password hashes, security stamps, reusable access/refresh tokens or Identity credential records.
3. `auth.Users` is one custom `IdentityUser<Guid>` table with required `AccountType` (Employee=1, Customer=2). Employee and Customer profile tables retain their unique required `UserId` FKs.
4. Login is `UserName + Password` only. `UserName` and `NormalizedUserName` are required and unique. Email and phone are optional contact methods, not login identifiers.
5. Login is allowed only when the account is administratively active and at least one contact method is confirmed. Confirmation flags are independent of administrative active status.
6. Lockout is enabled: five failed attempts cause a 15-minute lockout. Idle timeout is 30 minutes for both account types. Disabled accounts revoke all active sessions and cannot start new sessions.
7. Password minimum length is 12. Force password change after account creation and password reset. Password reset uses a one-time temporary token/code. MFA remains disabled.
8. Successful and failed login attempts are persisted. Unknown usernames are retained with nullable UserId. Passwords, OTP plaintext, tokens and secrets are never logged.
9. Employee role membership uses `auth.UserRoles` and the existing `org.Roles` catalogue. Do not create a second role table.
10. Only Identity relationship tables required by enabled features are created. No external-login, claim, role-claim or persisted token tables are added in this release unless a later approved feature requires them.

## 3. Central Identity tables

### 3.1 `auth.Users`

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| UserId | uniqueidentifier | No | PK; sequential Guid |
| UserName | nvarchar(256) | No | Login name; original/display form |
| NormalizedUserName | nvarchar(256) | No | Required unique normalized login |
| Email | nvarchar(254) | Yes | Contact only; not login |
| NormalizedEmail | nvarchar(254) | Yes | Non-unique lookup index only |
| EmailConfirmed | bit | No | Default false |
| PhoneNumber | nvarchar(32) | Yes | Normalized contact phone; not login |
| PhoneNumberConfirmed | bit | No | Default false |
| PasswordHash | nvarchar(512) | Yes | ASP.NET Core Identity password hash only |
| SecurityStamp | nvarchar(64) | No | Identity security stamp |
| ConcurrencyStamp | nvarchar(64) | No | Identity concurrency value |
| AccountType | tinyint | No | Employee=1; Customer=2 |
| LockoutEnabled | bit | No | Default true |
| LockoutEndUtc | datetime2(3) | Yes | UTC lockout end |
| AccessFailedCount | int | No | Default 0; non-negative |
| TwoFactorEnabled | bit | No | Default false |
| IsActive | bit | No | Administrative account status; independent of contact confirmation |
| MustChangePassword | bit | No | Default true for new/reset credentials |
| CreatedAtUtc | datetime2(3) | No | UTC |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Indexes and constraints**
- Clustered PK on UserId.
- Unique index on NormalizedUserName.
- Non-unique index on NormalizedEmail where not null.
- Check AccountType in (1,2); AccessFailedCount >= 0.
- Account activation/login rule (IsActive and at least one confirmed contact method) is enforced by Identity/Application workflows, not a cross-table check.
- Map Identity's `Id` property explicitly to SQL column `UserId`; use explicit column names and lengths.
- Map Identity's LockoutEnd value to UTC `datetime2(3)` through a tested converter; never persist local time.

### 3.2 `auth.UserRoles`

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| UserRoleId | uniqueidentifier | No | PK; sequential Guid |
| UserId | uniqueidentifier | No | FK → auth.Users |
| RoleId | uniqueidentifier | No | FK → org.Roles |
| AssignedAtUtc | datetime2(3) | No | UTC |
| AssignedByUserId | uniqueidentifier | No | Central actor |

**Indexes and constraints**
- Unique (UserId, RoleId).
- FKs are local Central FKs; no cascade delete.
- Application rejects role assignment to Customer accounts and checks AccountType=Employee.

### 3.3 `auth.LoginAttempts`

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| LoginAttemptId | uniqueidentifier | No | PK; sequential Guid |
| UserId | uniqueidentifier | Yes | FK → auth.Users when username resolves; null for unknown account |
| AttemptedUserName | nvarchar(256) | No | Normalized attempted username for audit/search |
| AttemptedAtUtc | datetime2(3) | No | UTC |
| DeviceId | uniqueidentifier | Yes | Registered device reference when known |
| IpAddress | nvarchar(45) | Yes | IPv4/IPv6 text |
| Result | tinyint | No | Success=1; Failure=2; LockedOut=3 |
| FailureReason | nvarchar(80) | Yes | Stable non-secret code |
| CorrelationId | uniqueidentifier | Yes | Request trace |
 
**Indexes and constraints**
- Index (AttemptedAtUtc).
- Index (UserId, AttemptedAtUtc).
- Index (AttemptedUserName, AttemptedAtUtc).
- Optional FK DeviceId → org.RegisteredDevices when the device is known and registered.
- Append-only for ordinary application roles; no password, OTP, access token or secret columns.
- Retention follows the approved Data Retention policy; no unapproved fixed retention period is introduced.

## 4. Existing Organization schema retained

The following detailed definitions and relationships from TD-15 are retained unless amended below:
- `org.Branches`, `org.Employees`, `org.EmployeeBranchAssignments`
- `org.Roles`, `org.Permissions`, `org.RolePermissions`, `org.EmployeePermissionGrants`
- `org.RegisteredDevices`, `org.EmployeeSessions`, `org.RuntimePolicies`
- Branch-local `org.BranchAccessSnapshots`, `org.BranchAccessSnapshotEmployees`, `org.BranchAccessSnapshotPermissions`, `org.BranchRuntimePolicies`.

Physical mapping refinements:
- `org.Employees.UserId` is a unique required FK to `auth.Users.UserId`; `crm.Customers.UserId` is separately unique and required. The account/profile compatibility check is performed by application use cases in one transaction.
- `org.Roles` is the Identity role store as well as the business RBAC catalogue. Map the same RoleId to IdentityRole<Guid>; add required Identity `NormalizedName` and `ConcurrencyStamp` fields to this table. Do not create `auth.Roles`.
- `auth.UserRoles.RoleId` references `org.Roles.RoleId`. The composite (UserId,RoleId) unique index is mandatory.
- `org.EmployeeSessions` references the same Employee and Identity user pair through the approved composite relationship. No plaintext tokens are stored.
- Actor references to auth.Users are Central-local FKs only where the actor is guaranteed to be a user row; nullable system/service actors use an explicit actor-kind/reference strategy rather than a fake user ID.
- Branch access snapshots contain only the minimum approved employee, branch, permission, device and expiry data. No password verifier design is inferred here.

## 5. Identity tables intentionally omitted

No `auth.UserClaims`, `auth.UserLogins`, `auth.UserTokens`, or `auth.RoleClaims` table is included in the first release because the approved flows use username/password, employee RBAC from org.Roles/permissions, and token generation without a persisted Identity token store. If a future feature requires one of these capabilities, add it with Guid PKs and explicit constraints in a separate reviewed change.

## 6. EF Core mapping requirements

- Identity mappings exist only in Central.Infrastructure.
- Use custom Identity types with Guid keys and explicit table/schema/column mappings.
- Map `org.Roles` as the single Identity role store; never duplicate the role catalogue.
- Configure `auth.UserRoles` with Guid surrogate PK and unique UserId/RoleId.
- Configure LoginAttempts as append-only.
- Use SQL Server integration tests for normalized username uniqueness, lockout transitions, disabled-session revocation, role/account-type compatibility, user/profile creation transaction and unknown-user login auditing.
- The final `CentralDbContext` includes these entities plus all other approved Central tables; no partial production migration is generated from this supplement alone.

## 7. Approval record

The design authority granted by the owner on 2026-11-28 is applied to this physical Identity/Organization specification. The decisions above are approved for implementation mapping. Migration generation remains gated on the complete Central and Branch schema packages.

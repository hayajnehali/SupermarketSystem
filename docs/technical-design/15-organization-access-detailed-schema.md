# TD-15 — Detailed Logical Schema: Organization and Access

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Central organization and employee administration, branch assignment, RBAC catalogue, device registry and branch authorization snapshots.  
**Depends on:** Approved BRS Unit 01, TD-03, TD-04, TD-06, TD-13 and TD-14.

> This is a logical/physical table specification for review. It is not executable DDL, EF Core source code or a migration. Identity protocol, token implementation and the secure Offline credential-verifier design remain governed by TD-06 and are not invented here.

## 1. Shared SQL conventions

| Concern | Rule |
|---|---|
| Primary keys | Every table uses a client-generated sequential `Guid` / SQL Server `uniqueidentifier` primary key. No `IDENTITY` primary keys. |
| Foreign keys | `uniqueidentifier` matching the referenced Guid PK; local database only. |
| UTC timestamps | `datetime2(3)`; application values must be UTC. |
| Business dates | `date` where a calendar date is intended, not an instant. |
| Status values | `tinyint` with stable numeric constants in code and documented values; never persist localized enum text. |
| Currency/quantity | Use fixed precision `decimal` values with scale defined by the owning domain; never use float/double for money or quantity. |
| Mutable rows | Add SQL Server `rowversion` for optimistic concurrency where concurrent updates are possible. |
| Audit | Created/updated actor and UTC timestamp fields where the table is mutable; immutable event/history tables retain their own actor/time. |
| Deletion | No cascade delete for employee, branch, role, permission, device, session or audit history. Deactivate/revoke and preserve references. |
| Naming | Explicit schema and table names; explicit column names and constraints in EF Core configuration. |

Sequential Guid generation follows the approved TD-12 approach using the EF Core SQL Server sequential GUID value generator. It is a Guid, not a numeric sequence.

## 2. Database placement

| Table | Central DB | Branch DB | Notes |
|---|---:|---:|---|
| `org.Branches` | Authoritative | No | Central branch master. |
| `org.Employees` | Authoritative | No direct master table | Branch uses a limited authorization snapshot, not a second employee authority. |
| `org.EmployeeBranchAssignments` | Authoritative | No | Central effective-dated assignments. |
| `org.Roles` | Authoritative | Snapshot only | Central role catalogue. |
| `org.Permissions` | Authoritative | Snapshot only | Central permission catalogue. |
| `org.RolePermissions` | Authoritative | Snapshot only | Central RBAC mapping. |
| `org.EmployeePermissionGrants` | Authoritative | Snapshot only | Central additional grants/revocations. |
| `org.RegisteredDevices` | Authoritative | Validated local snapshot | Branch checks its registered local devices against the last approved data. |
| `org.EmployeeSessions` | Authoritative session/revocation record | Minimal secure local validation state only | Exact Offline verifier/storage remains in TD-06. |
| `org.BranchAccessSnapshots` | Publication/version metadata may be recorded centrally | Active local snapshot header | Separate records; no cross-database FK. |
| `org.BranchAccessSnapshotEmployees` | No | Local snapshot entries | Contains only the approved employee data needed for local authorization. |
| `org.BranchAccessSnapshotPermissions` | No | Local snapshot permissions | Effective permission codes for the corresponding local snapshot employee. |
| `org.RuntimePolicies` | Authoritative | No | Centrally governed Offline/runtime policy definitions and version history. |
| `org.BranchRuntimePolicies` | No | Approved local projection | Branch applies centrally published runtime policy versions; it cannot edit or extend them. |

A branch database does not contain the full Central employee directory. It contains only the minimum approved employee/role/permission/device state required for branch operation during the valid Offline period.

## 3. Detailed table specifications

### 3.1 `org.Branches` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchId | uniqueidentifier | No | PK; sequential Guid |
| BranchCode | nvarchar(10) | No | Unique; invoice prefix such as BR01; immutable after operational use unless controlled migration is approved |
| Name | nvarchar(150) | No | Display name |
| Phone | nvarchar(32) | Yes | Normalized phone where available |
| Email | nvarchar(254) | Yes | Validated email where available |
| Address | nvarchar(500) | Yes | Branch address text |
| TimeZoneId | nvarchar(100) | No | Valid time-zone identifier used for branch-local business date and SequenceYear calculation |
| Status | tinyint | No | Active / Suspended; stable constants |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor identity reference |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| UpdatedByUserId | uniqueidentifier | Yes | Central actor identity reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- PK clustered on `BranchId`.
- Unique index on `BranchCode`.
- Check constraint for valid `Status`.
- No physical delete. Suspension blocks new operational transactions but preserves all historical records.
- Actor identity references are resolved within the Central identity boundary; exact identity table mapping is deferred to Section 4.

### 3.2 `org.Employees` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| EmployeeId | uniqueidentifier | No | PK; sequential Guid |
| EmployeeNumber | nvarchar(30) | No | Unique employee/business identifier |
| FullName | nvarchar(200) | No | Employee display/legal name as maintained by Central |
| Phone | nvarchar(32) | Yes | Contact number |
| Email | nvarchar(254) | Yes | Contact email |
| EmploymentStatus | tinyint | No | Active / Suspended / Ended; stable constants |
| HireDate | date | Yes | Employment date if recorded |
| EndDate | date | Yes | Must not precede HireDate |
| CreatedAtUtc | datetime2(3) | No | UTC |
| CreatedByUserId | uniqueidentifier | No | Central actor identity reference |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| UpdatedByUserId | uniqueidentifier | Yes | Central actor identity reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- PK clustered on `EmployeeId`.
- Unique index on `EmployeeNumber`.
- Optional filtered unique indexes for normalized Phone/Email only if the approved employee identity policy requires uniqueness; do not assume employee phone is globally unique from the BRS.
- Check constraint: `EndDate IS NULL OR HireDate IS NULL OR EndDate >= HireDate`.
- Suspend/end employee rather than deleting; session revocation and new sign-in denial are enforced by the identity/session subsystem.
- The employee profile is separate from authentication credentials.

### 3.3 `org.EmployeeBranchAssignments` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| EmployeeBranchAssignmentId | uniqueidentifier | No | PK; sequential Guid |
| EmployeeId | uniqueidentifier | No | FK → `org.Employees.EmployeeId` |
| BranchId | uniqueidentifier | No | FK → `org.Branches.BranchId` |
| EffectiveFromUtc | datetime2(3) | No | Assignment start instant |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end; null means currently effective |
| AssignedByUserId | uniqueidentifier | No | Central actor identity reference |
| Reason | nvarchar(500) | Yes | Transfer/assignment reason |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- PK clustered on `EmployeeBranchAssignmentId`.
- FK to Employees and Branches; restrict delete.
- Unique index on `EmployeeId, BranchId, EffectiveFromUtc`.
- Check constraint: `EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc`.
- Do not impose one active branch per employee: BRS allows assignment to one or more branches. The active branch for a work session is a separate single-valued session context.

### 3.4 `org.Roles` and `org.Permissions` — Central

**`org.Roles`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| RoleId | uniqueidentifier | No | PK; sequential Guid |
| RoleCode | nvarchar(50) | No | Unique stable code, e.g. CENTRAL_ADMIN, BRANCH_MANAGER, CASHIER, INVENTORY_CLERK, PRICING_CLERK |
| Name | nvarchar(120) | No | Display name |
| Description | nvarchar(500) | Yes | Description |
| IsSystemRole | bit | No | Protects seeded core roles from accidental deletion |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

**`org.Permissions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| PermissionId | uniqueidentifier | No | PK; sequential Guid |
| PermissionCode | nvarchar(120) | No | Unique stable code; not localized |
| ModuleCode | nvarchar(60) | No | Owning module identifier |
| ResourceCode | nvarchar(80) | No | Protected resource |
| ActionCode | nvarchar(60) | No | Operation such as read/create/approve/close |
| Description | nvarchar(500) | Yes | Description |
| IsActive | bit | No | Deactivation only |
| CreatedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique `RoleCode` and `PermissionCode`.
- Permission codes are immutable once referenced by authorization policy; changes require controlled migration.
- Seed the five approved core roles; do not infer permission assignments from role names alone.
- The physical integration between `org.Roles` and ASP.NET Core Identity's role store must use the same role identity; do not maintain a second competing role catalogue.

### 3.5 `org.RolePermissions` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| RolePermissionId | uniqueidentifier | No | PK; sequential Guid |
| RoleId | uniqueidentifier | No | FK → `org.Roles.RoleId` |
| PermissionId | uniqueidentifier | No | FK → `org.Permissions.PermissionId` |
| GrantedAtUtc | datetime2(3) | No | UTC |
| GrantedByUserId | uniqueidentifier | No | Central actor identity reference |

**Constraints and indexes**
- Unique `RoleId, PermissionId`; a role cannot receive the same permission twice.
- FKs restrict delete; remove assignment through an audited revoke/deactivation workflow rather than cascading.

### 3.6 `org.EmployeePermissionGrants` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| EmployeePermissionGrantId | uniqueidentifier | No | PK; sequential Guid |
| EmployeeId | uniqueidentifier | No | FK → `org.Employees.EmployeeId` |
| PermissionId | uniqueidentifier | No | FK → `org.Permissions.PermissionId` |
| Effect | tinyint | No | Grant / Revoke |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |
| Reason | nvarchar(500) | No | Required audit reason |
| GrantedByUserId | uniqueidentifier | No | Central actor identity reference |
| CreatedAtUtc | datetime2(3) | No | UTC |

**Constraints and indexes**
- FK to Employee and Permission.
- Check `EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc`.
- Index `EmployeeId, PermissionId, EffectiveFromUtc`.
- Preserve both grants and revocations as history. Effective permission evaluation combines role permissions, active employee grants/revocations, branch assignment and operation context.

### 3.7 `org.RegisteredDevices` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| DeviceId | uniqueidentifier | No | PK; sequential Guid |
| BranchId | uniqueidentifier | No | FK → `org.Branches.BranchId` |
| DeviceCode | nvarchar(50) | No | Unique within branch |
| DisplayName | nvarchar(120) | No | Device label |
| DeviceType | tinyint | No | POS / approved operational device types |
| CredentialReference | nvarchar(200) | No | Reference to protected credential/certificate storage; never store plaintext device secret |
| CredentialThumbprint | nvarchar(128) | Yes | Unique certificate/public-key fingerprint when that mechanism is selected |
| Status | tinyint | No | Pending / Active / Suspended / Revoked |
| RegisteredAtUtc | datetime2(3) | No | UTC |
| LastSeenAtUtc | datetime2(3) | Yes | UTC |
| RevokedAtUtc | datetime2(3) | Yes | UTC |
| RevokedByUserId | uniqueidentifier | Yes | Central actor identity reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique `BranchId, DeviceCode`.
- Unique filtered `CredentialThumbprint` when non-null.
- FK Branch; restrict delete.
- Device status transitions and credential rotation are audited.
- Credential technology is not selected here; store only a protected reference/fingerprint.

### 3.8 `org.EmployeeSessions` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| EmployeeSessionId | uniqueidentifier | No | PK; sequential Guid |
| EmployeeId | uniqueidentifier | No | FK → `org.Employees.EmployeeId` |
| IdentityUserId | uniqueidentifier | No | Identity user GUID reference; FK added only if identity user table is in this same database |
| DeviceId | uniqueidentifier | Yes | FK → `org.RegisteredDevices.DeviceId` when session is device-bound |
| ActiveBranchId | uniqueidentifier | No | FK → `org.Branches.BranchId`; exactly one active branch context |
| IssuedAtUtc | datetime2(3) | No | UTC |
| LastCentralVerifiedAtUtc | datetime2(3) | No | UTC; source for approved 24-hour local login validity |
| ExpiresAtUtc | datetime2(3) | No | Session expiry |
| RevokedAtUtc | datetime2(3) | Yes | UTC |
| RevokedByUserId | uniqueidentifier | Yes | Central actor identity reference |
| RevocationReason | nvarchar(300) | Yes | Reason |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Index `EmployeeId, RevokedAtUtc, ExpiresAtUtc`.
- Index `IdentityUserId`.
- FK Employee, Branch and optional Device; no cascade delete.
- Multiple concurrent sessions per employee are allowed; each session has one active branch context.
- Branch changes update ActiveBranchId and emit an audit event containing previous branch, new branch, employee and time.
- Do not store bearer/refresh token plaintext in this table. Exact session/token persistence is part of TD-06.

### 3.9 `org.BranchAccessSnapshots` — Branch

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchAccessSnapshotId | uniqueidentifier | No | PK; sequential Guid |
| SourceVersion | bigint | No | Monotonic Central publication version |
| GeneratedAtUtc | datetime2(3) | No | Central generation time |
| AppliedAtUtc | datetime2(3) | No | Local durable apply time |
| ValidUntilUtc | datetime2(3) | No | Must respect the approved Offline/login limits |
| PayloadHash | binary(32) | No | SHA-256 of canonical snapshot |
| IsActive | bit | No | Exactly one active snapshot per branch |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique `SourceVersion`.
- Enforce one active snapshot with a filtered unique index on the active marker.
- Do not extend validity locally. Central policy determines Offline duration and access validity.

### 3.10 `org.BranchAccessSnapshotEmployees` and `org.BranchAccessSnapshotPermissions` — Branch

**`org.BranchAccessSnapshotEmployees`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchAccessSnapshotEmployeeId | uniqueidentifier | No | PK; sequential Guid |
| BranchAccessSnapshotId | uniqueidentifier | No | FK → `org.BranchAccessSnapshots` |
| EmployeeId | uniqueidentifier | No | Central employee GUID reference; no cross-database FK |
| EmployeeNumber | nvarchar(30) | No | Minimal display/lookup value |
| DisplayName | nvarchar(200) | No | Minimum identity display data |
| EmploymentStatus | tinyint | No | Last approved status |
| SnapshotEffectiveAtUtc | datetime2(3) | No | Central effective time |

**`org.BranchAccessSnapshotPermissions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchAccessSnapshotPermissionId | uniqueidentifier | No | PK; sequential Guid |
| BranchAccessSnapshotEmployeeId | uniqueidentifier | No | FK → local snapshot employee |
| BranchId | uniqueidentifier | No | Branch identity value validated against hosting branch configuration; no Central FK |
| PermissionCode | nvarchar(120) | No | Stable central permission code |
| Effect | tinyint | No | Effective permission result |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| EffectiveToUtc | datetime2(3) | Yes | Exclusive end |

**Constraints and indexes**
- Unique `BranchAccessSnapshotId, EmployeeId` in snapshot employee table.
- Unique `BranchAccessSnapshotEmployeeId, PermissionCode` in permission table.
- FK from snapshot employee to local snapshot header; FK from snapshot permission to local snapshot employee.
- No FK to Central EmployeeId, RoleId, PermissionId or BranchId.
- Do not put password hashes, OTPs, access tokens or reusable credentials in these tables. The secure Offline authentication verifier is not designed in TD-15.

### 3.11 `org.BranchRuntimePolicies` — Branch projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchRuntimePolicyId | uniqueidentifier | No | PK; sequential Guid |
| PolicyCode | nvarchar(100) | No | Stable policy identifier |
| PolicyVersion | bigint | No | Monotonic Central version |
| PolicyValue | nvarchar(max) | No | Versioned structured policy payload |
| EffectiveFromUtc | datetime2(3) | No | UTC |
| ReceivedAtUtc | datetime2(3) | No | UTC |
| PayloadHash | binary(32) | No | SHA-256 |
| IsActive | bit | No | Current applied version |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and indexes**
- Unique `PolicyCode, PolicyVersion`.
- Filtered unique index to permit only one active version per PolicyCode.
- Branch cannot create, edit, extend or publish a Central policy; it only applies an authenticated versioned copy.

## 4. Identity integration boundary

The BRS requires employee accounts and customer accounts; TD-06 keeps employee, customer, device and service identities logically distinct. The detailed identity provider, password/token/session protocol and Offline credential-verifier remain deferred.

For implementation, if ASP.NET Core Identity is selected:
- Configure `Guid` keys for every Identity entity, including user, role, user-role join, claims, logins and tokens. The default integer/composite Identity key shapes must not violate the project-wide Guid-PK rule.
- Map the employee role store to the same `org.Roles` identity catalogue; do not maintain a duplicate set of role IDs in a second role table.
- Keep customer credentials logically separate from employee credentials and employee authorization roles.
- Every Identity persistence table that is introduced must be listed in the final database inventory and have a Guid PK; do not silently accept framework-default integer primary keys.
- Keep Branch authorization snapshots free of password hashes and plaintext or reusable authentication tokens. Offline credential validation requires a separately reviewed secure design.

No final authentication provider, token format, credential storage, MFA method or Offline verifier is selected by this document.

## 5. Requirement traceability

| BRS requirement | Schema support |
|---|---|
| BRS-ORG-001–005 | Branches; status and no-delete policy |
| BRS-ORG-006–010 | Employees and identity integration; status/suspension and session revocation |
| BRS-ORG-011–014 | EmployeeBranchAssignments; effective-dated history |
| BRS-ORG-015–020 | Roles, Permissions, RolePermissions, EmployeePermissionGrants |
| BRS-ORG-021–026 | EmployeeSessions, RegisteredDevices and session branch context; audit events |
| BRS-ORG-027–029 | Audit module records administrative changes; actor/time/target and before/after |

## 6. Decisions deliberately not invented

1. Exact employee fields beyond the approved profile/administration requirements.
2. Employee number generation and branch code lifecycle beyond uniqueness and historical stability.
3. Identity provider, password policy, MFA and token/session persistence.
4. Offline credential-verifier and secure local authentication material.
5. Device certificate/credential technology and provisioning.
6. Full permission catalogue and maker-checker matrix.
7. Exact branch snapshot payload size, publication cadence and retention.

## 7. Approval boundary and next step

TD-15 specifies the first table group only. It does not create database tables or authorize migrations. After owner review, the next detailed group is Product Catalog, followed by Procurement, Inventory, Pricing, POS/Returns, Payments, Online/CRM/Fulfillment, Operations and Reporting.

**No SQL table, EF Core entity, DDL or migration is created by this document.**

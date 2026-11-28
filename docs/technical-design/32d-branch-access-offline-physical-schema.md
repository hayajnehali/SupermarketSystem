# TD-32D — Branch Access, Offline Sessions and Policy Snapshot Physical Schema

**Version:** 0.1  
**Status:** Delegated design approval — approved for physical mapping; credential verifier security design remains blocked.  
**Date:** 2026-11-28  
**Depends on:** TD-15, TD-26, TD-29, TD-30, TD-31 and TD-32.

## 1. Authority boundary

- Central remains authoritative for Employees, Roles, Permissions, RegisteredDevices, EmployeeSessions and RuntimePolicies.
- A Branch stores only its own approved access/device/policy snapshots and local session state.
- No Branch database contains Central password hashes, Identity security stamps, reusable Central access/refresh tokens or a full employee directory.
- The local authorization snapshot is valid for no more than 24 hours from the last successful Central verification. A local process restart or clock reset cannot extend it.
- Idle timeout is 30 minutes. The local session expires at the earlier of its approved session expiry and the 24-hour Offline authorization boundary.
- When connectivity returns, normal sensitive operation is not restored merely because the API is reachable. The Branch must durably synchronize, refresh the access/device/policy snapshots, reconcile pending work, and receive successful Central verification/authorization.

## 2. Branch access snapshot

### 2.1 `org.BranchAccessSnapshots`

Use the TD-15 columns and add `LastCentralVerifiedAtUtc` as the explicit start of the 24-hour Offline validity calculation.

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchAccessSnapshotId | uniqueidentifier | No | PK; sequential Guid |
| SourceVersion | bigint | No | Monotonic Central publication version |
| GeneratedAtUtc | datetime2(3) | No | Central generation time |
| LastCentralVerifiedAtUtc | datetime2(3) | No | UTC; last successful Central verification |
| AppliedAtUtc | datetime2(3) | No | Local durable apply time |
| ValidUntilUtc | datetime2(3) | No | Must not exceed LastCentralVerifiedAtUtc + 24 hours |
| PayloadHash | binary(32) | No | SHA-256 of canonical snapshot |
| IsActive | bit | No | Exactly one active snapshot |
| RowVersion | rowversion | No | Concurrency token |

- Unique SourceVersion.
- Filtered unique index where IsActive=1.
- A snapshot is activated only after complete payload/hash validation and atomic application of its child rows.
- Preserve previous snapshot metadata for audit/recovery; only one snapshot is active.
- Branch cannot extend ValidUntilUtc or change LastCentralVerifiedAtUtc.

### 2.2 `org.BranchAccessSnapshotEmployees`

Retain TD-15 columns: BranchAccessSnapshotEmployeeId, BranchAccessSnapshotId, EmployeeId (Central GUID, no cross-database FK), EmployeeNumber, DisplayName, EmploymentStatus and SnapshotEffectiveAtUtc.

- Unique (BranchAccessSnapshotId, EmployeeId).
- Local FK only to BranchAccessSnapshots.
- Contains only employees authorized for this Branch; no credentials or sensitive HR fields.

### 2.3 `org.BranchAccessSnapshotPermissions`

Retain TD-15 columns: BranchAccessSnapshotPermissionId, BranchAccessSnapshotEmployeeId, BranchId, PermissionCode, Effect, EffectiveFromUtc and EffectiveToUtc.

- Unique (BranchAccessSnapshotEmployeeId, PermissionCode).
- Local FK only to BranchAccessSnapshotEmployees.
- PermissionCode is a stable Central permission identifier; Branch cannot create or edit permissions.
- Effective permission evaluation uses the snapshot's effective role/permission result and current local branch/device/session context.

### 2.4 `org.BranchAccessSnapshotDevices` — new Branch-local projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchAccessSnapshotDeviceId | uniqueidentifier | No | PK; sequential Guid |
| BranchAccessSnapshotId | uniqueidentifier | No | FK → BranchAccessSnapshots |
| DeviceId | uniqueidentifier | No | Central device GUID; no cross-database FK |
| DeviceCode | nvarchar(50) | No | Central approved device code |
| DeviceType | tinyint | No | Approved device type |
| DeviceStatus | tinyint | No | Last approved Central status |
| CredentialThumbprint | nvarchar(128) | Yes | Public certificate/key fingerprint only |
| SnapshotEffectiveAtUtc | datetime2(3) | No | UTC |

- Unique (BranchAccessSnapshotId, DeviceId).
- Branch cannot register, activate or revoke devices locally.
- No private key, shared secret or plaintext credential is stored in this table.

## 3. Branch-local employee session state

### 3.1 `org.BranchEmployeeSessions` — new Branch-local table

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| BranchEmployeeSessionId | uniqueidentifier | No | PK; sequential Guid |
| EmployeeId | uniqueidentifier | No | Central employee GUID; no cross-database FK |
| DeviceId | uniqueidentifier | No | Approved local snapshot device GUID |
| BranchId | uniqueidentifier | No | Validated hosting Branch identity |
| BranchAccessSnapshotId | uniqueidentifier | No | FK → local active snapshot |
| IssuedAtUtc | datetime2(3) | No | UTC |
| LastActivityAtUtc | datetime2(3) | No | UTC |
| LastCentralVerifiedAtUtc | datetime2(3) | No | UTC; must be within 24-hour policy |
| ExpiresAtUtc | datetime2(3) | No | UTC; cannot exceed Offline boundary |
| RevokedAtUtc | datetime2(3) | Yes | UTC |
| RevocationReason | nvarchar(300) | Yes | Safe reason code/details |
| RowVersion | rowversion | No | Concurrency token |

**Rules**
- A session is valid only if the Employee is active in the current approved snapshot, the Device is active in the same snapshot, BranchId matches the hosting Branch, the snapshot is active/unexpired, the session is not revoked/expired, and idle time is under 30 minutes.
- Only the authentication/session application service can create, refresh or revoke local session rows.
- Session refresh does not extend the 24-hour Offline authorization limit.
- A new snapshot that disables an employee/device or removes the required permission invalidates affected local sessions immediately when applied.
- No bearer token or refresh-token secret is persisted here. Session credentials are handled by the approved authentication mechanism and secure platform storage.

## 4. Branch runtime policy projection

Retain TD-15 `org.BranchRuntimePolicies`: BranchRuntimePolicyId, PolicyCode, PolicyVersion, PolicyValue, EffectiveFromUtc, ReceivedAtUtc, PayloadHash, IsActive and RowVersion.

- Unique (PolicyCode, PolicyVersion).
- Filtered unique index permits one active version per PolicyCode.
- PolicyValue must pass versioned schema validation and payload hash verification before activation.
- Branch cannot create, edit, extend or publish Central policies.
- An expired policy is not locally extended. Where a policy is mandatory for operation, expiry blocks the affected capability.

## 5. Security implementation gate — not approved for code yet

The cryptographic Offline credential verifier is deliberately not specified by this schema. Before implementing Offline password verification, complete a threat model and security review covering:
- verifier derivation and resistance to offline brute-force;
- unique per-user salt and key separation;
- encryption/key protection using OS/platform protected storage or an HSM-backed mechanism where available;
- key versioning, rotation, backup/restore and compromise response;
- employee disablement/revocation propagation and maximum stale window;
- secure memory/logging and device binding;
- behavior after device replacement, database restore or clock rollback.

Do not copy Identity PasswordHash, SecurityStamp, password reset tokens or central refresh tokens into Branch. If the security review selects a protected verifier artifact outside SQL, keep it outside these relational tables and store only its opaque reference/version in the Branch session subsystem.

## 6. Required constraints and tests

- One active access snapshot per Branch database.
- One active device snapshot record per DeviceId in the active snapshot.
- Session references only the active snapshot and approved device.
- Validate 24-hour Offline ceiling and 30-minute idle timeout using injected UTC clock.
- Test snapshot expiry, clock rollback, process restart, revoked employee/device, removed permission, branch mismatch, stale snapshot, reconnect/reconciliation and duplicate snapshot application.
- Every snapshot publication and session invalidation is audited and synchronized idempotently.

## 7. Approval record

The owner delegated detailed design decisions on 2026-11-28. The Branch snapshot/session schema above is approved for physical mapping. Offline credential-verifier cryptography and key custody remain a mandatory security gate and are not approved for implementation by this document.

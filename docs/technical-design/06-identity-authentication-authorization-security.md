# Technical Design — Identity, Authentication, Authorization, and Security

**Document:** TD-06  
**Version:** 0.2  
**Status:** Approved by project owner — baseline accepted for detailed design  
**Depends on:** Merged BRS, approved owner decisions, TD-01 through TD-05.

## 1. Security objectives

The security design must:

1. Ensure every employee, customer, POS device, service, and integration is authenticated according to its role.
2. Enforce authorization in backend APIs and application services; frontend visibility is never a security boundary.
3. Enforce branch isolation and least privilege.
4. Preserve the approved local login validity of 24 hours after the last successful central verification.
5. Enforce the centrally configured Offline duration uniformly across branches.
6. Protect synchronization messages and prevent one branch from impersonating another.
7. Protect customer, payment, employee, financial, and audit information.
8. Make sensitive actions attributable, reviewable, and auditable.
9. Fail securely when credentials, authorization state, or required security services are invalid or expired.

## 2. Identity domains

Keep the following identity categories logically distinct:

| Identity | Purpose | Authority |
|---|---|---|
| Central employee identity | Central administration and cross-branch authorized work | Central |
| Branch employee identity | Branch POS and branch operational work | Central identity and permission governance; locally validated within approved Offline limits |
| Customer identity | Online store account, customer profile, loyalty and complaints | Central |
| POS device identity | Identifies a registered terminal and its assigned branch | Central registration; branch validates approved device snapshot |
| Branch service identity | Authenticates Branch Worker/API when communicating with Central | Centrally registered service identity, scoped to one branch |
| Central service identity | Authenticates central background services and approved internal processes | Central |

The logical separation above does not require separate Employee and Customer credential tables. As approved by TD-29, Employee and Customer human accounts use one Central ASP.NET Core Identity store (`auth.Users`) with a required, mutually exclusive `AccountType` value. Their business profiles remain separate in `org.Employees` and `crm.Customers`, and their endpoint authorization, token audience/scheme and resource access remain separated. Device and service identities are separate identity categories and are not Employee/Customer user profiles.

Do not reuse a cashier's human credentials as a service-to-service credential. Do not allow a device identity to act as an employee identity.

## 3. Employee authentication and Offline login

### 3.1 Central verification

- Central is the authority for employee account status, roles, permissions, branch assignment, and credential lifecycle.
- Branch login validates the employee against the latest approved local authentication/authorization material received from Central.
- A successful Central verification timestamp is recorded for the employee/branch security context.
- Local login remains valid for no more than 24 hours after the last successful Central verification, as approved in the BRS.
- The 24-hour login validity is a separate limit from the centrally configured maximum Offline operating duration. Both controls must be enforced; the earlier expiry/blocking condition wins.
- The system must not extend either validity window merely because the user repeatedly logs in while disconnected.
- When the local authentication validity expires, the branch must require successful Central verification before the affected employee can authenticate again.

### 3.2 Credential handling

- Passwords must be stored only as salted, adaptive password hashes using a current, well-reviewed password hashing implementation.
- Plaintext passwords must never be stored in branch databases, logs, browser storage, synchronization messages, or support diagnostics.
- If Offline authentication requires local verification material, store only a purpose-built, protected verifier or equivalent approved mechanism; do not copy reusable plaintext credentials or long-lived central secrets to branches.
- Credential reset, account disablement, and permission changes are centrally governed and distributed to branches.
- Password reset and account recovery must use a centrally verified process; Offline mode must not invent or locally approve a password reset.
- Apply rate limits, progressive delays, lockout/risk controls, and security logging to repeated failed login attempts. Exact thresholds are configuration decisions for security testing and operations.

The exact Offline credential-verifier design and cryptographic storage mechanism must be validated during implementation threat modelling before coding.

## 4. Customer authentication

- Customer identity and account lifecycle are central.
- Customer registration must preserve the approved phone ownership verification via OTP.
- OTPs must be short-lived, single-use, securely generated, rate-limited, and stored only in a form suitable for verification.
- Do not log OTP values or expose them in API responses, analytics, or support logs.
- Customer account recovery and phone-number changes require verified ownership checks.
- Enforce the approved unique phone-number rule at the authoritative Central database level.
- Customer authentication must not grant access to employee or administration endpoints.
- Branch staff must not gain general customer profile search access through POS or fulfillment APIs.

SMS/email/OTP delivery provider selection and exact OTP expiry/rate limits are separate integration/security configuration decisions. This document does not change the approved OTP business requirement.

## 5. Authorization model

Use role-based access control (RBAC) combined with contextual authorization checks.

Every protected operation must evaluate, as applicable:

- Authenticated actor and identity type.
- Assigned role and explicit permission.
- Active branch scope.
- POS device registration and branch assignment.
- Resource ownership and workflow state.
- Required approval authority.
- Current Offline policy and local authorization snapshot validity.
- Whether the operation is permitted in the current connectivity/security state.

### 5.1 Role baseline

The approved business roles remain the source for the permission catalogue:

- Cashier.
- Inventory Clerk.
- Pricing Clerk.
- Branch Manager.
- Central Admin.

Permissions must be defined at action/resource level, not inferred solely from a role label. Central Admin access must still be permission-scoped and audited. High-impact actions may require a separate approval permission or maker-checker control where specified by the BRS.

### 5.2 Branch isolation

- Every branch-scoped request must derive BranchId from trusted authenticated context, not from an arbitrary client-supplied value.
- A user assigned to Branch A cannot read or modify Branch B resources unless a specific centrally granted cross-branch permission allows it.
- Query handlers, command handlers, background jobs, and exports must all enforce branch scope.
- API endpoints must not trust a BranchId supplied in a request body as proof of access.
- Branch database records and local caches must be scoped to the hosting branch.
- Central cross-branch reporting is available only to explicitly authorized central roles.

### 5.3 Sensitive actions

The backend must enforce permissions and workflow rules for, at minimum:

- Price and promotion publication.
- Stock adjustment, transfer approval, and stocktake approval.
- Refunds, returns, and payment reconciliation.
- Expense approval and payment authorization.
- Maintenance approval/closure where authorized.
- Employee, role, permission, and device administration.
- Conflict resolution and explicit branch sales resumption.
- Audit access and sensitive data exports.

The exact permission catalogue and approval matrix are separate deliverables derived from the approved BRS. This document does not grant new roles or override approved separation-of-duty rules.

## 6. POS device security

- Each POS terminal must be registered centrally and assigned to exactly one branch at a time.
- The Branch API validates the device identity against the latest approved local device registry snapshot.
- Device credentials must be unique per terminal and revocable.
- A device cannot change its BranchId by editing client configuration or request payloads.
- Device registration, activation, suspension, replacement, and credential rotation are audited.
- POS requests carry device identity and cashier/shift context where required by the business operation.
- A cashier cannot bypass the active shift/drawer controls by changing device identifiers.
- Lost, stolen, compromised, or decommissioned devices must be revocable; the revocation is distributed to branches and enforced when received.
- Device secrets must not be embedded as reusable plaintext in Angular bundles or source control.

The precise device credential technology and provisioning ceremony remain open for the security implementation design.

## 7. Central–Branch service authentication

- Every Branch Worker and branch synchronization client authenticates as a registered branch service identity.
- Central validates the service identity, registered BranchId, allowed message types, and target scope for every synchronization request.
- A branch service identity may submit only events for its own branch and receive only data intended for that branch.
- Central services authenticate to approved internal endpoints using distinct service identities and least-privilege scopes.
- Human access tokens must not be accepted as substitutes for machine service credentials.
- Service credentials must be rotated and revocable without changing business OperationIds or losing durable pending messages.
- Authentication failure, unknown branch identity, revoked identity, or branch-scope mismatch is rejected and audited; it must not trigger a permissive fallback.
- The transport must use TLS. Mutual TLS or an equivalent strong workload-identity mechanism is the preferred candidate for branch-to-central service authentication, subject to deployment feasibility and key lifecycle review.

## 8. API and session security

- All external and cross-site API traffic uses HTTPS with modern TLS configuration.
- Validate issuer, audience, signature, expiry, not-before, and token purpose for bearer tokens.
- Use short-lived access tokens and a controlled refresh/re-authentication strategy; exact lifetimes are set in the authentication design and must not weaken the approved Offline limits.
- Tokens must not be placed in URLs, query strings, logs, or analytics.
- Browser token storage and cookie strategy must be selected with CSRF/XSS threat analysis before implementation.
- Apply CORS allowlists for known application origins; CORS is not an authorization mechanism.
- Validate input server-side, use parameterized database access, and encode output appropriately.
- Apply request size limits, rate limits, anti-automation controls where relevant, and safe error responses.
- Protect state-changing browser requests against CSRF when cookie-based authentication is used.
- Do not expose stack traces, secrets, database details, or internal identifiers unnecessarily in production errors.
- Use API versioning and validate DTOs; reject unknown or invalid security-sensitive fields rather than trusting client claims.

## 9. Data protection and secrets

- Classify data at minimum as public, internal, confidential, or highly restricted.
- Treat credentials, tokens, OTPs, payment-provider secrets, customer personal information, employee data, and financial records as confidential or highly restricted according to the data classification policy.
- Encrypt network traffic in transit.
- Protect database files, backups, exported files, and local branch disks using appropriate encryption at rest for the deployment environment.
- Store secrets in a managed secret store or protected operating-system secret facility; never commit secrets to Git or place them in Angular configuration bundles.
- Separate development, test, and production credentials.
- Restrict production database access to approved service identities and administrators.
- Mask or redact sensitive values in application logs, monitoring, error reports, and support exports.
- Do not store full payment card data or security codes; use the payment provider's approved tokenized/hosted flow.
- Apply data minimization to branch customer/order projections and limit access to assigned workflow data.

Exact encryption algorithms, key management service, backup encryption, and retention periods are to be finalized in deployment/security design and verified against applicable legal obligations.

## 10. Audit and security monitoring

Record security-relevant events, including:

- Successful and failed employee/customer authentication.
- Account lock, disablement, recovery, and credential changes.
- Role, permission, branch assignment, and device registration changes.
- Access denials and branch-scope violations.
- High-impact approvals, refunds, adjustments, and overrides.
- Central/branch service authentication failures and credential rotation.
- Synchronization authorization failures and rejected messages.
- Conflict decisions and explicit branch resume authorization.
- Sensitive data exports and audit access.

Audit records should include actor/service identity, role/permission context, BranchId, DeviceId where relevant, timestamp, action, target reference, result, reason/correlation/OperationId where applicable, and before/after values for approved auditable changes.

Audit records must be protected against ordinary application-user modification or deletion. Access to audit data is itself audited. Central and branch audit events synchronize through the approved durable synchronization mechanism.

## 11. Threats and required controls

| Threat | Required control |
|---|---|
| Stolen employee credentials | Adaptive password hashing, MFA/risk controls where approved, rate limiting, revocation, audit |
| Expired Offline access used to continue selling | Enforce both 24-hour local login validity and centrally configured Offline duration |
| Branch impersonates another branch | Registered service identity, branch-bound credentials, server-derived BranchId, authorization |
| POS device cloned or moved | Unique device identity, branch assignment validation, revocation and audit |
| Privilege escalation through API payload | Server-side permission checks and trusted context derivation |
| Duplicate/replayed synchronization | OperationId/MessageId idempotency, durable Inbox, authenticated sender |
| Customer data exposed to branch staff | Minimized assigned-order projection, scoped permissions, no general customer search |
| Secrets leaked through repository/logs | Secret store, scanning, redaction, rotation |
| Malicious or compromised local terminal | Device registration, least privilege, endpoint hardening, local network controls, audit |
| Unauthorized branch resumption | Explicit central authorization with actor, branch, timestamp, and audit record |

## 12. Security acceptance criteria

The implementation must demonstrate that:

1. A cashier cannot invoke a manager-only or central-only action by calling an API directly.
2. A user assigned to one branch cannot access another branch's records by changing request parameters.
3. A POS device registered to one branch cannot submit operations as another branch.
4. A branch service identity cannot submit data for another branch or retrieve another branch's publication.
5. Local login is rejected after 24 hours from last successful Central verification.
6. Sales are blocked when the centrally configured Offline duration expires, even if local login validity has not yet expired.
7. Connectivity restoration does not bypass the explicit synchronization/resumption gate.
8. Revoked employee/device/service credentials are rejected when revocation state is available and enforced according to the approved Offline policy.
9. Customer OTPs, passwords, access tokens, and provider secrets do not appear in logs or API responses.
10. Customer accounts cannot call employee/administration endpoints.
11. High-impact security and business actions are attributable in audit records.
12. Production secrets are absent from source control and Angular client bundles.
13. Authentication and authorization failures are handled without permissive fallback.
14. Security tests cover direct API calls, branch isolation, token validation, replay, expiry, and privilege escalation.

## 13. Decisions deferred to detailed security/API design

1. Employee authentication protocol and identity-provider implementation.
2. Customer authentication/session mechanism.
3. MFA policy for central and privileged employees.
4. Offline credential-verifier design and secure local credential storage.
5. Token format, access/refresh lifetime, revocation, and signing-key rotation.
6. POS device provisioning and credential rotation mechanism.
7. Branch service workload identity choice (mTLS, certificates, or equivalent).
8. Exact permission catalogue, role mapping, and maker-checker matrix.
9. Secret manager, encryption/key-management products, and certificate authority.
10. Rate-limit thresholds, lockout rules, session timeout, and security alert thresholds.
11. Data classification, privacy retention, and incident response procedures.
12. Security testing plan, threat model, and penetration test scope.

## 14. Approval boundary

TD-06 defines security requirements and candidate controls. It does not select a final identity provider, token protocol, certificate product, MFA vendor, secret manager, or detailed permission catalogue. These choices require a focused compatibility and threat review before implementation.

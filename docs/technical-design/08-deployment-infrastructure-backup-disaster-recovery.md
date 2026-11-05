# Technical Design — Deployment, Infrastructure, Backup, and Disaster Recovery

**Document:** TD-08  
**Version:** 0.1  
**Status:** Initial infrastructure and operations design proposal — pending owner review  
**Depends on:** Approved BRS, TD-01 through TD-07.

## 1. Objectives and constraints

This document defines the deployment and operational model for Central and Branch environments.

The design must preserve the approved project constraints:

- Central and each branch have their own application/database boundary.
- Branch POS uses the local Branch API and local branch database; no direct database access from Angular.
- A branch cannot continue sales beyond the Offline duration configured by Central.
- Local employee login validity remains no more than 24 hours after last successful Central verification.
- Connectivity restoration alone does not authorize sales resumption; synchronization, reconciliation and explicit Central authorization are required.
- Central and branch synchronization follows TD-05 Outbox/Inbox and idempotency rules.
- Backup and recovery targets remain RPO 15 minutes for Central and RTO 4 hours for Central, as approved in the project baseline.
- Branch deployment and recovery must not introduce cross-branch database dependencies or weaken branch data ownership.

This is a logical deployment design. Hosting vendor, server sizing, network circuits, and final licensing require a separate sizing and procurement assessment.

## 2. Environment separation

Maintain separate environments:

| Environment | Purpose | Data rule |
|---|---|---|
| Development | Developer work and local integration | Synthetic/test data only |
| Test/QA | Functional, integration and regression testing | Synthetic or masked data |
| Staging/UAT | Production-like release verification and owner acceptance | Masked data; controlled access |
| Production | Live Central and Branch operations | Real operational data; restricted access |

- Production secrets, certificates, payment credentials and customer data must never be copied into development.
- Environment configuration is externalized; do not hardcode endpoints, credentials or keys into source code.
- Deployments are versioned and repeatable.
- Production changes require an approved release, rollback plan and recorded deployment identity.
- Schema migrations must be backward-compatible with the supported rollout sequence where possible.

## 3. Central deployment topology

Central production contains the following logical components:

- Central.Api — externally reachable application API through a secured reverse proxy/gateway.
- Central.Worker — background processing, publication, reconciliation and provider integration.
- Central SQL Server database — authoritative central data.
- Durable backup storage — protected, separate from the primary database host/failure domain.
- Monitoring/logging/alerting — centralized operational visibility with sensitive data redacted.
- Managed secret/certificate storage — restricted access and rotation.

Recommended logical layout:

1. Public traffic terminates at a hardened HTTPS reverse proxy or managed gateway.
2. Central.Api runs in a private application network behind that entry point.
3. Central.Worker runs as a separately managed background process using a least-privilege service identity.
4. SQL Server is on a private network and accepts connections only from approved application/worker identities and administrative paths.
5. Backups are encrypted and copied to a separate failure domain with restricted deletion permissions.
6. Administrative access uses a controlled management channel, MFA where available, and audit logging.

The first production deployment may use a modest single-region topology sized for the initial 3–5 branches, but database, backup and application roles must remain logically separated so capacity or availability can be improved without changing business boundaries.

## 4. Branch deployment topology

Each branch has a local environment:

- Branch.Api — local application API used by POS terminals.
- Branch.Worker — initiates synchronization with Central.
- Branch SQL Server database — authoritative branch operational database.
- Local network and managed switch/router — connects POS terminals to Branch.Api.
- Backup storage/target — protected local or remote backup destination.
- Health/diagnostic agent or endpoint — reports application, database, disk and synchronization health.

Rules:

- POS terminals communicate with Branch.Api over the branch LAN; they never connect directly to SQL Server.
- Branch.Api and Branch.Worker use separate least-privilege database/service identities where practical.
- SQL Server listens only on the required private branch network interfaces and firewall rules.
- The branch database is not exposed to the public internet.
- Branch.Worker initiates outbound secure synchronization to Central; no inbound public connection to the branch database is required.
- A branch can operate locally during an internet outage only within the centrally configured Offline duration and other approved login/security constraints.
- Loss of the local Branch.Api or local database is not treated as an internet-only outage; POS cannot sell without its required local server/database.
- Each branch must have a documented recovery procedure before production go-live.

## 5. Network and connectivity

### 5.1 Central connectivity

- All public API and provider callback traffic uses HTTPS.
- SQL Server ports are never publicly exposed.
- Administrative access is restricted through VPN, private access, or a hardened management gateway.
- Firewall rules are allowlisted by source, destination, protocol and purpose.
- Branch synchronization is outbound-initiated and authenticated using TD-06 service identity controls.
- Payment/OTP providers reach only the required Central callback/adapter endpoints.

### 5.2 Branch connectivity

- POS-to-Branch.Api traffic remains on the branch LAN.
- Branch devices and servers use fixed/reserved network addressing or managed service discovery.
- Separate staff/guest Wi-Fi from POS/server network segments.
- Restrict POS device communication to required local services.
- Protect the branch server physically and restrict local administrator access.
- Provide reliable internet connectivity appropriate to branch order/payment workflows; the exact ISP and backup-link choice is a procurement decision.
- Network restoration must trigger synchronization health checks, not automatic POS unlock.

### 5.3 Time synchronization

- Central and branch servers use trusted time synchronization.
- Store operational timestamps in UTC and display local time using the configured business timezone.
- Monitor clock drift because audit ordering, token validity, payment callbacks and synchronization checkpoints depend on reliable time.
- Device timestamps are not trusted as the sole source for security decisions.

## 6. Database deployment and operations

- Central database and each Branch database are separate SQL Server databases with independent backup and recovery.
- Use supported SQL Server editions and versions selected during procurement and compatibility testing.
- Branch database edition/capacity must be sized for the branch workload; do not assume all branches need the same edition without measurement.
- Database access is private and limited to application/worker identities and authorized administrators.
- Apply schema migrations through a controlled release process.
- Test migrations against representative data volumes and provide a rollback/forward-fix plan.
- Monitor database size, transaction log growth, disk space, query latency, blocking, deadlocks, failed jobs and backup completion.
- Establish maintenance windows and tested index/statistics maintenance appropriate to measured workload.
- Do not use database replication as a replacement for the business synchronization model in TD-05.

## 7. Backup policy

### 7.1 Central

- Preserve the approved Central RPO target of 15 minutes.
- Use a SQL Server recovery model and backup strategy capable of meeting the target. Under the full recovery model, schedule transaction-log backups frequently enough to keep the recoverable data-loss window within 15 minutes; full/differential backups alone do not establish this RPO.
- Monitor the age of the latest successfully completed transaction-log backup and alert before the 15-minute RPO can be breached.
- Confirm that backup storage throughput, transaction-log chain continuity, encryption, and restore procedures can meet the target under the selected Central workload.
- Back up application configuration and deployment metadata needed to rebuild the service, excluding recoverable secrets where a secret manager is authoritative.
- Encrypt backups in transit and at rest.
- Store backup copies outside the primary database host and preferably in a separate failure domain/account.
- Restrict backup deletion and use immutable or protected retention where supported.
- Monitor backup age, success, size anomalies and restore-test results.
- Document retention periods after legal, business and storage requirements are confirmed.

### 7.2 Branches

- Each branch database requires a scheduled backup policy independent of Central availability.
- Branch backup frequency and retention must be selected through branch workload and recovery analysis; do not assume the Central RPO automatically applies to branches.
- Protect local backups against server theft, disk failure, ransomware and unauthorized deletion.
- Where remote backup is used, transfer encrypted backups to a Central-controlled or approved protected destination when connectivity permits.
- A local backup must not be considered safe solely because it is on another partition of the same physical server.
- Monitor last successful branch backup and alert the responsible branch/central operator.

### 7.3 Backup security

- Backup encryption keys are managed separately from backup files.
- Access is least-privilege and audited.
- Backup files must not be emailed or copied to unmanaged USB devices.
- Perform periodic restore tests; a successful backup job alone does not prove recoverability.

## 8. Recovery objectives and disaster recovery

### 8.1 Central recovery

The approved targets are:

- RPO: 15 minutes.
- RTO: 4 hours.

The Central recovery plan must cover:

1. Database restore to a consistent point within the RPO target.
2. Rebuild or failover of Central.Api and Central.Worker.
3. Restore of required configuration and service identities.
4. Verification of payment-provider callback handling and pending payment reconciliation.
5. Verification of central Outbox/publication and synchronization checkpoints.
6. Controlled reconnection of branches.
7. Explicit review of branch sync status and any messages replayed after recovery.
8. Audit record of recovery start, restore point, validation and service resumption.

RPO/RTO are targets to be proven through timed exercises, not assumed from infrastructure specifications.

### 8.2 Branch recovery

- Branch recovery objectives must be defined before production rollout based on business impact and hardware replacement capability.
- Restore the branch database and required local configuration from verified backups.
- Reconcile local Outbox/Inbox and OperationId state before normal sales resume.
- If a backup restore could omit locally completed transactions, recover those transactions from durable local logs/Outbox or other approved evidence before resumption.
- Re-register or rotate compromised device/service credentials when required.
- Reconcile branch stock, invoices, tenders, reservations and central snapshots.
- Require explicit Central authorization before resuming sales if the outage/recovery path has triggered the TD-05 resumption gate.
- Do not create a new branch database and silently start selling with incomplete history.

### 8.3 Recovery exercises

- Conduct scheduled Central restore tests and branch recovery simulations.
- Record measured RPO/RTO, data loss, failed steps, corrective actions and owner.
- Test recovery from database corruption, server loss, storage loss, credential compromise and ransomware scenarios.
- Test recovery with pending synchronization messages and duplicate delivery to confirm idempotency.
- Retain evidence of successful restore tests for audit and release readiness.

## 9. Deployment and release process

Use a controlled pipeline:

1. Build and static validation.
2. Automated unit and integration tests.
3. Dependency and secret scanning.
4. Database migration validation.
5. Deploy to test/QA.
6. Run end-to-end and synchronization contract tests.
7. Promote to staging/UAT for owner/business acceptance.
8. Back up or verify recovery point before production migration.
9. Deploy Central components using a compatible rollout sequence.
10. Deploy branch components in a controlled pilot, then staged branch rollout.
11. Verify health, synchronization, payment callbacks, and branch Offline controls.
12. Record release version, migration version, deployment time, approver and rollback/forward-fix decision.

Branch deployments may be staggered. Central must maintain the supported API/message compatibility window defined by release policy.

A rollback must not discard committed business transactions or reverse an irreversible payment. Database rollback strategy must distinguish application binary rollback from data migration rollback; forward fixes may be safer for destructive schema changes.

## 10. Monitoring, logging and alerting

Monitor at Central and each Branch:

- API/worker process health and restart count.
- SQL connectivity, availability, disk space and database/log growth.
- CPU, memory, disk latency and network connectivity.
- Backup age, success and restore-test status.
- Synchronization Outbox depth/oldest age, last acknowledgement/checkpoint, failed messages and conflicts.
- Centrally configured Offline policy expiry and local login verification age.
- POS device health and active shifts where operationally appropriate.
- Online order reservation age and pending payment reconciliation.
- Payment/OTP/email provider availability and callback processing.
- Certificate/secret expiry and rotation status.

Alert severity and thresholds must be configurable and tuned from production observations. Alerts should identify affected branch/system, time, failure category, and runbook link without exposing secrets or customer payment data.

## 11. Operational runbooks

Before production go-live, create and test runbooks for:

- Central API outage.
- Central database outage and restore.
- Branch API/database outage.
- Branch internet outage and Offline policy expiry.
- Synchronization backlog, rejected message and conflict.
- Payment provider timeout, duplicate callback and refund reconciliation.
- Failed backup and restore.
- Expired/revoked certificate or service credential.
- Lost/compromised POS device.
- Disk capacity warning and database transaction log growth.
- Application release failure and database migration recovery.
- Ransomware/security incident and evidence preservation.

Each runbook identifies detection, impact, immediate containment, recovery steps, required approvals, validation, communication and audit evidence.

## 12. Deployment acceptance criteria

The implementation and production readiness review must demonstrate that:

1. POS can communicate with Branch.Api over the local network without direct SQL access.
2. Branch databases are not publicly reachable.
3. Branch.Worker can synchronize outbound securely without inbound database connectivity.
4. A branch internet outage respects the Central-configured Offline duration.
5. A local Branch.Api/database outage blocks POS sales.
6. Central backup and restore tests meet the 15-minute RPO and 4-hour RTO targets under the tested failure scenario, including point-in-time recovery from an unbroken transaction-log backup chain.
7. Branch backups can be restored and local Outbox/Inbox state remains consistent.
8. A recovery with pending synchronization messages does not duplicate invoices, stock movements or payment effects.
9. Restored branches do not resume sales before required reconciliation and explicit Central authorization.
10. Secrets and backup encryption keys are not stored with application source or unprotected backup files.
11. Monitoring detects failed backups, growing synchronization backlog, expiring Offline policy and service/database outages.
12. Production deployments are versioned, approved, auditable and recoverable.

## 13. Decisions deferred to infrastructure sizing and operations

1. Cloud provider, region, hosting model and commercial plan.
2. Central compute sizing, database sizing, availability tier and scaling thresholds.
3. Branch server hardware, storage redundancy, UPS and physical security specification.
4. SQL Server edition/licensing per environment and branch.
5. ISP bandwidth, static IP/VPN/private connectivity and optional secondary links.
6. Backup schedule details, retention periods, storage account and immutability settings.
7. Branch-specific RPO/RTO targets.
8. Monitoring/logging platform, alert thresholds and on-call ownership.
9. Deployment pipeline platform, artifact registry and release approval workflow.
10. Disaster recovery region/site and failover automation level.
11. Production data retention and applicable legal requirements.
12. Exact capacity/load targets and performance test profile.

## 14. Approval boundary

TD-08 establishes deployment topology, backup/recovery principles and operational readiness requirements. It does not select a cloud vendor, hardware model, ISP, SQL license, monitoring product, exact branch recovery target or authorize production deployment.

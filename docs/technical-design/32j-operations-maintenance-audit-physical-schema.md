# TD-32J — Operations, Maintenance, Alerts, Audit, Reporting and Branch Execution Physical Mapping

**Version:** 0.1  
**Status:** Implementation baseline — aligned to TD-23 and TD-32  
**Date:** 2026-11-28  
**Depends on:** TD-12, TD-23, TD-26, TD-29, TD-30, TD-31 and TD-32.

> This is a physical implementation mapping, not executable DDL or a migration. Payroll remains a tracking placeholder only; this document does not introduce payroll calculation or payment.

## 1. Ownership boundaries

- Central owns approvals, policy, assignment, alert definitions, security/audit review and reporting definitions.
- Branch owns the local request/execution facts for expenses, maintenance and operational actions performed at that Branch.
- Central stores synchronized projections of Branch-originated requests and execution facts for oversight.
- Cross-database relationships use correlation GUIDs, never cross-database foreign keys.
- Audit and event history is append-only. Corrections are compensating entries or explicit review actions, never destructive rewrites.

## 2. Operations and expenses

Tables in the approved inventory:
- Central: `operations.ExpenseCategories`, `operations.ExpenseApprovals`, `operations.ExpenseRequestDocuments`, `operations.CashDeposits`, `operations.CashDepositLines`.
- Branch authority: `operations.ExpenseRequests`, `operations.ExpenseExecutions`; Central keeps synchronized review projections.
- `operations.PayrollTrackingRecords` is a reporting/tracking placeholder only; no salary computation, payslip, attendance-to-payroll calculation or salary payment workflow is authorized.

Physical rules:
- Expense request is created in the originating Branch and carries BranchId, RequestNumber, OperationId, amount/currency, category, description, requester and evidence references.
- Central approval is required before an expense is executed. Approval decision stores approver, decision, reason, approved amount and timestamp; approved amount cannot exceed requested amount without a revised request.
- Execution is recorded in Branch only after a valid Central approval command. A request cannot be executed twice; unique OperationId and approval correlation prevent duplicate cash movement.
- If actual expense differs from approved amount, execution is blocked pending a new Central decision. Post-execution review is recorded separately and does not rewrite the approval or execution.
- Expense documents are stored in managed object storage; SQL stores metadata, checksum, media type, size and protected object reference. Do not store file bytes in ordinary relational rows.
- Cash deposits are recorded as a Central header plus allocation lines to Branch/shift cash evidence. Allocations must belong to one Branch and totals must equal the deposit amount; over-allocation is rejected transactionally.
- Expense approval and execution must not be modeled as a general ledger or accounts-payable ledger.

## 3. Maintenance

Tables:
- Central: `maintenance.MaintainableAssets`, `maintenance.MaintenanceAssignments`, `maintenance.MaintenanceWorkLogs`, `maintenance.MaintenanceDocuments`, `maintenance.MaintenanceRequestEvents`.
- Branch authority: `maintenance.MaintenanceRequests`; Central keeps workflow/review copies.

Physical rules:
- Each asset has a stable Guid and belongs to one Branch; asset code is unique within Branch and immutable after activation.
- A maintenance request originates in one Branch and references a local asset by GUID. Central review/assignment references the Branch request by correlation GUID, not FK.
- Assignment/reassignment requires Central authority and records assignee, scope, priority, reason, effective time and actor.
- Work logs are append-only and record work performed, materials/notes and actor/time; corrections are new entries.
- Maintenance documents use object storage with metadata/checksum and controlled access.
- Closing a request requires an explicit completion outcome and Central closure/approval where required by TD-23. A request cannot be silently deleted or reopened without a new audited event.

## 4. Alerts and notifications

Central tables:
- `alerts.AlertDefinitions`
- `alerts.AlertEvents`
- `alerts.AlertRecipients`
- `alerts.AlertDeliveries`
- `alerts.AlertAcknowledgements`
- `alerts.AlertActions`

Physical rules:
- AlertDefinitions are centrally versioned, enabled/disabled by authorized Central users, and define severity, routing, deduplication window and permitted action types.
- AlertEvents are immutable facts with stable EventKey/CorrelationId; a unique key prevents duplicate ingestion of the same source event.
- Recipients are materialized at alert creation so later role/assignment changes do not rewrite who was notified at the time.
- Delivery attempts are append-only and independent from the underlying alert/business workflow. A failed notification never changes an expense, stock, order, payment or maintenance status.
- Acknowledgement records actor/time and are unique per alert + recipient; repeated acknowledgements are idempotent.
- AlertActions must reference an allowlisted action type and a business correlation ID. They do not execute arbitrary SQL, code, URLs or free-form commands.
- External notification provider secrets are held outside SQL and referenced through secure configuration.

## 5. Audit and security events

Central tables:
- `audit.AuditEntries`
- `audit.AuditChangeDetails`
- `audit.SecurityEvents`
- `audit.SecurityEventReviews`

Physical rules:
- AuditEntries are append-only and include actor, actor type, action, module/entity identifiers, UTC timestamp, correlation/operation IDs, source and outcome.
- AuditChangeDetails store only approved before/after values. Secrets, password hashes, OTP values, access tokens, payment credentials and sensitive provider payloads must be excluded or redacted before persistence.
- SecurityEvents are immutable detection facts; SecurityEventReviews record reviewer, classification, decision, notes and reviewed time without modifying the original event.
- Use indexes on EntityType + EntityId + OccurredAtUtc, ActorUserId + OccurredAtUtc, CorrelationId and SecurityEventType + OccurredAtUtc.
- Application roles and service identities receive insert-only permissions for audit/event tables where practical; ordinary application workflows cannot update/delete historical rows.
- Audit retention and archival duration must be configurable only through an approved retention policy and must preserve statutory/business requirements. No hard-coded destructive purge is introduced by this document.

## 6. Reporting

- `reporting.ReportRunRecords` is optional and may be included only if report execution history is needed.
- Reporting read models are derived projections; they never become an authority for inventory, sales, payment, customer, procurement or accounting facts.
- Every report run stores report key/version, requested filters (validated and non-secret), requester, start/end time, status, row count and correlation ID.
- Long-running report generation must be asynchronous and idempotent; generated exports live in managed object storage with expiry/access metadata.
- No reporting table may be used to update operational source records.

## 7. Concurrency, idempotency and retention

- Mutable request/approval/assignment headers use rowversion.
- Every cross-boundary request, execution acknowledgement and alert ingestion has a stable OperationId/EventKey and durable Inbox/Outbox handling under TD-12.
- Unique constraints prevent duplicate execution, duplicate alert event ingestion and duplicate acknowledgement.
- No physical deletion of financial, operational, audit or security history through ordinary user workflows.
- Retention jobs must be bounded, observable, resumable and separately authorized. Deletion/archival must never remove rows still referenced by legal, financial, stock or dispute history.

## 8. Migration gate

Before DbContext/migrations:
1. Reconcile exact TD-23 columns and all table names against TD-13 and TD-14.
2. Confirm Branch-owned request/execution table columns and Central projection shape.
3. Confirm object-storage metadata contracts and access controls.
4. Confirm retention periods and archival authority; do not invent a retention duration.
5. Add SQL Server integration tests for approval-before-execution, duplicate execution, concurrent cash-deposit allocation, alert deduplication, append-only audit and retry-safe retention.

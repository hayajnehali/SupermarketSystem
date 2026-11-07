# TD-23 — Detailed Logical Schema: Operations, Maintenance, Alerts, Audit and Reporting

**Version:** 0.1  
**Status:** Proposed for owner review  
**Date:** 2026-11-07  
**Scope:** Branch expense requests and Central approval, central payroll-tracking placeholder, centrally managed branch cash deposits, maintenance requests/assignments/completion, centralized alerts, audit/security history and Central management reports.  
**Depends on:** Approved BRS Unit 12, FIN-01–FIN-07, FIN-08–FIN-10, FIN-03/FIN-04, OPS-01–OPS-04, MNT-01, NTF-01–NTF-03, SEC-01–SEC-03, OPS-RPT-001–004, TD-03, TD-04, TD-06, TD-12, TD-13 and TD-14.

> This is a logical/physical schema proposal, not executable DDL, EF Core code or a migration. It does not add a general ledger, statutory payroll engine, HR attendance/leave system or a separate data warehouse.

## 1. Shared conventions

1. Every PK is sequential application-generated `Guid` / SQL Server `uniqueidentifier`; no integer identity PK.
2. UTC event times use `datetime2(3)`; business/report periods use `date` where appropriate.
3. JOD amounts use `decimal(18,3)`; no floating-point monetary values.
4. Central owns approvals, policies, cash-deposit management, maintenance assignments/closure, alert definitions, security review and management reporting.
5. Branch-originated requests and execution facts are durably stored in the Branch DB and synchronized with stable GUIDs. Central copies do not create cross-database FKs.
6. Audit, security, approval and financial history is append-only for ordinary users; no cascade deletion.
7. Attachments are stored in managed protected file/object storage. SQL contains metadata and protected storage references, not public arbitrary URLs or large binary payloads.
8. Reports are Central-only. Read models are derived projections, not a second source of truth.

## 2. Database placement

| Table | Central DB | Each Branch DB |
|---|---|---|
| ExpenseCategories | Authoritative | Approved reference projection |
| ExpenseRequests, ExpenseRequestDocuments | Central review copy | Branch request origin and supporting documents |
| ExpenseApprovals | Authoritative | No |
| ExpenseExecutions | Central review copy | Branch actual execution evidence |
| PayrollTrackingRecords | Authoritative | No |
| CashDeposits, CashDepositLines | Authoritative | No; branch cash-shift facts are synchronized references |
| MaintainableAssets | Authoritative | Limited asset/location projection |
| MaintenanceRequests | Central workflow copy | Branch request origin |
| MaintenanceAssignments, WorkLogs, Documents, RequestEvents | Authoritative workflow/history | Branch confirmation/evidence origin where applicable |
| AlertDefinitions, AlertEvents, AlertRecipients, Deliveries, Acknowledgements, Actions | Authoritative | Branch-originating event facts may be synchronized to Central |
| AuditEntries, AuditChangeDetails | Central records and synchronized Branch facts | Local operational audit authority |
| SecurityEvents | Central and synchronized Branch facts | Local security-event origin |
| SecurityEventReviews | Authoritative | No |
| ReportRunRecords and reporting read models | Authoritative/derived | No management reporting authority |

## 3. Branch expenses and Central approval

### 3.1 `operations.ExpenseCategories` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ExpenseCategoryId | uniqueidentifier | No | PK; sequential Guid |
| CategoryCode | nvarchar(50) | No | Unique stable code |
| Name | nvarchar(150) | No | Display name |
| Description | nvarchar(500) | Yes | Optional |
| IsActive | bit | No | Deactivation only |
| DisplayOrder | int | No | Non-negative |
| RowVersion | rowversion | No | Concurrency token |

### 3.2 `operations.ExpenseRequests` — Branch origin; Central review copy

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ExpenseRequestId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| SourceBranchId | uniqueidentifier | No | Branch identity; Central FK only in Central copy |
| ExpenseCategoryId | uniqueidentifier | No | Central category GUID |
| EstimatedAmount | decimal(18,3) | No | Positive estimated amount |
| CurrencyCode | char(3) | No | JOD |
| Reason | nvarchar(1000) | No | Required reason |
| RequestedByEmployeeId | uniqueidentifier | No | Branch employee GUID |
| RequestedAtUtc | datetime2(3) | No | UTC |
| RequestStatus | tinyint | No | Submitted / UnderReview / ReturnedForCorrection / Approved / Rejected / Executed / Reviewed |
| ReceivedAtCentralUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 3.3 `operations.ExpenseRequestDocuments` — Branch upload origin; Central reference

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ExpenseRequestDocumentId | uniqueidentifier | No | PK; sequential Guid |
| ExpenseRequestId | uniqueidentifier | No | FK to local request copy |
| DocumentStage | tinyint | No | Quotation / ProForma / FinalInvoice / PaymentProof / OtherApproved |
| StorageKey | nvarchar(500) | No | Protected storage reference |
| OriginalFileName | nvarchar(255) | No | Sanitized original filename |
| ContentType | nvarchar(100) | No | Validated media type |
| UploadedByEmployeeId | uniqueidentifier | No | Actor |
| UploadedAtUtc | datetime2(3) | No | UTC |
| DocumentHash | binary(32) | No | SHA-256 |

### 3.4 `operations.ExpenseApprovals` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ExpenseApprovalId | uniqueidentifier | No | PK; sequential Guid |
| ExpenseRequestId | uniqueidentifier | No | FK → Central ExpenseRequests |
| Decision | tinyint | No | Approve / Reject / ReturnForCorrection |
| DecisionReason | nvarchar(1000) | No | Required reason |
| DecidedByUserId | uniqueidentifier | No | Central actor |
| DecidedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 3.5 `operations.ExpenseExecutions` — Branch execution; Central review copy

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ExpenseExecutionId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| ExpenseRequestId | uniqueidentifier | No | Request GUID |
| ActualAmount | decimal(18,3) | No | Actual executed amount |
| CurrencyCode | char(3) | No | JOD |
| ExecutedByEmployeeId | uniqueidentifier | No | Branch employee |
| ExecutedAtUtc | datetime2(3) | No | UTC |
| ExecutionStatus | tinyint | No | SubmittedForReview / Reviewed / ReturnedForCorrection |
| ReviewedByUserId | uniqueidentifier | Yes | Central reviewer |
| ReviewedAtUtc | datetime2(3) | Yes | UTC |
| ReviewReason | nvarchar(1000) | Yes | Central review reason |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and rules**
- ExpenseCategory → ExpenseRequests is 1:N.
- ExpenseRequest → Documents and Approvals is 1:N.
- An expense cannot be purchased or paid before a Central Approve decision exists.
- After approval and execution, Branch records actual amount and uploads final invoice/payment proof; Central reviews the completed record.
- ExpenseRequests and ExpenseExecutions preserve Branch origin GUIDs when synchronized.
- An expense is not financially approved/completed until Central review is recorded.
- No general ledger or accounts-payable module is implied.

## 4. Central payroll tracking boundary

### 4.1 `operations.PayrollTrackingRecords` — Central only

The BRS confirms Central manages payroll, but the approved business scope does not yet define the detailed payroll fields, calculations, statutory deductions, pay slips, payment batches, attendance, leave, or payroll accounting. Therefore TD-13 retains one Central placeholder only.

Before columns or supporting tables are designed, the project owner must approve the precise payroll-tracking scope. Do not create an assumed payroll engine, salary calculation model, attendance/leave system, or statutory payroll integration. This is a technical-design deferral, not a request to reopen the approved rule that payroll management belongs to Central.

## 5. Central cash deposits from Branches

### 5.1 `operations.CashDeposits` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CashDepositId | uniqueidentifier | No | PK; sequential Guid |
| DepositNumber | nvarchar(50) | No | Unique Central reference |
| SourceBranchId | uniqueidentifier | No | FK → org.Branches |
| DepositDate | date | No | Business date |
| ExpectedAmount | decimal(18,3) | No | Total branch cash collection expected |
| ReceivedAmount | decimal(18,3) | No | Amount physically received/confirmed centrally |
| DifferenceAmount | decimal(18,3) | No | Received minus expected |
| DepositStatus | tinyint | No | Expected / Received / Difference / Banked / Reviewed / Closed |
| BankReference | nvarchar(150) | Yes | Bank deposit reference |
| ReceivedByUserId | uniqueidentifier | Yes | Central receiving actor |
| ReceivedAtUtc | datetime2(3) | Yes | UTC |
| ReviewedByUserId | uniqueidentifier | Yes | Central reviewer |
| ReviewedAtUtc | datetime2(3) | Yes | UTC |
| ReviewNotes | nvarchar(1000) | Yes | Difference/review notes |
| RowVersion | rowversion | No | Concurrency token |

### 5.2 `operations.CashDepositLines` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| CashDepositLineId | uniqueidentifier | No | PK; sequential Guid |
| CashDepositId | uniqueidentifier | No | FK → CashDeposits |
| CashierShiftId | uniqueidentifier | No | Branch shift GUID; no cross-db FK |
| CashDrawerReconciliationId | uniqueidentifier | No | Branch reconciliation GUID; no cross-db FK |
| SourceCashAmount | decimal(18,3) | No | Branch reconciled cash amount |
| IncludedAmount | decimal(18,3) | No | Amount included in this deposit |
| SourceBranchOperationId | uniqueidentifier | No | Branch correlation/idempotency reference |

**Constraints and rules**
- CashDeposit → Lines is 1:N.
- Cash deposit management and reconciliation are Central-owned.
- Shift and reconciliation GUIDs are references to synchronized Branch facts; no cross-database FK.
- The same shift/reconciliation cannot be included twice in active/closed deposit records; enforce uniqueness by source GUID.
- Difference is retained for Central review; Branch cannot mark its own deposit as received or closed by Central.
- This is cash custody/deposit tracking, not a general ledger.

## 6. Maintenance assets and requests

### 6.1 `maintenance.MaintainableAssets` — Central master; Branch reference projection

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| MaintainableAssetId | uniqueidentifier | No | PK; sequential Guid |
| AssetCode | nvarchar(50) | No | Unique stable asset code |
| BranchId | uniqueidentifier | No | FK → org.Branches |
| AssetType | tinyint | No | POSDevice / Printer / Scale / NetworkDevice / Refrigerator / Other |
| Name | nvarchar(150) | No | Asset display name |
| SerialNumber | nvarchar(100) | Yes | Manufacturer serial |
| LocationDescription | nvarchar(300) | No | Branch location, e.g. cashier desk, sales floor, backroom |
| AssetStatus | tinyint | No | Active / OutOfService / Retired |
| RegisteredAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 6.2 `maintenance.MaintenanceRequests` — Branch origin; Central workflow copy

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| MaintenanceRequestId | uniqueidentifier | No | PK; generated at Branch and preserved centrally |
| RequestNumber | nvarchar(50) | No | Unique Central/Branch reference |
| BranchId | uniqueidentifier | No | Branch identity; Central FK only in Central copy |
| MaintainableAssetId | uniqueidentifier | Yes | Asset GUID; required when registered asset exists |
| AssetDescription | nvarchar(200) | No | Device/equipment description |
| LocationDescription | nvarchar(300) | No | Exact location in branch |
| FaultDescription | nvarchar(2000) | No | Fault details |
| Urgency | tinyint | No | Low / Normal / High / Critical |
| RequestStatus | tinyint | No | Submitted / UnderReview / Assigned / InProgress / AwaitingBranchConfirmation / AwaitingCentralReview / Closed / Rejected |
| RequestedByEmployeeId | uniqueidentifier | No | Branch employee |
| RequestedAtUtc | datetime2(3) | No | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 6.3 `maintenance.MaintenanceAssignments` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| MaintenanceAssignmentId | uniqueidentifier | No | PK; sequential Guid |
| MaintenanceRequestId | uniqueidentifier | No | FK → Central MaintenanceRequests |
| TechnicianName | nvarchar(150) | Yes | Assigned technician where individual |
| ServiceProviderName | nvarchar(200) | Yes | Assigned provider where external |
| AssignedByUserId | uniqueidentifier | No | Central actor |
| AssignedAtUtc | datetime2(3) | No | UTC |
| AssignmentStatus | tinyint | No | Assigned / Reassigned / Completed / Cancelled |
| Notes | nvarchar(1000) | Yes | Assignment notes |

### 6.4 `maintenance.MaintenanceWorkLogs` — Central workflow; technician report and Branch confirmation

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| MaintenanceWorkLogId | uniqueidentifier | No | PK; sequential Guid |
| MaintenanceRequestId | uniqueidentifier | No | FK → Central MaintenanceRequests |
| MaintenanceAssignmentId | uniqueidentifier | No | FK → MaintenanceAssignments |
| WorkDescription | nvarchar(2000) | No | Work performed/report |
| ActualCost | decimal(18,3) | No | Actual maintenance cost |
| CurrencyCode | char(3) | No | JOD |
| TechnicianReportedAtUtc | datetime2(3) | No | UTC |
| TechnicianReportReference | nvarchar(500) | Yes | Protected report reference |
| BranchOperationalConfirmation | bit | Yes | Branch confirms device returned to service |
| ConfirmedByEmployeeId | uniqueidentifier | Yes | Branch employee |
| ConfirmedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 6.5 `maintenance.MaintenanceDocuments` and `MaintenanceRequestEvents`

**`MaintenanceDocuments`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| MaintenanceDocumentId | uniqueidentifier | No | PK; sequential Guid |
| MaintenanceRequestId | uniqueidentifier | No | FK → MaintenanceRequests |
| DocumentType | tinyint | No | FaultPhoto / FaultVideo / TechnicianReport / Invoice / Other |
| StorageKey | nvarchar(500) | No | Protected storage reference |
| OriginalFileName | nvarchar(255) | No | Sanitized filename |
| ContentType | nvarchar(100) | No | Validated type |
| UploadedByUserId | uniqueidentifier | No | Actor |
| UploadedAtUtc | datetime2(3) | No | UTC |
| DocumentHash | binary(32) | No | SHA-256 |

**`MaintenanceRequestEvents`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| MaintenanceRequestEventId | uniqueidentifier | No | PK; sequential Guid |
| MaintenanceRequestId | uniqueidentifier | No | FK → MaintenanceRequests |
| EventType | tinyint | No | Submitted / Reviewed / Assigned / Reassigned / WorkReported / BranchConfirmed / CentralClosed / Rejected |
| ActorUserId | uniqueidentifier | No | Actor |
| OccurredAtUtc | datetime2(3) | No | UTC |
| ReasonOrNotes | nvarchar(1000) | Yes | Event notes |

**Constraints and rules**
- MaintainableAsset → MaintenanceRequests is 1:N where an asset record exists.
- MaintenanceRequest → Assignments, WorkLogs, Documents and Events is 1:N.
- Branch submits device/asset, location, fault description, urgency and photo/video evidence.
- Central reviews, assigns technician/provider and follows the request through completion.
- Technician reports work and cost; Branch confirms the device is operational again; Central reviews and closes.
- The complete chronological history is preserved.

## 7. Central alert definitions, events and recipient actions

### 7.1 `alerts.AlertDefinitions` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| AlertDefinitionId | uniqueidentifier | No | PK; sequential Guid |
| AlertCode | nvarchar(80) | No | Unique stable code |
| Name | nvarchar(150) | No | Display name |
| Description | nvarchar(500) | Yes | Description |
| Importance | tinyint | No | Info / Low / Medium / High / Critical |
| DeliveryChannels | nvarchar(200) | No | Allowlisted InApp / Email / MobilePush |
| ThresholdConfiguration | nvarchar(max) | Yes | Centrally managed validated threshold/config payload |
| IsActive | bit | No | Central enable/disable |
| CreatedByUserId | uniqueidentifier | No | Central actor |
| UpdatedAtUtc | datetime2(3) | Yes | UTC |
| RowVersion | rowversion | No | Concurrency token |

### 7.2 `alerts.AlertEvents` — Central authoritative; Branch source events synchronized

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| AlertEventId | uniqueidentifier | No | PK; sequential Guid |
| AlertDefinitionId | uniqueidentifier | No | FK → AlertDefinitions |
| SourceEntityType | nvarchar(80) | No | Stable source module/entity type |
| SourceEntityId | uniqueidentifier | Yes | Source entity GUID |
| BranchId | uniqueidentifier | Yes | FK → org.Branches when branch-scoped |
| ImportanceSnapshot | tinyint | No | Importance at event creation |
| Title | nvarchar(200) | No | Alert title |
| Description | nvarchar(1500) | No | Alert details |
| CreatedAtUtc | datetime2(3) | No | UTC |
| AlertStatus | tinyint | No | New / Assigned / Acknowledged / Resolved / Closed |
| CorrelationId | uniqueidentifier | No | Workflow trace |
| RowVersion | rowversion | No | Concurrency token |

### 7.3 `alerts.AlertRecipients` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| AlertRecipientId | uniqueidentifier | No | PK; sequential Guid |
| AlertEventId | uniqueidentifier | No | FK → AlertEvents |
| EmployeeId | uniqueidentifier | No | Central employee GUID |
| AssignedRoleCode | nvarchar(50) | Yes | Role-based recipient context |
| AssignedAtUtc | datetime2(3) | No | UTC |
| RecipientStatus | tinyint | No | Pending / Delivered / Acknowledged / Resolved |

### 7.4 `alerts.AlertDeliveries` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| AlertDeliveryId | uniqueidentifier | No | PK; sequential Guid |
| AlertRecipientId | uniqueidentifier | No | FK → AlertRecipients |
| Channel | tinyint | No | InApp / Email / MobilePush |
| DeliveryStatus | tinyint | No | Pending / Sent / Failed |
| AttemptCount | int | No | Non-negative |
| LastAttemptAtUtc | datetime2(3) | Yes | UTC |
| DeliveredAtUtc | datetime2(3) | Yes | UTC |
| ProviderReference | nvarchar(150) | Yes | Provider reference |
| FailureReason | nvarchar(500) | Yes | Sanitized reason |

### 7.5 `alerts.AlertAcknowledgements` and `AlertActions` — Central

**`AlertAcknowledgements`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| AlertAcknowledgementId | uniqueidentifier | No | PK; sequential Guid |
| AlertRecipientId | uniqueidentifier | No | Unique FK → AlertRecipients |
| AcknowledgedByUserId | uniqueidentifier | No | Recipient/authorized actor |
| AcknowledgedAtUtc | datetime2(3) | No | UTC |
| AcknowledgementNote | nvarchar(1000) | Yes | Initial note |

**`AlertActions`**

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| AlertActionId | uniqueidentifier | No | PK; sequential Guid |
| AlertAcknowledgementId | uniqueidentifier | No | FK → AlertAcknowledgements |
| ActionType | tinyint | No | Note / FollowUp / Escalated / Resolved |
| ActionDescription | nvarchar(1500) | No | Recorded action/note |
| RecordedByUserId | uniqueidentifier | No | Actor |
| RecordedAtUtc | datetime2(3) | No | UTC |

**Constraints and rules**
- AlertDefinition → Events → Recipients → Deliveries is 1:N.
- Recipient has at most one acknowledgement; acknowledgement may have multiple actions/notes.
- Reading/acknowledging an alert never deletes it or its delivery history.
- Central configures alert types, importance, channels and recipients.
- Internal alerts support InApp, Email and MobilePush as approved; this is separate from customer order notifications (Website/Email only).
- Alert delivery failure does not delete or alter the source business event.

## 8. Audit and security event records

### 8.1 `audit.AuditEntries` — Central and Branch local

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| AuditEntryId | uniqueidentifier | No | PK; sequential Guid |
| ActorType | tinyint | No | Employee / Customer / Service / System |
| ActorId | uniqueidentifier | Yes | Actor GUID; null only for unauthenticated/system event |
| ActionCode | nvarchar(100) | No | Stable action code |
| EntityType | nvarchar(100) | No | Stable target entity type |
| EntityId | uniqueidentifier | Yes | Target entity GUID |
| BranchId | uniqueidentifier | Yes | Branch GUID; local context or Central reference |
| DeviceId | uniqueidentifier | Yes | Device GUID |
| OccurredAtUtc | datetime2(3) | No | UTC |
| CorrelationId | uniqueidentifier | No | Workflow trace |
| Source | tinyint | No | Central / Branch / API / Worker |
| Outcome | tinyint | No | Success / Rejected / Failed |

### 8.2 `audit.AuditChangeDetails` — Same database as AuditEntries

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| AuditChangeDetailId | uniqueidentifier | No | PK; sequential Guid |
| AuditEntryId | uniqueidentifier | No | FK → AuditEntries |
| FieldName | nvarchar(150) | No | Changed field |
| OldValue | nvarchar(max) | Yes | Redacted before value |
| NewValue | nvarchar(max) | Yes | Redacted after value |
| IsSensitiveRedacted | bit | No | Marks protected/redacted fields |

**Constraints and rules**
- AuditEntry → AuditChangeDetails is 1:N.
- Append-only; application roles cannot update/delete protected audit history.
- Passwords, OTPs, access/refresh tokens, provider secrets, card data and other credentials must never be captured in audit before/after values.
- Administrative changes include branch/employee creation/update/suspension, role/permission changes, employee assignments, approvals, pricing/promotion changes and central policy changes.
- Branch operational facts are recorded locally and synchronized to Central; no cross-database FK.

### 8.3 `audit.SecurityEvents` — Central and Branch-originated facts

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SecurityEventId | uniqueidentifier | No | PK; generated at origin and preserved centrally |
| EventType | tinyint | No | FailedLogin / UnusualDevice / UnusualLocation / SuspiciousActivity / DeviceRevoked / Other |
| ActorUserId | uniqueidentifier | Yes | Employee/customer/service identity GUID |
| DeviceId | uniqueidentifier | Yes | Device GUID |
| BranchId | uniqueidentifier | Yes | Branch GUID |
| OccurredAtUtc | datetime2(3) | No | UTC |
| DetectionRuleCode | nvarchar(100) | No | Stable rule code |
| Severity | tinyint | No | Low / Medium / High / Critical |
| Details | nvarchar(1500) | Yes | Sanitized event details; no secrets |
| Source | tinyint | No | Central / Branch |
| ReviewStatus | tinyint | No | New / UnderReview / Reviewed / Closed |
| CorrelationId | uniqueidentifier | No | Trace |
| RowVersion | rowversion | No | Concurrency token |

### 8.4 `audit.SecurityEventReviews` — Central

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| SecurityEventReviewId | uniqueidentifier | No | PK; sequential Guid |
| SecurityEventId | uniqueidentifier | No | Central event GUID |
| ReviewOutcome | tinyint | No | Benign / ConfirmedSuspicious / Escalated / Other |
| ActionTaken | nvarchar(1000) | Yes | Recorded Central action |
| ReviewedByUserId | uniqueidentifier | No | Central reviewer |
| ReviewedAtUtc | datetime2(3) | No | UTC |
| ReviewNotes | nvarchar(1500) | Yes | Notes |

**Constraints and rules**
- SecurityEvent → Reviews is 1:N; reviews are Central-only.
- Suspicious patterns include repeated failed logins and unusual device/location activity.
- Events raise Central alerts according to configured definitions.
- Central reviews and records outcome/action. No automatic account suspension or lockout is implied by the approved decision.
- Preserve source security event; do not delete it after review.

## 9. Central management reporting

### 9.1 `reporting.ReportRunRecords` — Central, optional audit record

| Column | SQL Server type | Null | Rule |
|---|---|---:|---|
| ReportRunRecordId | uniqueidentifier | No | PK; sequential Guid |
| ReportCode | nvarchar(80) | No | Stable report identifier |
| RequestedByUserId | uniqueidentifier | No | Central user |
| BranchFilterId | uniqueidentifier | Yes | Optional Central Branch FK |
| ProductFilterId | uniqueidentifier | Yes | Optional central Product GUID |
| PeriodStart | date | No | Selected report start date |
| PeriodEnd | date | No | Selected report end date |
| FilterPayload | nvarchar(max) | Yes | Validated additional filters |
| RunStatus | tinyint | No | Queued / Running / Completed / Failed |
| RequestedAtUtc | datetime2(3) | No | UTC |
| CompletedAtUtc | datetime2(3) | Yes | UTC |
| OutputReference | nvarchar(500) | Yes | Protected export reference |
| RowVersion | rowversion | No | Concurrency token |

**Constraints and rules**
- ReportRunRecords is optional and records execution/filter history only; it is not a reporting fact table.
- Central Administration alone can access sales, profit and inventory management reports.
- Reports support company-wide and per-branch views, with Central-selected period and filters for branch, period and product.
- Profit reporting uses actual batch cost/issue history from Inventory; do not introduce weighted-average costing.
- Initial reporting uses Central relational read models/projections. No separate data warehouse is assumed.
- Report outputs are protected references; do not store arbitrary large exports in relational rows.

## 10. Requirement traceability

| Approved requirement/decision | Schema support |
|---|---|
| FIN-01, FIN-02, FIN-08–FIN-10 | ExpenseRequests, documents, Central approvals, Branch execution and Central review |
| FIN-03 | Central PayrollTrackingRecords placeholder; exact payroll scope deferred |
| FIN-04 | Central CashDeposits and CashDepositLines |
| FIN-05–FIN-07, OPS-RPT-001–004 | Central ReportRunRecords/read models and Central-only report access |
| OPS-01–OPS-04, MNT-01, OPS-MNT-001–004 | MaintainableAssets, MaintenanceRequests, assignments, work logs, documents and chronological events |
| OPS-ALT-001–004, NTF-01–NTF-03 | Alert definitions/events/recipients/deliveries/acknowledgements/actions |
| SEC-01–SEC-03 | SecurityEvents and Central SecurityEventReviews |
| BRS-ORG-027–029 | AuditEntries and AuditChangeDetails |
| FIN-04 and cashier shift data | CashDepositLines correlated to Branch shift/reconciliation GUIDs |

## 11. Technical risks for review

1. Define the exact limited payroll tracking fields only after the owner-approved FIN-03 scope is mapped; do not infer a statutory payroll system.
2. Define how cash physically moves from Branch to Central and what constitutes Central receipt confirmation; schema records source shifts, expected/received amounts and bank reference but does not invent a cash transport procedure.
3. Define whether one Central cash deposit may include multiple shifts from one branch; current header is branch-scoped and lines are shift-scoped.
4. Define attachment size/type/retention policy and secure file/object storage.
5. Define maintenance asset catalogue lifecycle and whether unregistered equipment can be reported; the request supports asset description when no registered asset exists.
6. Define alert threshold evaluation cadence and delivery retry policy in the worker design; no timer/polling behavior is specified here.
7. Define audit retention, partitioning and archival without permitting operational deletion.
8. Define the report calculation contracts for sales, profit, inventory valuation and branch comparison; preserve source data lineage.
9. Define privacy/redaction rules for AuditChangeDetails, SecurityEvents and maintenance/expense documents.

## 12. Approval boundary and next step

TD-23 specifies Operations, Maintenance, Alerts, Audit and Reporting. It does not create SQL tables, EF Core entities, DDL or migrations. The next step is a cross-document schema consistency review: compare TD-13 inventory against TD-14 relationships and TD-15–TD-23 detailed specifications, reconcile all missing/duplicate entities, then produce the final ERD and implementation-ready schema package.

**No SQL table, EF Core entity, DDL or migration is created by this document.**

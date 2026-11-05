# Business Requirements Specification (BRS)
## Part 2 — Functional Requirements: Units 01–04

**Document version:** 0.2  
**Status:** Approved by the project owner for documentation  
**Parent document:** [01-business-foundation.md](01-business-foundation.md)  
**Scope:** Organizational administration; products; suppliers and purchasing; inventory and stocktaking.

This part defines business-facing functional requirements. It intentionally does not prescribe database tables, APIs, controllers, or other implementation details. Requirement identifiers are stable references for later design, testing, and traceability.

Priority meanings:
- **Must:** required for the approved business scope.
- **Should:** important, but not a release-blocking core requirement unless later confirmed otherwise.

---

# Unit 01 — Central, Branch, Employee, and User Administration

## 1.1 Branch management

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-ORG-001 | The system shall allow central administration to create a branch and record its basic information. | Must |
| BRS-ORG-002 | The system shall allow central administration to update branch information. | Must |
| BRS-ORG-003 | The system shall allow central administration to activate or suspend a branch. | Must |
| BRS-ORG-004 | Suspending a branch shall prevent new operational transactions for that branch while preserving its historical records. | Must |
| BRS-ORG-005 | The system shall prevent permanent deletion of a branch while operational or historical records are associated with it. | Must |

**Use case — Suspend a branch**

1. A central administrator selects an active branch.
2. The administrator requests suspension.
3. The system confirms the action and records the actor and timestamp.
4. The branch becomes suspended and new operational transactions against it are blocked.
5. Historical invoices, inventory movements, and other records remain available for review.

**Acceptance criteria**

- Given an active branch, when an authorized central administrator suspends it, then its status changes to suspended.
- Given a suspended branch, when a user attempts to start a new operational transaction for it, then the system rejects the transaction.
- Given a branch with historical invoices, when the branch is suspended, then those invoices remain available for authorized review.

## 1.2 Employee and account management

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-ORG-006 | The system shall allow central administration to create and manage employee accounts. | Must |
| BRS-ORG-007 | The system shall allow central administration to update employee employment information. | Must |
| BRS-ORG-008 | The system shall allow central administration to activate or suspend an employee account. | Must |
| BRS-ORG-009 | Suspending an employee account shall revoke its active sessions and prevent new sign-ins. | Must |
| BRS-ORG-010 | The system shall prevent permanent deletion of an employee who is linked to operational or financial records. | Must |
| BRS-ORG-011 | The system shall allow central administration to assign an employee to one or more branches. | Must |
| BRS-ORG-012 | The system shall support employee transfers between branches while preserving assignment history. | Must |
| BRS-ORG-013 | On the effective date of a transfer, permissions for the previous branch shall end and permissions for the new branch shall apply according to the approved assignment. | Must |
| BRS-ORG-014 | The system should allow a branch to submit employee-account requests to central administration without granting the branch authority to execute those account changes itself. | Should |

**Use case — Transfer an employee**

1. A central administrator selects an employee.
2. The administrator selects the destination branch and transfer effective date.
3. The administrator approves the transfer.
4. The employee's previous branch permissions end according to the effective date.
5. The new branch permissions apply according to the employee's assignment.
6. The system preserves the previous and new assignment records.

**Acceptance criteria**

- Given an employee assigned to a branch, when central administration transfers the employee to another branch and the transfer becomes effective, then the employee cannot perform new operations in the previous branch.
- Given an employee with historical assignments, when an authorized user reviews the employee record, then previous and current assignments and their dates are visible.
- Given an active employee account, when central administration suspends it, then active sessions are revoked and subsequent sign-ins are denied.

## 1.3 Roles and permissions

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-ORG-015 | The system shall support the approved core roles: Central Admin, Branch Manager, Cashier, Inventory Clerk, and Pricing Clerk. | Must |
| BRS-ORG-016 | The system shall allow central administration to assign a role to an employee. | Must |
| BRS-ORG-017 | The system shall allow central administration to grant or revoke additional permissions for an employee without changing the employee's core role. | Must |
| BRS-ORG-018 | The system shall enforce an employee's effective permissions based on role, branch assignments, and additional permissions granted. | Must |
| BRS-ORG-019 | A Branch Manager shall not independently create employee accounts or administer employee permissions. | Must |
| BRS-ORG-020 | The system shall prevent disabling the last active central administrator or removing that administrator's essential administrative permissions if doing so would leave central administration without an active administrator. | Must |

**Acceptance criteria**

- Given an employee with the Cashier role, when central administration grants an additional permission, then the permission becomes available without changing the employee's role.
- Given an employee without permission to perform an operation, when the employee attempts that operation, then the system rejects it.
- Given only one active central administrator remains, when an action would disable that administrator or remove essential administrative permissions, then the system blocks the action.

## 1.4 Sessions, devices, and active branch context

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-ORG-021 | The system shall allow an authorized employee to sign in from more than one device. | Must |
| BRS-ORG-022 | The system shall maintain records of registered devices, active sessions, and previous sessions. | Must |
| BRS-ORG-023 | The system shall allow central administration to revoke one or more employee sessions. | Must |
| BRS-ORG-024 | Each work session shall have exactly one active branch context at a time. | Must |
| BRS-ORG-025 | During a session, an employee may change the active branch only to a branch where the employee is assigned and authorized. | Must |
| BRS-ORG-026 | The system shall audit active-branch changes, including employee, previous branch, new branch, and timestamp. | Must |

**Use case — Change active branch**

1. An employee signs in.
2. The employee selects a branch from the branches available to them.
3. The system validates the employee's assignment and permissions.
4. If authorized, the selected branch becomes the session's sole active branch.
5. The system records the change.

**Acceptance criteria**

- Given an employee assigned to two branches, when the employee selects one branch, then the session has only that branch as its active context.
- Given an employee not assigned to a branch, when the employee attempts to select it, then the system rejects the change.
- Given a successful branch change, when an authorized reviewer checks the audit history, then the employee, previous branch, new branch, and timestamp are recorded.

## 1.5 Administrative audit

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-ORG-027 | The system shall audit important administrative changes, including branch and employee creation, updates, suspension, role and permission changes, and employee assignments. | Must |
| BRS-ORG-028 | Each applicable audit record shall identify the actor, timestamp, affected entity, and previous and new values. | Must |
| BRS-ORG-029 | Operational users shall not be able to alter or delete protected administrative audit records. | Must |

---

# Unit 02 — Products, Categories, Selling Units, and Barcodes

## 2.1 Product and category management

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-PRD-001 | The system shall allow central administration to create and centrally manage products. | Must |
| BRS-PRD-002 | The system shall allow central administration to create and organize parent and child categories and assign products to them. | Must |
| BRS-PRD-003 | The system shall allow central administration to update product descriptive information, such as name, description, category, and image. | Must |
| BRS-PRD-004 | Updating descriptive information shall not change the SKU identity or alter historical invoices and movements. | Must |
| BRS-PRD-005 | The system shall allow central administration to deactivate a product instead of deleting it when historical transactions are linked to it. | Must |
| BRS-PRD-006 | A deactivated product shall not be available for new sales or other new transactions, while its historical records remain available. | Must |
| BRS-PRD-007 | The system shall allow central administration to define the branches in which a product is available or permitted for sale. | Must |
| BRS-PRD-008 | The system should allow a branch to submit a request to central administration to add a product, without creating or approving the product directly. | Should |

**Use case — Request a new product**

1. A branch submits a request containing the proposed product information.
2. Central administration receives and reviews the request.
3. Central administration approves or rejects it.
4. If approved, central administration creates and activates the product.
5. The product becomes available to the branches selected in its settings.

**Acceptance criteria**

- Given a branch employee, when they submit a product request, then a request is created for central review and no product is automatically approved.
- Given a product linked to historical invoices, when central administration changes its name or description, then historical invoice records are not rewritten.
- Given a deactivated product, when a cashier attempts to sell it, then the system blocks the sale.

## 2.2 SKU and barcode management

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-PRD-009 | Each distinct product type or size shall have its own SKU, in accordance with the approved rule that distinct sizes and types are separate products. | Must |
| BRS-PRD-010 | The system shall allow more than one barcode to be associated with the same product when required. | Must |
| BRS-PRD-011 | The system shall prevent the same barcode from being associated with more than one active product. | Must |
| BRS-PRD-012 | Central administration shall be able to add, deactivate, or update product barcodes within its permissions, with changes audited. | Must |
| BRS-PRD-013 | The system shall identify a product when any of its active associated barcodes is scanned. | Must |

**Use case — Attempt to assign a duplicate barcode**

1. An authorized user attempts to associate a barcode with a product.
2. The system checks whether that barcode is already associated with another active product.
3. If it is already in use, the system rejects the association and explains the reason.
4. Otherwise, the system accepts the association.

**Acceptance criteria**

- Given a barcode assigned to an active product, when a user attempts to assign it to another product, then the system rejects the operation.
- Given a product with multiple active barcodes, when a cashier scans any one of them, then the system identifies the same product.
- Given two products with different sizes, when they are registered, then each has a separate SKU.

## 2.3 Selling units, conversions, and weight

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-PRD-014 | The system shall allow approved selling units to be defined for each product. | Must |
| BRS-PRD-015 | The system shall support conversion factors between units when a product can be sold in more than one unit. | Must |
| BRS-PRD-016 | The system shall preserve weighed quantities to a precision of up to three decimal places. | Must |
| BRS-PRD-017 | The system shall support manual weight entry or weight received from a scale, according to the approved operating configuration. | Must |
| BRS-PRD-018 | The system shall validate that the selling unit and any conversion used are defined and approved for the product. | Must |
| BRS-PRD-019 | The system shall record the quantity, selling unit, and conversion used in the sale and inventory movement so the transaction can be reviewed. | Must |

**Acceptance criteria**

- Given a product with approved selling units and conversion factors, when it is sold in a unit different from its inventory unit, then the system calculates the equivalent quantity using the approved conversion factor.
- Given a weighed product, when a user enters a weight with three decimal places, then the system preserves that precision.
- Given a unit not defined for the product, when a user attempts to sell using that unit, then the system rejects the transaction.

## 2.4 Composite products

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-PRD-020 | The system shall allow a composite product to be defined with its components and the quantity of each component. | Must |
| BRS-PRD-021 | The system shall support a single total selling price for a composite product, according to the approved decision. | Must |
| BRS-PRD-022 | Before completing a sale, the system shall verify that the branch has sufficient stock of every required component. | Must |
| BRS-PRD-023 | After a composite-product sale is completed, the system shall deduct the actual component quantities from the branch inventory. | Must |
| BRS-PRD-024 | The system shall prevent completion of the sale if any required component is unavailable in the required quantity. | Must |
| BRS-PRD-025 | The system shall preserve the components and quantities consumed by each completed composite-product sale for later review. | Must |

**Use case — Sell a composite product**

1. The cashier adds the composite product to the sale.
2. The system checks the availability of every component at the selling branch.
3. The system presents the approved total price.
4. After the sale and payment are completed, the system deducts the component quantities according to the approved composition.
5. The system retains the consumption details linked to the invoice.

**Acceptance criteria**

- Given all required components are available, when the sale is completed, then the specified component quantities are deducted.
- Given one component is unavailable in the required quantity, when the cashier attempts to complete the sale, then the system blocks it.
- Given a completed composite-product invoice, when it is reviewed, then its consumed components and quantities are visible.

---

# Unit 03 — Suppliers, Purchasing, and Receiving

## 3.1 Supplier management

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-PUR-001 | The system shall allow central administration to create, update, and deactivate supplier records. | Must |
| BRS-PUR-002 | The system shall maintain supplier information and the purchasing, receiving, and return transactions associated with each supplier. | Must |
| BRS-PUR-003 | The system shall allow central administration to record supplier-specific purchase-return terms. | Must |
| BRS-PUR-004 | Where available, supplier return terms shall cover return reasons, time limits, goods condition, packaging, expiry, transport costs, and compensation method. | Must |
| BRS-PUR-005 | Missing recorded return terms shall not prevent creation of a return request; the system shall warn central administration to verify the supplier's terms before approving the return. | Must |

**Acceptance criteria**

- Given a supplier with recorded return terms, when central administration reviews the supplier, then those terms are displayed.
- Given a supplier with no recorded return terms, when a return request is created, then the system allows the request and warns central administration.
- Given a deactivated supplier, when a user attempts to create a new purchase transaction for that supplier, then the system blocks the transaction according to the supplier's status.

## 3.2 Branch needs and purchase orders

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-PUR-006 | The system shall allow a branch to submit a stock-needs request containing requested products, quantities, and a reason when applicable. | Must |
| BRS-PUR-007 | The system shall allow central administration to review a needs request and approve, reject, or adjust its quantities before creating a purchase. | Must |
| BRS-PUR-008 | Creating and approving purchase orders shall be the responsibility of central administration. | Must |
| BRS-PUR-009 | A purchase order shall be associated with a supplier and the approved items, quantities, prices, and terms. | Must |
| BRS-PUR-010 | The system shall preserve the purchase-order status, date, creator, and approver. | Must |
| BRS-PUR-011 | The system shall support exceptional receiving without a prior purchase order only with central approval and a recorded reason. | Must |

**Use case — Branch submits a needs request**

1. An authorized branch employee identifies the required products and quantities.
2. The employee submits the request to central administration.
3. Central administration reviews the request.
4. Central administration approves, rejects, or adjusts the requested quantities.
5. If approved, central administration creates and approves the purchase order.

**Acceptance criteria**

- Given a submitted needs request, when central administration reviews it, then a documented decision can be recorded.
- Given a branch employee, when they attempt to create an approved purchase order directly, then the system blocks the action.
- Given receiving without a purchase order, when central administration approves the exception and records its reason, then the system allows an exceptional receiving record to be created.

## 3.3 Purchase receiving

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-PUR-012 | The system shall support partial and complete receiving against a purchase order. | Must |
| BRS-PUR-013 | The system shall record ordered, received, and outstanding quantities for each item. | Must |
| BRS-PUR-014 | The system shall record quantities received in excess of the ordered quantity separately from approved quantities. | Must |
| BRS-PUR-015 | Excess quantities shall not become available inventory for sale until approved by central administration. | Must |
| BRS-PUR-016 | The system shall record the cost of each received batch separately. | Must |
| BRS-PUR-017 | The system shall record receiving date, supplier, branch, responsible employee, quantities, cost, and related references. | Must |
| BRS-PUR-018 | The system shall record batch and expiry information for products that require expiry tracking. | Must |
| BRS-PUR-019 | The system shall preserve a history of receiving changes, exceptions, and approvals. | Must |

**Use case — Receive excess quantity**

1. A branch receives more than the quantity ordered.
2. The receiving employee records the actual quantity.
3. The system separates the excess from the approved quantity.
4. The excess is submitted to central administration for review.
5. The excess does not become available for sale until approved.

**Acceptance criteria**

- Given a partially received purchase order, when the employee records the received quantity, then the system preserves the outstanding quantity.
- Given a quantity greater than the ordered quantity, when receiving is recorded, then the system separates the excess from available inventory.
- Given two batches with different costs, when receiving is recorded, then each batch retains its own cost.
- Given a product requiring expiry tracking, when it is received, then the required expiry information must be recorded to complete the batch record according to the product policy.

## 3.4 Supplier purchase returns

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-PUR-020 | The system shall allow creation of a supplier return request linked to the original purchase and receiving transaction. | Must |
| BRS-PUR-021 | A return request shall record items, quantities, batches, and return reasons. | Must |
| BRS-PUR-022 | Central administration shall review and approve supplier return requests according to its permissions and the supplier's recorded terms. | Must |
| BRS-PUR-023 | The system shall record the return outcome and any associated compensation or settlement method when available. | Must |
| BRS-PUR-024 | The system shall record the return's effect on branch inventory, batch costs, and supplier history. | Must |
| BRS-PUR-025 | The system shall prevent a return quantity from exceeding the quantity available for return from the linked batch or receiving transaction. | Must |

**Acceptance criteria**

- Given an original receiving transaction, when a return request is created, then it is linked to the original receiving, supplier, and batch.
- Given that the quantity available for return is lower than the requested return quantity, when a user attempts to record the excess, then the system rejects it.
- Given an unapproved return request, when a branch attempts to apply its final inventory effect, then the system blocks execution until approval.

---

# Unit 04 — Inventory, Batches, Costing, and Stocktaking

## 4.1 Branch inventory management

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-INV-001 | The system shall maintain independent inventory for each branch. | Must |
| BRS-INV-002 | The system shall display each product's balance separately for each branch. | Must |
| BRS-INV-003 | The system shall record every inventory movement with its source and related transaction reference. | Must |
| BRS-INV-004 | The system shall prevent sales exceeding the actual quantity available for sale at the branch. | Must |
| BRS-INV-005 | The system shall distinguish physically held, reserved, quarantined, and otherwise unavailable quantities. | Must |
| BRS-INV-006 | The system shall support storage locations within a branch, such as sales floor, warehouse, and refrigerators. | Must |
| BRS-INV-007 | The system should record transfers between storage locations within a branch and update the associated balances. | Should |

**Acceptance criteria**

- Given a product with balances at two branches, when inventory is reviewed, then the balances are displayed separately.
- Given a quantity reserved for an online order, when a cashier attempts to sell it, then the system does not treat it as available for sale.
- Given a quarantined quantity, when a user attempts to sell it, then the system blocks the sale.
- Given an inventory movement, when it is reviewed, then its source, transaction reference, employee, and timestamp are available.

## 4.2 Batches and costing

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-INV-008 | The system shall maintain each inventory batch separately from other batches. | Must |
| BRS-INV-009 | The system shall preserve each batch's original cost without combining costs into an average product cost. | Must |
| BRS-INV-010 | The system shall record each batch's remaining quantity, cost, and source. | Must |
| BRS-INV-011 | The system shall issue batches using FIFO for products not tracked by expiry. | Must |
| BRS-INV-012 | The system shall issue batches using FEFO for products requiring expiry tracking. | Must |
| BRS-INV-013 | The system shall prevent expired batches from being issued for sale. | Must |
| BRS-INV-014 | The system shall preserve the batch-level quantities issued for each sale. | Must |
| BRS-INV-015 | The system shall calculate cost of goods sold using the actual costs of the batches issued. | Must |
| BRS-INV-016 | A completed sale's historical cost shall not change when the product's purchase cost changes later. | Must |

**Use case — Sell a product from multiple batches**

1. A cashier requests a product quantity.
2. The system selects batches according to the product's approved issuing policy.
3. The system allocates the requested quantity across eligible batches.
4. After the sale is completed, the system records the quantity and cost issued from each batch.
5. Central administration can review the historical cost of goods sold.

**Acceptance criteria**

- Given a product not tracked by expiry with multiple batches, when stock is issued, then FIFO is applied.
- Given a product tracked by expiry with multiple batches, when stock is issued, then FEFO is applied.
- Given an expired batch, when the system attempts to issue it for sale, then the system blocks the issue.
- Given a later change in purchase cost, when a historical sale is reviewed, then its cost remains based on the batches actually issued.

## 4.3 Inter-branch transfers

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-INV-017 | The system shall allow an authorized branch employee to submit an inventory-transfer request to Central Administration for movement between two branches. | Must |
| BRS-INV-018 | A transfer request shall require approval before dispatch is executed. | Must |
| BRS-INV-019 | The system shall record transferred items, quantities, batches, sending branch, and receiving branch. | Must |
| BRS-INV-020 | Central Administration employees shall execute the physical dispatch of an approved transfer; the quantity shall be deducted from the sending branch's inventory when Central Administration records the dispatch. | Must |
| BRS-INV-021 | Central Administration employees shall execute and record the physical receipt at the destination branch; the quantity shall be added to the receiving branch's inventory only when Central Administration records the actual receipt. | Must |
| BRS-INV-022 | The system shall record differences between dispatched and received quantities and submit them for review according to permissions. | Must |
| BRS-INV-023 | The system shall preserve the transfer status, stages, responsible users, and timestamps. | Must |

**Use case — Transfer inventory between branches**

1. A branch submits a transfer request to Central Administration.
2. Central Administration reviews and approves or rejects the request.
3. Central Administration employees execute and record the physical dispatch.
4. The dispatched quantity is deducted from the sending branch's inventory.
5. Central Administration employees execute and record the physical receipt at the destination branch.
6. Only the quantity actually received is added to the destination branch's inventory.
7. Any difference is recorded for Central Administration review; unreceived quantities do not become available inventory.

**Acceptance criteria**

- Given an unapproved transfer request, when any branch employee attempts to execute dispatch, then the system blocks execution.
- Given an approved transfer, when an authorized Central Administration employee records physical dispatch, then the dispatched quantity is deducted from the sending branch.
- Given a shipment in transit, when the destination branch inventory is reviewed, then the quantity is not shown as available there.
- Given an authorized Central Administration employee records physical receipt, then only the actually received quantity is added to the destination branch.
- Given a difference between dispatched and received quantities, when receipt is recorded, then the difference is presented to Central Administration for review.

## 4.4 Stocktaking and variance adjustments

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-INV-024 | The system shall support full, partial, and surprise stocktaking. | Must |
| BRS-INV-025 | The system shall allow the stocktake scope to be defined by branch, products, and storage locations. | Must |
| BRS-INV-026 | Physical stocktaking shall be performed by assigned Central Administration employees using blind-count mode; expected balances shall not be displayed to the employees performing the count. | Must |
| BRS-INV-027 | The system shall record the actual quantities entered by the assigned Central Administration stocktaking employees. | Must |
| BRS-INV-028 | After counting is completed, the system shall compare actual quantities with recorded balances. | Must |
| BRS-INV-029 | The system shall record stock variances and request an explanation when a variance exists. | Must |
| BRS-INV-030 | The system shall not change inventory balances due to a stocktake variance before the required central approval. | Must |
| BRS-INV-031 | The system shall preserve stocktake results, variance explanations, decisions, and approvals. | Must |

**Use case — Surprise stocktake**

1. An authorized user creates a surprise stocktake for a branch or selected products.
2. The system defines the products and storage locations in scope.
3. Assigned Central Administration employees enter actual quantities without seeing expected balances.
4. After counting is completed, the system compares the results with recorded balances.
5. Variances are displayed and explanations are requested.
6. Central administration reviews the results and approves or rejects the adjustment.

**Acceptance criteria**

- Given a Central Administration employee assigned to count stock, when the employee opens the stocktake, then expected balances are not visible.
- Given a branch employee, when the employee attempts to perform the physical stock count, then the system does not grant that role the stocktaking execution authority.
- Given a completed count, when the system calculates the variance, then actual quantity, recorded balance, and difference are displayed.
- Given an unapproved variance, when the system attempts to update inventory because of it, then the update is blocked.
- Given an approved adjustment, when it is applied, then the responsible user, reason, quantity, and timestamp are recorded.

## 4.5 Damaged, expired, and near-expiry products

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-INV-032 | The system shall allow damaged or expired products to be recorded and quarantined so they are excluded from available-for-sale inventory. | Must |
| BRS-INV-033 | The system shall prevent quarantined or expired products from being sold. | Must |
| BRS-INV-034 | The system shall submit inventory adjustment or destruction requests to central administration for approval. | Must |
| BRS-INV-035 | The system shall record the quarantine reason, quantity, batch, responsible employee, and timestamp. | Must |
| BRS-INV-036 | The system shall support near-expiry alerts using thresholds defined by central administration. | Must |
| BRS-INV-037 | The system shall allow central administration to review near-expiry products by branch and batch. | Must |

**Acceptance criteria**

- Given a damaged product, when an employee records it for quarantine, then it is excluded from available-for-sale quantity.
- Given an unapproved destruction request, when a branch attempts to perform the final adjustment, then the system blocks it.
- Given a batch approaching its expiry date, when its alert threshold is met, then the system issues an alert to the configured recipients.
- Given an expired product, when a cashier attempts to sell it, then the system rejects the sale.

## 4.6 Reorder thresholds and stock-increase requests

| ID | Functional requirement | Priority |
|---|---|---|
| BRS-INV-038 | The system shall allow central administration to define a reorder threshold for each product at each branch. | Must |
| BRS-INV-039 | The system shall monitor each branch's product balance against its approved reorder threshold. | Must |
| BRS-INV-040 | The system shall issue an alert when a product's balance reaches or falls below its threshold. | Must |
| BRS-INV-041 | The system shall allow a branch to submit a stock-increase request to central administration. | Must |
| BRS-INV-042 | The system shall allow central administration to review and decide on a stock-increase request. | Must |

**Acceptance criteria**

- Given a product with an approved reorder threshold, when its branch balance reaches or falls below that threshold, then the system issues an alert.
- Given a stock-increase request from a branch, when central administration reviews it, then central administration can approve or reject it and record the decision.
- Given an unapproved request, when the branch attempts to treat the requested increase as approved inventory, then the system blocks that treatment.

---

## Review perspectives

The following are analytical review perspectives, not claims that external experts participated:

1. **Business analyst:** The four units form a coherent flow: product definitions support purchasing and receiving; receiving creates inventory batches; inventory supports sales, transfers, and stocktaking.
2. **Implementation reviewer:** The requirements are expressed as business outcomes and do not prescribe database tables, controllers, or API endpoints. The implementation must distinguish physical, reserved, quarantined, and available quantities, as well as dispatch from receipt and request creation from approval.
3. **Auditor:** The acceptance criteria cover important exception cases, including duplicate barcodes, excess and partial receiving, stocktake variances, expired batches, and incomplete transfers.
4. **Critical reviewer:** During detailed design, special attention is required for branch-scoped permissions, reserved and quarantined quantities, batch identity and cost during returns and transfers, and central approval boundaries. These are implementation controls, not invitations to reopen approved business decisions.
5. **Advisor:** Preserve these requirement identifiers for the traceability matrix and test cases. Each requirement must ultimately be linked to its originating approved decision or recommendation.

## Traceability and status

This part records the approved functional requirements for Units 01–04. The complete traceability matrix linking each requirement to its source decision identifier and verification test will be produced during the BRS traceability stage.

This document is Part 2 only. It does not complete the BRS. Units 05–12, shared business rules, end-to-end scenarios, non-functional requirements, and the final traceability matrix remain separate deliverables for review and approval.

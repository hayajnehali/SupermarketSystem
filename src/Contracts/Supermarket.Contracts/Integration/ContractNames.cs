namespace Supermarket.Contracts.Integration;

/// <summary>Stable names for integration contracts; names are not localized.</summary>
public static class ContractNames
{
    public const string BranchAccessSnapshotPublished = "branch.access-snapshot.published";
    public const string CatalogProductProjectionPublished = "catalog.product-projection.published";
    public const string PurchaseOrderApproved = "procurement.purchase-order.approved";
    public const string PurchaseReceiptDecisionIssued = "procurement.purchase-receipt.decision-issued";
    public const string StockReservationRequested = "inventory.stock-reservation.requested";
    public const string StockReservationResult = "inventory.stock-reservation.result";
    public const string CentralCommandIssued = "sync.central-command.issued";
    public const string BranchCommandAcknowledged = "sync.branch-command.acknowledged";
}
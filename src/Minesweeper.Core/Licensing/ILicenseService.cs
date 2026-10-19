namespace Minesweeper.Core.Licensing;

public enum PurchaseOutcome
{
    Purchased,
    Cancelled,
    AlreadyOwned,
    Error,
}

/// <summary>The single source of truth for "does this install show ads right now", and the one door
/// through which a purchase happens. Same split as PdfReader.Core/DocScanner.Core's own ILicenseService:
/// implemented once against Google Play Billing; everything else asks this interface.</summary>
public interface ILicenseService
{
    LicenseState State { get; }
    string? ProPriceText { get; }
    string? LastError { get; }
    event Action? Changed;

    Task RefreshAsync();
    Task<PurchaseOutcome> PurchaseProAsync();
    Task<PurchaseOutcome> RestoreAsync();
}

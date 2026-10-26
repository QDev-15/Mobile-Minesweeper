namespace Minesweeper.Core.Licensing;

/// <summary>Whether the paid unlock ("Pro") has been bought. Pure data (no Play Billing, no storage): fully
/// unit-testable. Same split as PdfReader.Core.Licensing/DocScanner.Core.Licensing.</summary>
public sealed record LicenseState(bool IsPro)
{
    public static readonly LicenseState Initial = new(false);
}

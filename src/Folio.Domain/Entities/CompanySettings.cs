using Folio.Domain.Enums;

namespace Folio.Domain.Entities;

/// <summary>
/// The single company profile Folio operates for (SPEC §2.2). There is exactly one row.
/// </summary>
public sealed class CompanySettings
{
    public Guid Id { get; }

    public string Name { get; private set; }

    public string LegalId { get; private set; }

    public string? Address { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public byte[]? Logo { get; private set; }

    public Currency FunctionalCurrency { get; }

    public int FiscalYearStartMonth { get; private set; }

    /// <summary>
    /// The VAT rate as a fraction, e.g. 0.13m for the default 13% IVA.
    /// </summary>
    public decimal VatRate { get; private set; }

    public CompanySettings(
        string name,
        string legalId,
        int fiscalYearStartMonth,
        decimal vatRate,
        Currency functionalCurrency = Currency.CRC)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Company name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(legalId))
        {
            throw new ArgumentException("Legal id (cédula jurídica) is required.", nameof(legalId));
        }

        ValidateFiscalYearStartMonth(fiscalYearStartMonth);
        ValidateVatRate(vatRate);

        Id = Guid.NewGuid();
        Name = name;
        LegalId = legalId;
        FunctionalCurrency = functionalCurrency;
        FiscalYearStartMonth = fiscalYearStartMonth;
        VatRate = vatRate;
    }

    public void UpdateProfile(string name, string legalId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Company name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(legalId))
        {
            throw new ArgumentException("Legal id (cédula jurídica) is required.", nameof(legalId));
        }

        Name = name;
        LegalId = legalId;
    }

    public void UpdateContactInfo(string? address, string? phone, string? email)
    {
        Address = address;
        Phone = phone;
        Email = email;
    }

    public void UpdateVatRate(decimal vatRate)
    {
        ValidateVatRate(vatRate);
        VatRate = vatRate;
    }

    public void UpdateFiscalYearStartMonth(int month)
    {
        ValidateFiscalYearStartMonth(month);
        FiscalYearStartMonth = month;
    }

    public void SetLogo(byte[]? logo) => Logo = logo;

    private static void ValidateFiscalYearStartMonth(int month)
    {
        if (month < 1 || month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "Fiscal year start month must be between 1 and 12.");
        }
    }

    private static void ValidateVatRate(decimal vatRate)
    {
        if (vatRate < 0 || vatRate > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(vatRate), vatRate, "VAT rate must be between 0 and 1 (e.g. 0.13 for 13%).");
        }
    }
}

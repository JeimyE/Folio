namespace Folio.Domain.Entities;

/// <summary>
/// One line of an <see cref="EntryTemplate"/>. Unlike a <see cref="JournalLine"/>, the
/// amount is optional (SPEC §2.4: templates are saved "without amounts required") —
/// it is only a suggestion for when a draft is created from the template.
/// </summary>
public sealed class EntryTemplateLine
{
    public Guid AccountId { get; }

    public bool IsDebit { get; }

    public decimal? DefaultAmount { get; }

    public string? Description { get; }

    public EntryTemplateLine(Guid accountId, bool isDebit, decimal? defaultAmount = null, string? description = null)
    {
        if (defaultAmount is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultAmount), defaultAmount, "Default amount must be greater than zero when provided.");
        }

        AccountId = accountId;
        IsDebit = isDebit;
        DefaultAmount = defaultAmount;
        Description = description;
    }
}

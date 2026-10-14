using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;

namespace Folio.Domain.Entities;

/// <summary>
/// A saved recurring entry (rent, payroll) used to pre-fill a new draft (SPEC §2.4).
/// </summary>
public sealed class EntryTemplate
{
    private readonly List<EntryTemplateLine> _lines = [];

    public Guid Id { get; }

    public string Name { get; private set; }

    public string? DefaultDescription { get; private set; }

    public IReadOnlyList<EntryTemplateLine> Lines => _lines;

    public EntryTemplate(string name, string? defaultDescription = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name;
        DefaultDescription = defaultDescription;
    }

    public void AddLine(EntryTemplateLine line) => _lines.Add(line);

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.", nameof(name));
        }

        Name = name;
    }

    /// <summary>
    /// Creates a new draft entry from this template. Only template lines that carry a
    /// <see cref="EntryTemplateLine.DefaultAmount"/> become real lines — a
    /// <see cref="JournalLine"/> can never have a zero amount, so lines without a
    /// default are left for the user to fill in via the entry editor (M5).
    /// </summary>
    public JournalEntry CreateDraftEntry(DateOnly date, Guid createdByUserId, DateTime createdAtUtc, string? description = null)
    {
        var entry = new JournalEntry(date, description ?? DefaultDescription ?? Name, reference: null, createdByUserId, createdAtUtc);

        foreach (var templateLine in _lines)
        {
            if (templateLine.DefaultAmount is not { } amount)
            {
                continue;
            }

            var money = new Money(amount, Currency.CRC);
            var line = templateLine.IsDebit
                ? JournalLine.CreateDebit(templateLine.AccountId, money, templateLine.Description)
                : JournalLine.CreateCredit(templateLine.AccountId, money, templateLine.Description);

            entry.AddLine(line);
        }

        return entry;
    }
}

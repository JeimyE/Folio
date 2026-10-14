using Folio.Domain.Enums;
using Folio.Domain.Exceptions;

namespace Folio.Domain.Entities;

/// <summary>
/// A double-entry accounting transaction: a header plus two or more balanced lines.
/// Lifecycle: <see cref="JournalEntryStatus.Draft"/> → <see cref="JournalEntryStatus.Posted"/>
/// → (<see cref="JournalEntryStatus.Reversed"/>). Posted entries are immutable; the only
/// way to correct one is <see cref="Reverse"/>.
/// </summary>
public sealed class JournalEntry
{
    private readonly List<JournalLine> _lines = [];

    public Guid Id { get; }

    public int? Number { get; private set; }

    public DateOnly Date { get; private set; }

    public string Description { get; private set; }

    public string? Reference { get; private set; }

    public JournalEntryStatus Status { get; private set; }

    public Guid CreatedByUserId { get; }

    public DateTime CreatedAtUtc { get; }

    public Guid? PostedByUserId { get; private set; }

    public DateTime? PostedAtUtc { get; private set; }

    public Guid? PeriodId { get; private set; }

    /// <summary>Set when this entry is itself the reversal of another entry.</summary>
    public Guid? ReversesEntryId { get; private init; }

    public IReadOnlyList<JournalLine> Lines => _lines;

    // Used by EF Core to materialize an existing row; see Account's private constructor.
    private JournalEntry()
    {
        Description = null!; // overwritten by EF immediately after construction
    }

    public JournalEntry(
        DateOnly date,
        string description,
        string? reference,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Journal entry description is required.", nameof(description));
        }

        Id = Guid.NewGuid();
        Date = date;
        Description = description;
        Reference = reference;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        Status = JournalEntryStatus.Draft;
    }

    public void AddLine(JournalLine line)
    {
        EnsureEditable();
        line.LineNumber = _lines.Count + 1;
        _lines.Add(line);
    }

    public void UpdateLine(int lineNumber, JournalLine newLine)
    {
        EnsureEditable();
        var index = _lines.FindIndex(l => l.LineNumber == lineNumber);
        if (index < 0)
        {
            throw new ArgumentException($"There is no line numbered {lineNumber}.", nameof(lineNumber));
        }

        newLine.LineNumber = lineNumber;
        _lines[index] = newLine;
    }

    public void RemoveLine(int lineNumber)
    {
        EnsureEditable();
        var removed = _lines.RemoveAll(l => l.LineNumber == lineNumber);
        if (removed == 0)
        {
            throw new ArgumentException($"There is no line numbered {lineNumber}.", nameof(lineNumber));
        }

        Renumber();
    }

    public void UpdateHeader(DateOnly date, string description, string? reference)
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Journal entry description is required.", nameof(description));
        }

        Date = date;
        Description = description;
        Reference = reference;
    }

    /// <summary>
    /// Validates and posts the entry. The Domain has no repository, so the caller
    /// supplies everything it needs to validate: the sequential <paramref name="number"/>
    /// (unique per fiscal year), the <paramref name="period"/> the entry's date must fall
    /// in, and every <see cref="Account"/> referenced by a line, keyed by id.
    /// </summary>
    public void Post(
        int number,
        Period period,
        IReadOnlyDictionary<Guid, Account> accountsById,
        Guid postedByUserId,
        DateTime postedAtUtc)
    {
        EnsureEditable();

        if (_lines.Count < 2)
        {
            throw new JournalEntryHasFewerThanTwoLinesException(_lines.Count);
        }

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        foreach (var line in _lines)
        {
            if (!accountsById.TryGetValue(line.AccountId, out var account))
            {
                throw new AccountNotProvidedException(line.AccountId);
            }

            if (!account.IsPostable)
            {
                throw new NonPostableAccountException(line.AccountId);
            }

            if (!account.IsActive)
            {
                throw new InactiveAccountException(line.AccountId);
            }

            totalDebit += line.Debit;
            totalCredit += line.Credit;
        }

        if (totalDebit != totalCredit)
        {
            throw new UnbalancedEntryException(totalDebit, totalCredit);
        }

        if (!period.IsOpenFor(Date))
        {
            throw new PeriodNotOpenException(Date);
        }

        Number = number;
        PeriodId = period.Id;
        PostedByUserId = postedByUserId;
        PostedAtUtc = postedAtUtc;
        Status = JournalEntryStatus.Posted;
    }

    /// <summary>
    /// Creates and posts the mirror-image entry that reverses this one. This entry
    /// moves to <see cref="JournalEntryStatus.Reversed"/>; the new entry is itself
    /// posted (not reversible further) and references this entry via
    /// <see cref="ReversesEntryId"/>.
    /// </summary>
    public JournalEntry Reverse(
        int reversalNumber,
        DateOnly reversalDate,
        Period reversalPeriod,
        IReadOnlyDictionary<Guid, Account> accountsById,
        Guid reversedByUserId,
        DateTime reversedAtUtc)
    {
        if (Status == JournalEntryStatus.Draft)
        {
            throw new CannotReverseDraftEntryException();
        }

        if (Status == JournalEntryStatus.Reversed)
        {
            throw new EntryAlreadyReversedException();
        }

        if (ReversesEntryId is not null)
        {
            throw new CannotReverseAReversalEntryException();
        }

        var reversal = new JournalEntry(
            reversalDate,
            $"Reversal of entry {Number}: {Description}",
            Reference,
            reversedByUserId,
            reversedAtUtc)
        {
            ReversesEntryId = Id,
        };

        foreach (var line in _lines)
        {
            reversal.AddLine(line.Mirror());
        }

        reversal.Post(reversalNumber, reversalPeriod, accountsById, reversedByUserId, reversedAtUtc);

        Status = JournalEntryStatus.Reversed;
        return reversal;
    }

    private void Renumber()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].LineNumber = i + 1;
        }
    }

    private void EnsureEditable()
    {
        if (Status != JournalEntryStatus.Draft)
        {
            throw new EntryIsNotEditableException(Status);
        }
    }
}

using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.Exceptions;
using Folio.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class JournalEntryTests
{
    private static readonly DateOnly EntryDate = new(2026, 3, 15);
    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private static Account CreatePostableAccount() =>
        new(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);

    private static Period CreateOpenPeriod() =>
        new(Guid.NewGuid(), 2026, 3, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    private static JournalEntry CreateDraft() =>
        new(EntryDate, "Venta de café y repostería", reference: "F-001", UserId, Now);

    private static (JournalEntry Entry, Account Cash, Account Sales, IReadOnlyDictionary<Guid, Account> Accounts) CreateBalancedDraft()
    {
        var entry = CreateDraft();
        var cash = CreatePostableAccount();
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);

        entry.AddLine(JournalLine.CreateDebit(cash.Id, new Money(56_500m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(sales.Id, new Money(56_500m, Currency.CRC)));

        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [sales.Id] = sales };
        return (entry, cash, sales, accounts);
    }

    // --- Draft editing: only line-level invariants apply while in Draft ---

    [Fact]
    public void Constructor_NewEntry_StartsAsDraftWithNoLines()
    {
        var entry = CreateDraft();

        entry.Status.Should().Be(JournalEntryStatus.Draft);
        entry.Lines.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_BlankDescription_Throws()
    {
        var act = () => new JournalEntry(EntryDate, "   ", null, UserId, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddLine_DraftWithOneLine_DoesNotThrow_IncompleteDraftsAreAllowed()
    {
        var entry = CreateDraft();

        entry.AddLine(JournalLine.CreateDebit(Guid.NewGuid(), new Money(100m, Currency.CRC)));

        entry.Lines.Should().HaveCount(1);
    }

    [Fact]
    public void AddLine_UnbalancedDraft_DoesNotThrow_UnbalancedDraftsAreAllowed()
    {
        var entry = CreateDraft();

        entry.AddLine(JournalLine.CreateDebit(Guid.NewGuid(), new Money(100m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(Guid.NewGuid(), new Money(50m, Currency.CRC)));

        entry.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateLine_DraftEntry_ReplacesLineAtSamePosition()
    {
        var entry = CreateDraft();
        var accountId = Guid.NewGuid();
        entry.AddLine(JournalLine.CreateDebit(accountId, new Money(100m, Currency.CRC)));

        entry.UpdateLine(1, JournalLine.CreateDebit(accountId, new Money(200m, Currency.CRC)));

        entry.Lines.Single().Debit.Should().Be(200m);
    }

    [Fact]
    public void RemoveLine_DraftEntry_RenumbersRemainingLines()
    {
        var entry = CreateDraft();
        entry.AddLine(JournalLine.CreateDebit(Guid.NewGuid(), new Money(100m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(Guid.NewGuid(), new Money(50m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(Guid.NewGuid(), new Money(50m, Currency.CRC)));

        entry.RemoveLine(1);

        entry.Lines.Should().HaveCount(2);
        entry.Lines[0].LineNumber.Should().Be(1);
        entry.Lines[1].LineNumber.Should().Be(2);
    }

    [Fact]
    public void RemoveLine_UnknownLineNumber_Throws()
    {
        var entry = CreateDraft();

        var act = () => entry.RemoveLine(1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddLine_PostedEntry_Throws()
    {
        var (entry, _, _, accounts) = CreateBalancedDraft();
        entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        var act = () => entry.AddLine(JournalLine.CreateDebit(Guid.NewGuid(), new Money(1m, Currency.CRC)));

        act.Should().Throw<EntryIsNotEditableException>();
    }

    // --- Post(): the full set of rules applies only here ---

    [Fact]
    public void Post_FewerThanTwoLines_Throws()
    {
        var entry = CreateDraft();
        var account = CreatePostableAccount();
        entry.AddLine(JournalLine.CreateDebit(account.Id, new Money(100m, Currency.CRC)));
        var accounts = new Dictionary<Guid, Account> { [account.Id] = account };

        var act = () => entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<JournalEntryHasFewerThanTwoLinesException>();
    }

    [Fact]
    public void Post_UnbalancedEntry_Throws()
    {
        var entry = CreateDraft();
        var cash = CreatePostableAccount();
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        entry.AddLine(JournalLine.CreateDebit(cash.Id, new Money(100m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(sales.Id, new Money(99m, Currency.CRC)));
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [sales.Id] = sales };

        var act = () => entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<UnbalancedEntryException>();
    }

    [Fact]
    public void Post_AccountNotInProvidedDictionary_Throws()
    {
        var entry = CreateDraft();
        var cash = CreatePostableAccount();
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        entry.AddLine(JournalLine.CreateDebit(cash.Id, new Money(100m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(sales.Id, new Money(100m, Currency.CRC)));
        // Only "cash" is provided; "sales" is missing.
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash };

        var act = () => entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<AccountNotProvidedException>();
    }

    [Fact]
    public void Post_LineAccountNotPostable_Throws()
    {
        var entry = CreateDraft();
        var cash = CreatePostableAccount();
        var parentAccount = new Account(new AccountCode("4"), "Ingresos", AccountType.Revenue, isPostable: false);
        entry.AddLine(JournalLine.CreateDebit(cash.Id, new Money(100m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(parentAccount.Id, new Money(100m, Currency.CRC)));
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [parentAccount.Id] = parentAccount };

        var act = () => entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<NonPostableAccountException>();
    }

    [Fact]
    public void Post_LineAccountInactive_Throws()
    {
        var entry = CreateDraft();
        var cash = CreatePostableAccount();
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        sales.Deactivate();
        entry.AddLine(JournalLine.CreateDebit(cash.Id, new Money(100m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(sales.Id, new Money(100m, Currency.CRC)));
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [sales.Id] = sales };

        var act = () => entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<InactiveAccountException>();
    }

    [Fact]
    public void Post_DateOutsideOpenPeriod_Throws()
    {
        var (entry, _, _, accounts) = CreateBalancedDraft();
        var periodForDifferentMonth = new Period(Guid.NewGuid(), 2026, 4, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        var act = () => entry.Post(1, periodForDifferentMonth, accounts, UserId, Now);

        act.Should().Throw<PeriodNotOpenException>();
    }

    [Fact]
    public void Post_DateInClosedPeriod_Throws()
    {
        var (entry, _, _, accounts) = CreateBalancedDraft();
        var period = CreateOpenPeriod();
        period.Close(UserId, Now);

        var act = () => entry.Post(1, period, accounts, UserId, Now);

        act.Should().Throw<PeriodNotOpenException>();
    }

    [Fact]
    public void Post_ValidEntry_SetsNumberPeriodAndStatusPosted()
    {
        var (entry, _, _, accounts) = CreateBalancedDraft();
        var period = CreateOpenPeriod();

        entry.Post(42, period, accounts, UserId, Now);

        entry.Number.Should().Be(42);
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.PeriodId.Should().Be(period.Id);
        entry.PostedByUserId.Should().Be(UserId);
        entry.PostedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Post_AlreadyPostedEntry_Throws()
    {
        var (entry, _, _, accounts) = CreateBalancedDraft();
        entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        var act = () => entry.Post(2, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<EntryIsNotEditableException>();
    }

    [Fact]
    public void Post_SplitForeignCurrencyLinesThatDoNotTieOutInCrc_Throws()
    {
        // Mirrors the rounding scenario from JournalLineTests: a $10.00 sale split as
        // $3.33 + $3.33 + $3.34 against a single $10.00 collection, where converting
        // each split line independently at ₡517.77 does not tie out to the direct
        // conversion of $10.00 — the Domain must reject it, not silently absorb it.
        const decimal rate = 517.77m;
        var entry = CreateDraft();
        var cash = CreatePostableAccount();
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);

        entry.AddLine(JournalLine.CreateForeignDebit(cash.Id, new Money(10.00m, Currency.USD), rate));
        entry.AddLine(JournalLine.CreateForeignCredit(sales.Id, new Money(3.33m, Currency.USD), rate));
        entry.AddLine(JournalLine.CreateForeignCredit(sales.Id, new Money(3.33m, Currency.USD), rate));
        entry.AddLine(JournalLine.CreateForeignCredit(sales.Id, new Money(3.34m, Currency.USD), rate));

        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [sales.Id] = sales };

        var act = () => entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<UnbalancedEntryException>();
    }

    [Fact]
    public void Post_SplitForeignCurrencyLinesWhereLastLineAbsorbsTheCrcRemainder_Succeeds()
    {
        // The caller rounds the total once and assigns the remainder to the last line,
        // instead of rounding each original-currency split independently. This is the
        // technique the rounding ADR (docs/adr/0002) prescribes for callers.
        const decimal rate = 517.77m;
        var entry = CreateDraft();
        var cash = CreatePostableAccount();
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);

        var totalDebit = JournalLine.CreateForeignDebit(cash.Id, new Money(10.00m, Currency.USD), rate);
        var creditLine1 = JournalLine.CreateForeignCredit(sales.Id, new Money(3.33m, Currency.USD), rate);
        var creditLine2 = JournalLine.CreateForeignCredit(sales.Id, new Money(3.33m, Currency.USD), rate);
        var remainder = totalDebit.Debit - creditLine1.Credit - creditLine2.Credit;
        var creditLine3 = JournalLine.CreateCredit(sales.Id, new Money(remainder, Currency.CRC));

        entry.AddLine(totalDebit);
        entry.AddLine(creditLine1);
        entry.AddLine(creditLine2);
        entry.AddLine(creditLine3);

        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [sales.Id] = sales };

        entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        entry.Status.Should().Be(JournalEntryStatus.Posted);
    }

    // --- Reverse() ---

    [Fact]
    public void Reverse_DraftEntry_Throws()
    {
        var entry = CreateDraft();
        var accounts = new Dictionary<Guid, Account>();

        var act = () => entry.Reverse(1, EntryDate, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<CannotReverseDraftEntryException>();
    }

    [Fact]
    public void Reverse_PostedEntry_CreatesPostedMirrorAndMarksOriginalReversed()
    {
        var (entry, cash, sales, accounts) = CreateBalancedDraft();
        entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);

        var reversal = entry.Reverse(2, EntryDate, CreateOpenPeriod(), accounts, UserId, Now);

        entry.Status.Should().Be(JournalEntryStatus.Reversed);
        reversal.Status.Should().Be(JournalEntryStatus.Posted);
        reversal.ReversesEntryId.Should().Be(entry.Id);
        reversal.Lines.Should().HaveCount(2);
        reversal.Lines.Single(l => l.AccountId == cash.Id).Credit.Should().Be(56_500m);
        reversal.Lines.Single(l => l.AccountId == sales.Id).Debit.Should().Be(56_500m);
    }

    [Fact]
    public void Reverse_AlreadyReversedEntry_Throws()
    {
        var (entry, _, _, accounts) = CreateBalancedDraft();
        entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);
        entry.Reverse(2, EntryDate, CreateOpenPeriod(), accounts, UserId, Now);

        var act = () => entry.Reverse(3, EntryDate, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<EntryAlreadyReversedException>();
    }

    [Fact]
    public void Reverse_AReversalEntry_Throws()
    {
        var (entry, _, _, accounts) = CreateBalancedDraft();
        entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);
        var reversal = entry.Reverse(2, EntryDate, CreateOpenPeriod(), accounts, UserId, Now);

        var act = () => reversal.Reverse(3, EntryDate, CreateOpenPeriod(), accounts, UserId, Now);

        act.Should().Throw<CannotReverseAReversalEntryException>();
    }

    [Fact]
    public void Reverse_DateOutsideOpenPeriod_Throws()
    {
        var (entry, _, _, accounts) = CreateBalancedDraft();
        entry.Post(1, CreateOpenPeriod(), accounts, UserId, Now);
        var closedPeriod = CreateOpenPeriod();
        closedPeriod.Close(UserId, Now);

        var act = () => entry.Reverse(2, EntryDate, closedPeriod, accounts, UserId, Now);

        act.Should().Throw<PeriodNotOpenException>();
    }
}

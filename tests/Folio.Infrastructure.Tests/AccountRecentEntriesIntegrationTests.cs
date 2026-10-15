using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;
using Folio.Infrastructure.Persistence.Repositories;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using Xunit;

namespace Folio.Infrastructure.Tests;

/// <summary>
/// Proves <see cref="AccountRepository.GetRecentEntriesAsync"/> — the "últimos
/// asientos" list in the detail panel — returns newest-first, shows only the
/// viewed account's own side of each entry (not the other leg), respects the
/// requested limit, and never surfaces a Draft or Reversed entry.
/// </summary>
public sealed class AccountRecentEntriesIntegrationTests : IDisposable
{
    private readonly SqliteInMemoryContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static (Account Caja, Account Ventas, FiscalYear FiscalYear, List<Period> Periods, Dictionary<Guid, Account> AccountsById) BuildFixture()
    {
        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var ventas = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        var periods = fiscalYear.GeneratePeriods().ToList();
        var accountsById = new Dictionary<Guid, Account> { [caja.Id] = caja, [ventas.Id] = ventas };
        return (caja, ventas, fiscalYear, periods, accountsById);
    }

    [Fact]
    public async Task GetRecentEntriesAsync_ReturnsNewestFirstAndOnlyThisAccountsSide()
    {
        var (caja, ventas, fiscalYear, periods, accountsById) = BuildFixture();
        var contextFactory = new TestDbContextFactory(_factory);
        var accountRepository = new AccountRepository(contextFactory);

        var oldest = new JournalEntry(new DateOnly(2026, 1, 5), "Depósito a bancos", null, Guid.NewGuid(), DateTime.UtcNow);
        oldest.AddLine(JournalLine.CreateCredit(caja.Id, new Money(500m, Currency.CRC)));
        oldest.AddLine(JournalLine.CreateDebit(ventas.Id, new Money(500m, Currency.CRC)));
        oldest.Post(1, periods.Single(p => p.Month == 1), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        var middle = new JournalEntry(new DateOnly(2026, 2, 10), "Compra de insumos", null, Guid.NewGuid(), DateTime.UtcNow);
        middle.AddLine(JournalLine.CreateCredit(caja.Id, new Money(86450m, Currency.CRC)));
        middle.AddLine(JournalLine.CreateDebit(ventas.Id, new Money(86450m, Currency.CRC)));
        middle.Post(2, periods.Single(p => p.Month == 2), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        var newest = new JournalEntry(new DateOnly(2026, 3, 15), "Ventas del día", null, Guid.NewGuid(), DateTime.UtcNow);
        newest.AddLine(JournalLine.CreateDebit(caja.Id, new Money(245300m, Currency.CRC)));
        newest.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(245300m, Currency.CRC)));
        newest.Post(3, periods.Single(p => p.Month == 3), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(caja, ventas);
            writeContext.FiscalYears.Add(fiscalYear);
            writeContext.Periods.AddRange(periods);
            writeContext.JournalEntries.AddRange(oldest, middle, newest);
            await writeContext.SaveChangesAsync();
        }

        var result = await accountRepository.GetRecentEntriesAsync([caja.Id], take: 10);

        result.Should().HaveCount(3);
        result[0].EntryId.Should().Be(newest.Id);
        result[0].Description.Should().Be("Ventas del día");
        result[0].TotalDebit.Should().Be(245300m);
        result[0].TotalCredit.Should().Be(0m, "this entry's OTHER leg (Ventas) must never show up as Caja's credit");
        result[1].EntryId.Should().Be(middle.Id);
        result[2].EntryId.Should().Be(oldest.Id);
        result[2].TotalCredit.Should().Be(500m);
    }

    [Fact]
    public async Task GetRecentEntriesAsync_RespectsTakeLimit()
    {
        var (caja, ventas, fiscalYear, periods, accountsById) = BuildFixture();
        var contextFactory = new TestDbContextFactory(_factory);
        var accountRepository = new AccountRepository(contextFactory);

        var entries = new List<JournalEntry>();
        for (var i = 1; i <= 5; i++)
        {
            var entry = new JournalEntry(new DateOnly(2026, 1, i), $"Venta {i}", null, Guid.NewGuid(), DateTime.UtcNow);
            entry.AddLine(JournalLine.CreateDebit(caja.Id, new Money(10m * i, Currency.CRC)));
            entry.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(10m * i, Currency.CRC)));
            entry.Post(i, periods.Single(p => p.Month == 1), accountsById, Guid.NewGuid(), DateTime.UtcNow);
            entries.Add(entry);
        }

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(caja, ventas);
            writeContext.FiscalYears.Add(fiscalYear);
            writeContext.Periods.AddRange(periods);
            writeContext.JournalEntries.AddRange(entries);
            await writeContext.SaveChangesAsync();
        }

        var result = await accountRepository.GetRecentEntriesAsync([caja.Id], take: 2);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentEntriesAsync_IgnoresDraftAndReversedEntries()
    {
        var (caja, ventas, fiscalYear, periods, accountsById) = BuildFixture();
        var contextFactory = new TestDbContextFactory(_factory);
        var accountRepository = new AccountRepository(contextFactory);

        // Draft: never posted, must never appear.
        var draft = new JournalEntry(new DateOnly(2026, 1, 20), "Borrador sin contabilizar", null, Guid.NewGuid(), DateTime.UtcNow);
        draft.AddLine(JournalLine.CreateDebit(caja.Id, new Money(999m, Currency.CRC)));
        draft.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(999m, Currency.CRC)));

        // Posted then reversed: the ORIGINAL entry's Status becomes Reversed and
        // must disappear too — only the (also-posted) reversal entry should show.
        var original = new JournalEntry(new DateOnly(2026, 1, 10), "Venta a revertir", null, Guid.NewGuid(), DateTime.UtcNow);
        original.AddLine(JournalLine.CreateDebit(caja.Id, new Money(300m, Currency.CRC)));
        original.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(300m, Currency.CRC)));
        original.Post(1, periods.Single(p => p.Month == 1), accountsById, Guid.NewGuid(), DateTime.UtcNow);
        var reversal = original.Reverse(2, new DateOnly(2026, 1, 11), periods.Single(p => p.Month == 1), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(caja, ventas);
            writeContext.FiscalYears.Add(fiscalYear);
            writeContext.Periods.AddRange(periods);
            writeContext.JournalEntries.AddRange(draft, original, reversal);
            await writeContext.SaveChangesAsync();
        }

        var result = await accountRepository.GetRecentEntriesAsync([caja.Id], take: 10);

        result.Should().ContainSingle();
        result[0].EntryId.Should().Be(reversal.Id);
    }
}

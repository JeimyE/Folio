using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Folio.Infrastructure.Tests;

public sealed class FolioDbContextTests : IDisposable
{
    private readonly SqliteInMemoryContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Migrate_AppliesAllMigrationsWithoutError()
    {
        using var context = _factory.CreateContext();

        var canConnect = await context.Database.CanConnectAsync();

        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task Account_RoundTrip_PreservesAccountCodeAndEnums()
    {
        var account = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.Add(account);
            await writeContext.SaveChangesAsync();
        }

        using var readContext = _factory.CreateContext();
        var reloaded = await readContext.Accounts.SingleAsync(a => a.Id == account.Id);

        reloaded.Code.Should().Be(new AccountCode("1.1.01"));
        reloaded.Type.Should().Be(AccountType.Asset);
        reloaded.NormalBalance.Should().Be(NormalBalance.Debit);
        reloaded.IsPostable.Should().BeTrue();
        reloaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task JournalEntry_RoundTrip_PreservesLinesViaEncapsulatedCollection()
    {
        var cash = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var period = fiscalYear.GeneratePeriods().Single(p => p.Month == 3);

        var entry = new JournalEntry(new DateOnly(2026, 3, 15), "Venta de café", "F-001", Guid.NewGuid(), DateTime.UtcNow);
        entry.AddLine(JournalLine.CreateDebit(cash.Id, new Money(56_500m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(sales.Id, new Money(56_500m, Currency.CRC)));
        entry.Post(1, period, new Dictionary<Guid, Account> { [cash.Id] = cash, [sales.Id] = sales }, Guid.NewGuid(), DateTime.UtcNow);

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(cash, sales);
            writeContext.FiscalYears.Add(fiscalYear);
            writeContext.Periods.Add(period);
            writeContext.JournalEntries.Add(entry);
            await writeContext.SaveChangesAsync();
        }

        using var readContext = _factory.CreateContext();
        var reloaded = await readContext.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == entry.Id);

        reloaded.Status.Should().Be(JournalEntryStatus.Posted);
        reloaded.Lines.Should().HaveCount(2);
        reloaded.Lines.Single(l => l.AccountId == cash.Id).Debit.Should().Be(56_500m);
        reloaded.Lines.Single(l => l.AccountId == sales.Id).Credit.Should().Be(56_500m);
    }

    [Fact]
    public async Task JournalLines_Debit_SqlSumIsExact()
    {
        // The whole point of storing money as integer cents (ADR-0003): SQLite can
        // SUM the column directly, and the result is still exact, not a float blur.
        var cash = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var period = fiscalYear.GeneratePeriods().Single(p => p.Month == 3);
        var accounts = new Dictionary<Guid, Account> { [cash.Id] = cash, [sales.Id] = sales };

        var amounts = new[] { 100.10m, 200.20m, 300.30m };
        var entries = new List<JournalEntry>();
        var number = 1;
        foreach (var amount in amounts)
        {
            var entry = new JournalEntry(new DateOnly(2026, 3, 10), "Venta", null, Guid.NewGuid(), DateTime.UtcNow);
            entry.AddLine(JournalLine.CreateDebit(cash.Id, new Money(amount, Currency.CRC)));
            entry.AddLine(JournalLine.CreateCredit(sales.Id, new Money(amount, Currency.CRC)));
            entry.Post(number++, period, accounts, Guid.NewGuid(), DateTime.UtcNow);
            entries.Add(entry);
        }

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(cash, sales);
            writeContext.FiscalYears.Add(fiscalYear);
            writeContext.Periods.Add(period);
            writeContext.JournalEntries.AddRange(entries);
            await writeContext.SaveChangesAsync();
        }

        using var readContext = _factory.CreateContext();
        var sum = await readContext.JournalEntries
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == cash.Id)
            .SumAsync(l => l.Debit);

        sum.Should().Be(600.60m);
    }

    [Fact]
    public async Task ExchangeRate_RoundTrip_PreservesFullRatePrecision()
    {
        var rate = new ExchangeRate(new DateOnly(2026, 3, 1), buy: 515.365m, sell: 517.77m, "BCCR");

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.ExchangeRates.Add(rate);
            await writeContext.SaveChangesAsync();
        }

        using var readContext = _factory.CreateContext();
        var reloaded = await readContext.ExchangeRates.SingleAsync(r => r.Id == rate.Id);

        reloaded.Buy.Should().Be(515.365m);
        reloaded.Sell.Should().Be(517.77m);
    }
}

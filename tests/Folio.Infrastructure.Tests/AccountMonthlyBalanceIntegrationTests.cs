using Folio.Application.ChartOfAccounts;
using Folio.Domain.Common;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;
using Folio.Infrastructure.Persistence.Repositories;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Folio.Infrastructure.Tests;

/// <summary>
/// Proves <see cref="AccountRepository.GetMonthlyMovementTotalsAsync"/> — the raw
/// per-account-per-month SQL aggregation the "saldo por mes" sparkline is built
/// from — groups by real calendar month via a real SQL <c>GROUP BY</c>, not an
/// in-memory operation, and that months with no activity are simply absent from
/// the result (the caller, <c>ChartOfAccountsService</c>, is the one that carries a
/// running balance forward across those gaps — see
/// <see cref="ChartOfAccountsBalanceIntegrationTests"/> for that behavior).
/// </summary>
public sealed class AccountMonthlyBalanceIntegrationTests : IDisposable
{
    private readonly SqliteInMemoryContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetMonthlyMovementTotalsAsync_EntriesInNonConsecutiveMonths_GroupsByCalendarMonthAndOmitsMonthsWithNoActivity()
    {
        var contextFactory = new TestDbContextFactory(_factory);
        var accountRepository = new AccountRepository(contextFactory);

        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var ventas = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);

        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var periods = fiscalYear.GeneratePeriods().ToList();
        var accountsById = new Dictionary<Guid, Account> { [caja.Id] = caja, [ventas.Id] = ventas };

        // January: one sale. February: nothing touches Caja. March: two entries,
        // one debit and one credit, to prove both sides of the same month are summed.
        var january = new JournalEntry(new DateOnly(2026, 1, 10), "Venta de enero", null, Guid.NewGuid(), DateTime.UtcNow);
        january.AddLine(JournalLine.CreateDebit(caja.Id, new Money(100m, Currency.CRC)));
        january.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(100m, Currency.CRC)));
        january.Post(1, periods.Single(p => p.Month == 1), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        var marchSale = new JournalEntry(new DateOnly(2026, 3, 5), "Venta de marzo", null, Guid.NewGuid(), DateTime.UtcNow);
        marchSale.AddLine(JournalLine.CreateDebit(caja.Id, new Money(50m, Currency.CRC)));
        marchSale.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(50m, Currency.CRC)));
        marchSale.Post(2, periods.Single(p => p.Month == 3), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        var marchRefund = new JournalEntry(new DateOnly(2026, 3, 20), "Devolución de marzo", null, Guid.NewGuid(), DateTime.UtcNow);
        marchRefund.AddLine(JournalLine.CreateCredit(caja.Id, new Money(20m, Currency.CRC)));
        marchRefund.AddLine(JournalLine.CreateDebit(ventas.Id, new Money(20m, Currency.CRC)));
        marchRefund.Post(3, periods.Single(p => p.Month == 3), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(caja, ventas);
            writeContext.FiscalYears.Add(fiscalYear);
            writeContext.Periods.AddRange(periods);
            writeContext.JournalEntries.AddRange(january, marchSale, marchRefund);
            await writeContext.SaveChangesAsync();
        }

        var result = await accountRepository.GetMonthlyMovementTotalsAsync([caja.Id], through: new DateOnly(2026, 6, 30));

        result.Should().HaveCount(2, "February has no Caja movement and must be absent, not zero");
        result[0].Year.Should().Be(2026);
        result[0].Month.Should().Be(1);
        result[0].TotalDebit.Should().Be(100m);
        result[0].TotalCredit.Should().Be(0m);
        result[1].Month.Should().Be(3);
        result[1].TotalDebit.Should().Be(50m);
        result[1].TotalCredit.Should().Be(20m);
    }

    [Fact]
    public async Task GetMonthlyMovementTotalsAsync_ThroughBeforeLaterEntries_ExcludesThem()
    {
        var contextFactory = new TestDbContextFactory(_factory);
        var accountRepository = new AccountRepository(contextFactory);

        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var ventas = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);

        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var periods = fiscalYear.GeneratePeriods().ToList();
        var accountsById = new Dictionary<Guid, Account> { [caja.Id] = caja, [ventas.Id] = ventas };

        var january = new JournalEntry(new DateOnly(2026, 1, 10), "Venta de enero", null, Guid.NewGuid(), DateTime.UtcNow);
        january.AddLine(JournalLine.CreateDebit(caja.Id, new Money(100m, Currency.CRC)));
        january.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(100m, Currency.CRC)));
        january.Post(1, periods.Single(p => p.Month == 1), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        var march = new JournalEntry(new DateOnly(2026, 3, 5), "Venta de marzo", null, Guid.NewGuid(), DateTime.UtcNow);
        march.AddLine(JournalLine.CreateDebit(caja.Id, new Money(50m, Currency.CRC)));
        march.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(50m, Currency.CRC)));
        march.Post(2, periods.Single(p => p.Month == 3), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(caja, ventas);
            writeContext.FiscalYears.Add(fiscalYear);
            writeContext.Periods.AddRange(periods);
            writeContext.JournalEntries.AddRange(january, march);
            await writeContext.SaveChangesAsync();
        }

        var result = await accountRepository.GetMonthlyMovementTotalsAsync([caja.Id], through: new DateOnly(2026, 1, 31));

        result.Should().ContainSingle();
        result[0].Month.Should().Be(1);
    }

    /// <summary>
    /// End-to-end through the real <see cref="ChartOfAccountsService"/> (not just the
    /// repository): a movement posted BEFORE the six-month window — with nothing at
    /// all posted in the first couple of months of the window itself — must already
    /// show up in the window's first reported month, not zero. This is the exact
    /// scenario Jeimy asked to see covered: an account's history didn't start at the
    /// edge of whatever "last 6 months" happens to mean today.
    /// </summary>
    [Fact]
    public async Task GetDetailAsync_MovementBeforeTheSixMonthWindow_IsAlreadyReflectedInTheFirstReportedMonth()
    {
        var contextFactory = new TestDbContextFactory(_factory);
        var accountRepository = new AccountRepository(contextFactory);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc)); // window: Jan..Jun 2026
        var service = new ChartOfAccountsService(accountRepository, clock);

        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var ventas = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);

        // Two separate fiscal years — Periods are uniquely keyed by (FiscalYearId,
        // Month), so a single fiscal year can't span more than 12 calendar months
        // without repeating a month number.
        var fiscalYear2025 = new FiscalYear(2025, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        var periods2025 = fiscalYear2025.GeneratePeriods().ToList();
        var fiscalYear2026 = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var periods2026 = fiscalYear2026.GeneratePeriods().ToList();
        var accountsById = new Dictionary<Guid, Account> { [caja.Id] = caja, [ventas.Id] = ventas };

        // Posted in December 2025 — entirely before the Jan-Jun 2026 window.
        var openingDeposit = new JournalEntry(new DateOnly(2025, 12, 10), "Capital inicial", null, Guid.NewGuid(), DateTime.UtcNow);
        openingDeposit.AddLine(JournalLine.CreateDebit(caja.Id, new Money(1000m, Currency.CRC)));
        openingDeposit.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(1000m, Currency.CRC)));
        openingDeposit.Post(1, periods2025.Single(p => p.Month == 12), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        // Nothing at all happens in January or February 2026 for Caja.

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(caja, ventas);
            writeContext.FiscalYears.AddRange(fiscalYear2025, fiscalYear2026);
            writeContext.Periods.AddRange(periods2025.Concat(periods2026));
            writeContext.JournalEntries.Add(openingDeposit);
            await writeContext.SaveChangesAsync();
        }

        var detail = await service.GetDetailAsync(caja.Id);

        detail!.MonthlyBalances.Should().HaveCount(6);
        detail.MonthlyBalances[0].Year.Should().Be(2026);
        detail.MonthlyBalances[0].Month.Should().Be(1, "the window is Jan..Jun 2026");
        detail.MonthlyBalances[0].Balance.Should().Be(1000m, "the December deposit already happened before the window started");
        detail.MonthlyBalances[1].Balance.Should().Be(1000m, "February has no activity of its own, so it carries January's balance forward");
        detail.MonthlyBalances[^1].Month.Should().Be(6, "the window always ends at the current month");
    }

    /// <summary>
    /// A credit-normal GROUP account (Ingresos) aggregates the monthly closing
    /// balance of all its descendant leaves, signed per its own normal balance (a
    /// sale increases it), and still carries a pre-window movement forward.
    /// </summary>
    [Fact]
    public async Task GetDetailAsync_CreditNormalGroupAccount_AggregatesLeavesAsRunningCreditPositiveBalance()
    {
        var contextFactory = new TestDbContextFactory(_factory);
        var accountRepository = new AccountRepository(contextFactory);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc)); // window: Jan..Jun 2026
        var service = new ChartOfAccountsService(accountRepository, clock);

        // 4 Ingresos (group) -> 4.1 Ventas, 4.2 Servicios (leaves)
        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var ingresos = new Account(new AccountCode("4"), "Ingresos", AccountType.Revenue, isPostable: false);
        var ventas = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true, ingresos.Id);
        var servicios = new Account(new AccountCode("4.2"), "Servicios", AccountType.Revenue, isPostable: true, ingresos.Id);
        ingresos.AddChild();
        ingresos.AddChild();

        var fiscalYear2025 = new FiscalYear(2025, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        var periods2025 = fiscalYear2025.GeneratePeriods().ToList();
        var fiscalYear2026 = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var periods2026 = fiscalYear2026.GeneratePeriods().ToList();
        var accountsById = new Dictionary<Guid, Account> { [caja.Id] = caja, [ventas.Id] = ventas, [servicios.Id] = servicios };

        // November 2025 (before the window): Ventas 300.
        var novemberSale = new JournalEntry(new DateOnly(2025, 11, 20), "Venta de noviembre", null, Guid.NewGuid(), DateTime.UtcNow);
        novemberSale.AddLine(JournalLine.CreateDebit(caja.Id, new Money(300m, Currency.CRC)));
        novemberSale.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(300m, Currency.CRC)));
        novemberSale.Post(1, periods2025.Single(p => p.Month == 11), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        // March 2026: Servicios 150 — a different leaf of the same group.
        var marchService = new JournalEntry(new DateOnly(2026, 3, 8), "Servicio de marzo", null, Guid.NewGuid(), DateTime.UtcNow);
        marchService.AddLine(JournalLine.CreateDebit(caja.Id, new Money(150m, Currency.CRC)));
        marchService.AddLine(JournalLine.CreateCredit(servicios.Id, new Money(150m, Currency.CRC)));
        marchService.Post(2, periods2026.Single(p => p.Month == 3), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        // May 2026: a 50 refund debited to Ventas reduces the credit-normal balance.
        var mayRefund = new JournalEntry(new DateOnly(2026, 5, 2), "Devolución de mayo", null, Guid.NewGuid(), DateTime.UtcNow);
        mayRefund.AddLine(JournalLine.CreateDebit(ventas.Id, new Money(50m, Currency.CRC)));
        mayRefund.AddLine(JournalLine.CreateCredit(caja.Id, new Money(50m, Currency.CRC)));
        mayRefund.Post(3, periods2026.Single(p => p.Month == 5), accountsById, Guid.NewGuid(), DateTime.UtcNow);

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(caja, ingresos, ventas, servicios);
            writeContext.FiscalYears.AddRange(fiscalYear2025, fiscalYear2026);
            writeContext.Periods.AddRange(periods2025.Concat(periods2026));
            writeContext.JournalEntries.AddRange(novemberSale, marchService, mayRefund);
            await writeContext.SaveChangesAsync();
        }

        var detail = await service.GetDetailAsync(ingresos.Id);

        detail!.MonthlyBalances.Select(m => m.Balance).Should().Equal(
            [300m, 300m, 450m, 450m, 400m, 400m],
            "Jan-Feb carry November's 300, March adds Servicios, May's refund reduces it, June carries May");
    }
}

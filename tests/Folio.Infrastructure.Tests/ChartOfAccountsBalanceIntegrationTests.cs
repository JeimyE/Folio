using Folio.Application.ChartOfAccounts;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;
using Folio.Infrastructure.Persistence.Repositories;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using Xunit;

namespace Folio.Infrastructure.Tests;

/// <summary>
/// Proves the balance shown in the chart-of-accounts detail panel is computed by a
/// real SQL <c>SUM</c> over the integer-cents columns (ADR-0003), end to end through
/// <see cref="ChartOfAccountsService"/> and the real <see cref="AccountRepository"/>
/// — not by loading every line into memory and summing in C#.
/// </summary>
public sealed class ChartOfAccountsBalanceIntegrationTests : IDisposable
{
    private readonly SqliteInMemoryContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetDetailAsync_LeafAndGroupAccounts_ComputeBalanceViaSqlSum()
    {
        var contextFactory = new TestDbContextFactory(_factory);
        var accountRepository = new AccountRepository(contextFactory);
        var service = new ChartOfAccountsService(accountRepository);

        // 1 (group) -> 1.1 (group) -> 1.1.01 Caja, 1.1.02 Bancos (both leaves)
        // 4 (group) -> 4.1 Ventas (leaf)
        var activo = new Account(new AccountCode("1"), "Activo", AccountType.Asset, isPostable: false);
        var activoCorriente = new Account(new AccountCode("1.1"), "Activo corriente", AccountType.Asset, isPostable: false, activo.Id);
        activo.AddChild();
        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true, activoCorriente.Id);
        var bancos = new Account(new AccountCode("1.1.02"), "Bancos colones", AccountType.Asset, isPostable: true, activoCorriente.Id);
        activoCorriente.AddChild();
        activoCorriente.AddChild();

        var ingresos = new Account(new AccountCode("4"), "Ingresos", AccountType.Revenue, isPostable: false);
        var ventas = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true, ingresos.Id);
        ingresos.AddChild();

        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var period = fiscalYear.GeneratePeriods().Single(p => p.Month == 3);

        var accountsById = new Dictionary<Guid, Account>
        {
            [caja.Id] = caja, [bancos.Id] = bancos, [ventas.Id] = ventas,
        };

        // Sale: Debit Caja 100.10, Credit Ventas 100.10.
        var sale = new JournalEntry(new DateOnly(2026, 3, 5), "Venta de café", null, Guid.NewGuid(), DateTime.UtcNow);
        sale.AddLine(JournalLine.CreateDebit(caja.Id, new Money(100.10m, Currency.CRC)));
        sale.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(100.10m, Currency.CRC)));
        sale.Post(1, period, accountsById, Guid.NewGuid(), DateTime.UtcNow);

        // Another sale, collected to the bank: Debit Bancos 200.20, Credit Ventas 200.20.
        var bankSale = new JournalEntry(new DateOnly(2026, 3, 6), "Venta cobrada por transferencia", null, Guid.NewGuid(), DateTime.UtcNow);
        bankSale.AddLine(JournalLine.CreateDebit(bancos.Id, new Money(200.20m, Currency.CRC)));
        bankSale.AddLine(JournalLine.CreateCredit(ventas.Id, new Money(200.20m, Currency.CRC)));
        bankSale.Post(2, period, accountsById, Guid.NewGuid(), DateTime.UtcNow);

        // Internal transfer within the "1.1" subtree: Debit Caja 50.00, Credit Bancos 50.00.
        // Should net to zero for the "1.1" group balance — proving the aggregation
        // doesn't just add every line, it nets debits against credits correctly.
        var transfer = new JournalEntry(new DateOnly(2026, 3, 7), "Depósito de caja a banco", null, Guid.NewGuid(), DateTime.UtcNow);
        transfer.AddLine(JournalLine.CreateDebit(caja.Id, new Money(50.00m, Currency.CRC)));
        transfer.AddLine(JournalLine.CreateCredit(bancos.Id, new Money(50.00m, Currency.CRC)));
        transfer.Post(3, period, accountsById, Guid.NewGuid(), DateTime.UtcNow);

        using (var writeContext = _factory.CreateContext())
        {
            writeContext.Accounts.AddRange(activo, activoCorriente, caja, bancos, ingresos, ventas);
            writeContext.FiscalYears.Add(fiscalYear);
            writeContext.Periods.Add(period);
            writeContext.JournalEntries.AddRange(sale, bankSale, transfer);
            await writeContext.SaveChangesAsync();
        }

        var cajaDetail = await service.GetDetailAsync(caja.Id);
        var bancosDetail = await service.GetDetailAsync(bancos.Id);
        var ventasDetail = await service.GetDetailAsync(ventas.Id);
        var activoCorrienteDetail = await service.GetDetailAsync(activoCorriente.Id);
        var ingresosDetail = await service.GetDetailAsync(ingresos.Id);

        cajaDetail!.Balance.Should().Be(150.10m); // 100.10 + 50.00
        bancosDetail!.Balance.Should().Be(150.20m); // 200.20 - 50.00
        activoCorrienteDetail!.Balance.Should().Be(300.30m); // sum of its leaf children, transfer nets to zero
        ventasDetail!.Balance.Should().Be(300.30m); // Credit-normal: 100.10 + 200.20
        ingresosDetail!.Balance.Should().Be(300.30m); // group aggregates its one leaf child
    }
}

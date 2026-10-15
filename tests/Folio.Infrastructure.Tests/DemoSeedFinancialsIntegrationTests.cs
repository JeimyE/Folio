using Folio.Application.ChartOfAccounts;
using Folio.Domain.Common;
using Folio.Infrastructure.Persistence.Repositories;
using Folio.Infrastructure.Security;
using Folio.Infrastructure.Seed;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Folio.Infrastructure.Tests;

/// <summary>
/// Proves the demo company's numbers are believable, not just balanced: seeds a real
/// SQLite database through <see cref="FolioSeeder"/> and reads every root account's
/// balance back through the real <see cref="ChartOfAccountsService"/> — the same
/// figures the chart-of-accounts screen shows.
/// </summary>
public sealed class DemoSeedFinancialsIntegrationTests : IDisposable
{
    private static readonly DateTime FixedNow = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteInMemoryContextFactory _factory = new();
    private readonly ITestOutputHelper _output;

    public DemoSeedFinancialsIntegrationTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SeedAsync_DemoCompany_HasPositiveProfitAndMoreAssetsThanLiabilities()
    {
        var totals = await SeedAndReadRootBalancesAsync();
        var profit = totals["4"] - totals["5"] - totals["6"];

        _output.WriteLine(
            $"Activo {totals["1"]:N2} | Pasivo {totals["2"]:N2} | Patrimonio {totals["3"]:N2} | " +
            $"Ingresos {totals["4"]:N2} | Costos {totals["5"]:N2} | Gastos {totals["6"]:N2} | Utilidad {profit:N2}");

        profit.Should().BePositive("a café with sales every month should not post a loss for the year");
        totals["1"].Should().BeGreaterThan(totals["2"], "assets must exceed liabilities for a solvent company");
    }

    [Fact]
    public async Task SeedAsync_DemoCompany_SatisfiesTheAccountingEquation()
    {
        var totals = await SeedAndReadRootBalancesAsync();
        var profit = totals["4"] - totals["5"] - totals["6"];

        totals["1"].Should().Be(totals["2"] + totals["3"] + profit);
    }

    private async Task<Dictionary<string, decimal>> SeedAndReadRootBalancesAsync()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        using (var seedContext = _factory.CreateContext())
        {
            await FolioSeeder.SeedAsync(seedContext, clock, new Pbkdf2PasswordHasher());
        }

        var service = new ChartOfAccountsService(new AccountRepository(new TestDbContextFactory(_factory)), clock);
        var roots = await service.GetTreeAsync();
        return roots.ToDictionary(r => r.Code, r => r.Balance);
    }
}

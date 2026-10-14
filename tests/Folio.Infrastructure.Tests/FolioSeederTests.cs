using Folio.Application.Abstractions;
using Folio.Domain.Common;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Folio.Infrastructure.Security;
using Folio.Infrastructure.Seed;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Folio.Infrastructure.Tests;

public sealed class FolioSeederTests : IDisposable
{
    // Fixed "now" so the trailing-12-month window, and every assertion about it, is
    // deterministic regardless of when the test suite actually runs.
    private static readonly DateTime FixedNow = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteInMemoryContextFactory _factory = new();
    private readonly IClock _clock;
    private readonly Pbkdf2PasswordHasher _hasher = new();

    public FolioSeederTests()
    {
        _clock = Substitute.For<IClock>();
        _clock.UtcNow.Returns(FixedNow);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SeedAsync_EmptyDatabase_CreatesExpectedCompanyAndUsers()
    {
        using var context = _factory.CreateContext();

        await FolioSeeder.SeedAsync(context, _clock, _hasher);

        var company = await context.CompanySettings.SingleAsync();
        company.Name.Should().Be("Sleepy Sloth Café & Bakery S.A.");

        var usernames = await context.Users.Select(u => u.Username).ToListAsync();
        usernames.Should().BeEquivalentTo("admin", "contador", "consulta");
    }

    [Fact]
    public async Task SeedAsync_Passwords_AreNeverStoredAsPlainText()
    {
        using var context = _factory.CreateContext();

        await FolioSeeder.SeedAsync(context, _clock, _hasher);

        var users = await context.Users.ToListAsync();
        users.Should().OnlyContain(u => u.PasswordHash != "Folio2026!" && u.Salt != "Folio2026!");
        users.Should().OnlyContain(u => _hasher.Verify("Folio2026!", u.PasswordHash, u.Salt));
    }

    [Fact]
    public async Task SeedAsync_ClosesEveryPeriodExceptTheCurrentOne()
    {
        using var context = _factory.CreateContext();

        await FolioSeeder.SeedAsync(context, _clock, _hasher);

        var periods = await context.Periods.OrderBy(p => p.StartDate).ToListAsync();
        periods.Should().HaveCount(12);
        periods.Take(11).Should().OnlyContain(p => p.Status == PeriodStatus.Closed);
        periods.Last().Status.Should().Be(PeriodStatus.Open);
        periods.Last().Month.Should().Be(FixedNow.Month);
    }

    [Fact]
    public async Task SeedAsync_EveryPostedEntry_BalancesInFunctionalCurrency()
    {
        using var context = _factory.CreateContext();

        await FolioSeeder.SeedAsync(context, _clock, _hasher);

        var entries = await context.JournalEntries.Include(e => e.Lines).ToListAsync();
        entries.Should().NotBeEmpty();
        entries.Should().OnlyContain(e => e.Status == JournalEntryStatus.Posted);
        entries.Should().OnlyContain(e => e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task SeedAsync_UsesFixedWindowRelativeToClock_NotWallClock()
    {
        using var context = _factory.CreateContext();

        await FolioSeeder.SeedAsync(context, _clock, _hasher);

        var rates = await context.ExchangeRates.OrderBy(r => r.Date).ToListAsync();
        rates.First().Date.Should().Be(new DateOnly(2025, 7, 1));
        rates.Last().Date.Should().Be(DateOnly.FromDateTime(FixedNow));
    }

    [Fact]
    public async Task InitializeAsync_FreshDatabase_WritesExactlyOneAuditRow_NotOnePerSeededEntity()
    {
        // FolioDbInitializer, not FolioSeeder directly: the "no audit flood" guarantee
        // comes from the bootstrap context it builds having no interceptor attached at
        // all, plus the one summary row it writes itself afterward.
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"folio-seed-test-{Guid.NewGuid():N}.db");
        try
        {
            await FolioDbInitializer.InitializeAsync($"Data Source={tempDbPath}");

            var optionsBuilder = new DbContextOptionsBuilder<FolioDbContext>()
                .UseSqlite($"Data Source={tempDbPath}");
            using var verifyContext = new FolioDbContext(optionsBuilder.Options);

            var auditRows = await verifyContext.AuditLogs.ToListAsync();
            auditRows.Should().HaveCount(1);
            auditRows[0].Action.Should().Be("Seed");
            auditRows[0].NewValuesJson.Should().Contain("Sleepy Sloth");

            (await verifyContext.Accounts.CountAsync()).Should().BeGreaterThan(0);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(tempDbPath);
        }
    }
}

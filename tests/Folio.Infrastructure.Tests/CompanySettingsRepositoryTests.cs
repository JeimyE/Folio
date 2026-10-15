using Folio.Domain.Entities;
using Folio.Infrastructure.Persistence.Repositories;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using Xunit;

namespace Folio.Infrastructure.Tests;

public sealed class CompanySettingsRepositoryTests : IDisposable
{
    private readonly SqliteInMemoryContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// The test context turns EF Core's "First without OrderBy and filter" warning
    /// into an exception, so this also proves GetAsync no longer triggers it.
    /// </summary>
    [Fact]
    public async Task GetAsync_SingleCompanyRow_ReturnsItWithoutAnUnorderedFirstQuery()
    {
        using (var writeContext = _factory.CreateContext())
        {
            writeContext.CompanySettings.Add(new CompanySettings("Sleepy Sloth Café & Bakery S.A.", "3-101-987654", fiscalYearStartMonth: 1, vatRate: 0.13m));
            await writeContext.SaveChangesAsync();
        }

        var repository = new CompanySettingsRepository(new TestDbContextFactory(_factory));

        var settings = await repository.GetAsync();

        settings!.Name.Should().Be("Sleepy Sloth Café & Bakery S.A.");
    }

    [Fact]
    public async Task GetAsync_NoCompanyRow_ReturnsNull()
    {
        var repository = new CompanySettingsRepository(new TestDbContextFactory(_factory));

        var settings = await repository.GetAsync();

        settings.Should().BeNull();
    }
}

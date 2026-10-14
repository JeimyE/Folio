using Folio.Application.Authentication;
using Folio.Domain.Common;
using Folio.Infrastructure.Auditing;
using Folio.Infrastructure.Persistence;
using Folio.Infrastructure.Persistence.Repositories;
using Folio.Infrastructure.Security;
using Folio.Infrastructure.Seed;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Folio.Infrastructure.Tests;

/// <summary>
/// Exercises the real login path end to end — the real seeder's password hashing,
/// the real <see cref="UserRepository"/>, and the real <see cref="AuthenticationService"/>
/// — against a freshly seeded SQLite database, to catch exactly the kind of bug a
/// unit test with mocked dependencies can't: a wiring problem between layers rather
/// than a logic error in any single one of them.
/// </summary>
public sealed class AuthenticationServiceIntegrationTests : IDisposable
{
    private readonly SqliteInMemoryContextFactory _factory = new();
    private readonly Pbkdf2PasswordHasher _hasher = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task LoginAsync_SeededAdminWithDocumentedPassword_Succeeds()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTime.UtcNow);

        using var seedContext = _factory.CreateContext();
        await FolioSeeder.SeedAsync(seedContext, clock, _hasher);

        var contextFactory = new TestDbContextFactory(_factory);
        var userRepository = new UserRepository(contextFactory);
        var currentUserSession = new CurrentUserSession();
        var authenticationService = new AuthenticationService(userRepository, _hasher, clock, currentUserSession);

        var result = await authenticationService.LoginAsync("admin", "Folio2026!");

        result.Succeeded.Should().BeTrue();
        currentUserSession.UserId.Should().Be(result.UserId);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("contador")]
    [InlineData("consulta")]
    public async Task LoginAsync_EverySeededUserWithDocumentedPassword_Succeeds(string username)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTime.UtcNow);

        using var seedContext = _factory.CreateContext();
        await FolioSeeder.SeedAsync(seedContext, clock, _hasher);

        var contextFactory = new TestDbContextFactory(_factory);
        var userRepository = new UserRepository(contextFactory);
        var authenticationService = new AuthenticationService(userRepository, _hasher, clock, new CurrentUserSession());

        var result = await authenticationService.LoginAsync(username, "Folio2026!");

        result.Succeeded.Should().BeTrue();
    }

    private sealed class TestDbContextFactory(SqliteInMemoryContextFactory factory) : IDbContextFactory<FolioDbContext>
    {
        public FolioDbContext CreateDbContext() => factory.CreateContext();

        public Task<FolioDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(factory.CreateContext());
    }
}

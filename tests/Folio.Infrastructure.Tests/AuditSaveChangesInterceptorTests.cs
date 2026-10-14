using Folio.Application.Abstractions;
using Folio.Domain.Common;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;
using Folio.Infrastructure.Auditing;
using Folio.Infrastructure.Persistence;
using Folio.Infrastructure.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Folio.Infrastructure.Tests;

public sealed class AuditSaveChangesInterceptorTests : IDisposable
{
    private static readonly DateTime FixedNow = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
    private readonly SqliteInMemoryContextFactory _factory = new();
    private readonly Guid _currentUserId = Guid.NewGuid();

    public AuditSaveChangesInterceptorTests()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(_currentUserId);
        Interceptor = new AuditSaveChangesInterceptor(clock, currentUser);
    }

    private AuditSaveChangesInterceptor Interceptor { get; }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SaveChanges_AddingAccount_WritesCreateAuditRow()
    {
        var account = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);

        using var context = _factory.CreateContext(Interceptor);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        var auditRow = await context.AuditLogs.SingleAsync(a => a.Entity == nameof(Account) && a.EntityId == account.Id.ToString());
        auditRow.Action.Should().Be("Create");
        auditRow.UserId.Should().Be(_currentUserId);
        auditRow.TimestampUtc.Should().Be(FixedNow);
        auditRow.NewValuesJson.Should().Contain("Caja");
    }

    [Fact]
    public async Task SaveChanges_PostingAJournalEntry_LabelsActionAsPost()
    {
        var cash = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var sales = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var period = fiscalYear.GeneratePeriods().Single(p => p.Month == 3);
        var entry = new JournalEntry(new DateOnly(2026, 3, 10), "Venta", null, Guid.NewGuid(), FixedNow);
        entry.AddLine(JournalLine.CreateDebit(cash.Id, new Money(100m, Currency.CRC)));
        entry.AddLine(JournalLine.CreateCredit(sales.Id, new Money(100m, Currency.CRC)));

        using var context = _factory.CreateContext(Interceptor);
        context.Accounts.AddRange(cash, sales);
        context.FiscalYears.Add(fiscalYear);
        context.Periods.Add(period);
        context.JournalEntries.Add(entry);
        await context.SaveChangesAsync();

        // The entry was Added (not Modified), so creating it is just "Create" — Post
        // only applies to a Draft→Posted transition detected on an already-saved row.
        entry.Post(1, period, new Dictionary<Guid, Account> { [cash.Id] = cash, [sales.Id] = sales }, Guid.NewGuid(), FixedNow);
        await context.SaveChangesAsync();

        var postRow = await context.AuditLogs.SingleAsync(a => a.Entity == nameof(JournalEntry) && a.Action == "Post");
        postRow.OldValuesJson.Should().Contain("Draft");
        postRow.NewValuesJson.Should().Contain("Posted");
    }

    [Fact]
    public async Task SaveChanges_ChangingUserPassword_NeverWritesHashOrSaltToAuditLog()
    {
        var user = new User("barista1", "Ana Pérez", "ana@sleepysloth.cr", "original-hash", "original-salt", UserRole.Contador, FixedNow);

        using var context = _factory.CreateContext(Interceptor);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        user.ChangePassword("new-hash-value", "new-salt-value");
        await context.SaveChangesAsync();

        var rows = await context.AuditLogs.Where(a => a.Entity == nameof(User) && a.EntityId == user.Id.ToString()).ToListAsync();

        rows.Should().NotBeEmpty();
        foreach (var row in rows)
        {
            (row.OldValuesJson + row.NewValuesJson).Should().NotContain("original-hash");
            (row.OldValuesJson + row.NewValuesJson).Should().NotContain("new-hash-value");
            (row.OldValuesJson + row.NewValuesJson).Should().NotContain("original-salt");
            (row.OldValuesJson + row.NewValuesJson).Should().NotContain("new-salt-value");
            (row.OldValuesJson + row.NewValuesJson).Should().NotContain("PasswordHash");
            (row.OldValuesJson + row.NewValuesJson).Should().NotContain("\"Salt\"");
        }
    }

    [Fact]
    public async Task SaveChanges_DeactivatingUser_StillAuditsNonSensitiveFields()
    {
        var user = new User("barista1", "Ana Pérez", "ana@sleepysloth.cr", "hash", "salt", UserRole.Contador, FixedNow);

        using var context = _factory.CreateContext(Interceptor);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        user.Deactivate();
        await context.SaveChangesAsync();

        var updateRow = await context.AuditLogs.SingleAsync(a => a.Entity == nameof(User) && a.Action == "Update");
        updateRow.NewValuesJson.Should().Contain(nameof(User.IsActive));
        updateRow.OldValuesJson.Should().NotBeNull();
    }
}

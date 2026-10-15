using Folio.Application.Abstractions;
using Folio.Domain.Common;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;

namespace Folio.Infrastructure.Seed;

/// <summary>
/// Seeds one demo company — Sleepy Sloth Café &amp; Bakery S.A. — with its chart of
/// accounts, users, a trailing 12-month window of exchange rates and posted journal
/// entries, and closed periods for every month except the current one. Only called
/// when the database is empty (<see cref="FolioDbInitializer"/>).
/// </summary>
public static class FolioSeeder
{
    public static async Task<string> SeedAsync(
        FolioDbContext context,
        IClock clock,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        var today = clock.TodayInCostaRica();
        var windowStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var windowEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        var company = new CompanySettings(
            "Sleepy Sloth Café & Bakery S.A.",
            "3-101-987654",
            fiscalYearStartMonth: 1,
            vatRate: 0.13m);
        context.CompanySettings.Add(company);

        var users = SeedUsers(passwordHasher, clock.UtcNow);
        context.Users.AddRange(users);
        var adminUserId = users.First(u => u.Role == UserRole.Administrador).Id;

        var (accounts, accountsByCode) = ChartOfAccountsSeedData.BuildAccounts();
        context.Accounts.AddRange(accounts);

        var fiscalYear = new FiscalYear(today.Year, windowStart, windowEnd);
        var periods = fiscalYear.GeneratePeriods();
        context.FiscalYears.Add(fiscalYear);
        context.Periods.AddRange(periods);

        var rates = ExchangeRateSeedData.BuildRates(windowStart, today);
        context.ExchangeRates.AddRange(rates);
        var ratesByDate = rates.ToDictionary(r => r.Date);

        var entries = DemoEntriesSeeder.BuildEntries(accountsByCode, periods, ratesByDate, adminUserId, today);
        context.JournalEntries.AddRange(entries);

        // Every period is closed except the current (last) one, which stays open.
        foreach (var period in periods.Take(periods.Count - 1))
        {
            period.Close(adminUserId, clock.UtcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        return $"Sleepy Sloth Café & Bakery S.A.: {accounts.Count} cuentas, {users.Count} usuarios, " +
               $"{periods.Count} períodos, {rates.Count} tipos de cambio, {entries.Count} asientos contabilizados.";
    }

    private static List<User> SeedUsers(IPasswordHasher hasher, DateTime nowUtc)
    {
        (string Username, string FullName, string Email, UserRole Role)[] specs =
        [
            ("admin", "Administradora del Sistema", "admin@sleepysloth.cr", UserRole.Administrador),
            ("contador", "Contador General", "contador@sleepysloth.cr", UserRole.Contador),
            ("consulta", "Usuario de Consulta", "consulta@sleepysloth.cr", UserRole.Consulta),
        ];

        var users = new List<User>(specs.Length);
        foreach (var spec in specs)
        {
            var (hash, salt) = hasher.Hash("Folio2026!");
            users.Add(new User(spec.Username, spec.FullName, spec.Email, hash, salt, spec.Role, nowUtc));
        }

        return users;
    }
}

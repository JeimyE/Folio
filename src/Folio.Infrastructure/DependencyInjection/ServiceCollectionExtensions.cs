using Folio.Application.Abstractions;
using Folio.Application.Abstractions.Repositories;
using Folio.Application.Authentication;
using Folio.Application.ChartOfAccounts;
using Folio.Application.Company;
using Folio.Domain.Common;
using Folio.Infrastructure.Auditing;
using Folio.Infrastructure.Common;
using Folio.Infrastructure.Hosting;
using Folio.Infrastructure.Persistence;
using Folio.Infrastructure.Persistence.Repositories;
using Folio.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything Infrastructure provides: the EF Core context factory (with
    /// the audit interceptor attached), repositories, cross-cutting singletons
    /// (<see cref="IClock"/>, <see cref="IPasswordHasher"/>, the current-user session),
    /// the authentication use case, and the hosted service that migrates/seeds the
    /// database on startup. A single-user desktop app has no per-request scope, so
    /// every one of these is a Singleton — the <see cref="IDbContextFactory{TContext}"/>
    /// hands out short-lived contexts on demand instead.
    /// </summary>
    public static IServiceCollection AddFolioInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<CurrentUserSession>();
        services.AddSingleton<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserSession>());
        services.AddSingleton<ICurrentUserSession>(sp => sp.GetRequiredService<CurrentUserSession>());
        services.AddSingleton<AuditSaveChangesInterceptor>();

        services.AddDbContextFactory<FolioDbContext>((sp, options) =>
            options.UseSqlite(FolioPaths.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddSingleton<IAccountRepository, AccountRepository>();
        services.AddSingleton<IFiscalYearRepository, FiscalYearRepository>();
        services.AddSingleton<IPeriodRepository, PeriodRepository>();
        services.AddSingleton<IJournalEntryRepository, JournalEntryRepository>();
        services.AddSingleton<IExchangeRateRepository, ExchangeRateRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<ICompanySettingsRepository, CompanySettingsRepository>();
        services.AddSingleton<IEntryTemplateRepository, EntryTemplateRepository>();

        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IChartOfAccountsService, ChartOfAccountsService>();
        services.AddSingleton<ICompanyContextService, CompanyContextService>();

        services.AddHostedService<DatabaseInitializerHostedService>();

        return services;
    }
}

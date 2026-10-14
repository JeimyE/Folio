using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;

namespace Folio.Infrastructure.Seed;

/// <summary>Builds the standard Costa Rican SME chart of accounts from SPEC §2.3.</summary>
public static class ChartOfAccountsSeedData
{
    public static (IReadOnlyList<Account> Accounts, IReadOnlyDictionary<string, Account> ByCode) BuildAccounts()
    {
        var byCode = new Dictionary<string, Account>();

        Account Add(string code, string name, AccountType type, string? parentCode, bool isPostable, NormalBalance? normalBalanceOverride = null)
        {
            var parent = parentCode is null ? null : byCode[parentCode];
            var account = new Account(new AccountCode(code), name, type, isPostable, parent?.Id, normalBalanceOverride);
            parent?.AddChild();
            byCode[code] = account;
            return account;
        }

        // 1 Activo
        Add("1", "Activo", AccountType.Asset, null, isPostable: false);
        Add("1.1", "Activo corriente", AccountType.Asset, "1", isPostable: false);
        Add("1.1.01", "Caja", AccountType.Asset, "1.1", isPostable: true);
        Add("1.1.02", "Bancos colones", AccountType.Asset, "1.1", isPostable: true);
        Add("1.1.03", "Bancos dólares", AccountType.Asset, "1.1", isPostable: true);
        Add("1.1.04", "Cuentas por cobrar", AccountType.Asset, "1.1", isPostable: true);
        Add("1.1.05", "Inventario", AccountType.Asset, "1.1", isPostable: true);
        Add("1.1.06", "IVA acreditable", AccountType.Asset, "1.1", isPostable: true);
        Add("1.2", "Activo no corriente", AccountType.Asset, "1", isPostable: false);
        Add("1.2.01", "Mobiliario y equipo", AccountType.Asset, "1.2", isPostable: true);
        Add("1.2.02", "Equipo de cómputo", AccountType.Asset, "1.2", isPostable: true);
        Add("1.2.03", "Vehículos", AccountType.Asset, "1.2", isPostable: true);
        Add("1.2.04", "Depreciación acumulada", AccountType.Asset, "1.2", isPostable: true, NormalBalance.Credit);

        // 2 Pasivo
        Add("2", "Pasivo", AccountType.Liability, null, isPostable: false);
        Add("2.1", "Pasivo corriente", AccountType.Liability, "2", isPostable: false);
        Add("2.1.01", "Cuentas por pagar", AccountType.Liability, "2.1", isPostable: true);
        Add("2.1.02", "IVA por pagar", AccountType.Liability, "2.1", isPostable: true);
        Add("2.1.03", "Cargas sociales por pagar", AccountType.Liability, "2.1", isPostable: true);
        Add("2.1.04", "Impuesto sobre la renta por pagar", AccountType.Liability, "2.1", isPostable: true);
        Add("2.1.05", "Salarios por pagar", AccountType.Liability, "2.1", isPostable: true);
        Add("2.2", "Pasivo no corriente", AccountType.Liability, "2", isPostable: false);
        Add("2.2.01", "Préstamos bancarios a largo plazo", AccountType.Liability, "2.2", isPostable: true);

        // 3 Patrimonio
        Add("3", "Patrimonio", AccountType.Equity, null, isPostable: false);
        Add("3.1", "Capital social", AccountType.Equity, "3", isPostable: true);
        Add("3.2", "Utilidades acumuladas", AccountType.Equity, "3", isPostable: true);
        Add("3.3", "Utilidad o pérdida del período", AccountType.Equity, "3", isPostable: true);

        // 4 Ingresos
        Add("4", "Ingresos", AccountType.Revenue, null, isPostable: false);
        Add("4.1", "Ventas", AccountType.Revenue, "4", isPostable: true);
        Add("4.2", "Servicios", AccountType.Revenue, "4", isPostable: true);
        Add("4.3", "Diferencial cambiario ganado", AccountType.Revenue, "4", isPostable: true);
        Add("4.4", "Otros ingresos", AccountType.Revenue, "4", isPostable: true);

        // 5 Costos
        Add("5", "Costos", AccountType.Cost, null, isPostable: false);
        Add("5.1", "Costo de ventas", AccountType.Cost, "5", isPostable: true);

        // 6 Gastos
        Add("6", "Gastos", AccountType.Expense, null, isPostable: false);
        Add("6.1", "Salarios", AccountType.Expense, "6", isPostable: true);
        Add("6.2", "Cargas sociales", AccountType.Expense, "6", isPostable: true);
        Add("6.3", "Alquiler", AccountType.Expense, "6", isPostable: true);
        Add("6.4", "Servicios públicos", AccountType.Expense, "6", isPostable: true);
        Add("6.5", "Telecomunicaciones", AccountType.Expense, "6", isPostable: true);
        Add("6.6", "Depreciación", AccountType.Expense, "6", isPostable: true);
        Add("6.7", "Gastos bancarios", AccountType.Expense, "6", isPostable: true);
        Add("6.8", "Diferencial cambiario perdido", AccountType.Expense, "6", isPostable: true);
        Add("6.9", "Intereses", AccountType.Expense, "6", isPostable: true);

        return (byCode.Values.ToList(), byCode);
    }
}

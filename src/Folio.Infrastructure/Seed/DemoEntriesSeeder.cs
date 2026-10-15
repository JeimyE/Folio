using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;

namespace Folio.Infrastructure.Seed;

/// <summary>
/// Builds 12 months of realistic, balanced journal entries for Sleepy Sloth Café &amp;
/// Bakery S.A. (SPEC §5, demo data). Every entry is posted through the real
/// <see cref="JournalEntry.Post"/> — if any amount here didn't balance, seeding would
/// fail loudly at first launch instead of quietly writing bad data.
/// <para>
/// The numbers are meant to be believable, not just balanced: everything that is
/// accrued in a month (suppliers, CCSS, VAT) is paid the following month, the way a
/// real business settles after month-end, so liabilities stay at roughly one month's
/// worth instead of piling up; purchases flow through inventory into cost of sales;
/// and the café earns a moderate profit (verified by
/// <c>DemoSeedFinancialsIntegrationTests</c>).
/// </para>
/// </summary>
public static class DemoEntriesSeeder
{
    // Costa Rican payroll (CCSS): the employer pays 26.33% on top of gross salary, and
    // 10.67% is withheld from the employee's salary; both are paid to the CCSS together.
    private const decimal EmployerChargesRate = 0.2633m;
    private const decimal WorkerDeductionRate = 0.1067m;
    private const decimal VatRate = 0.13m;

    // 12% a year on the outstanding loan balance, plus a fixed principal installment.
    private const decimal LoanMonthlyInterestRate = 0.01m;
    private const decimal LoanMonthlyPrincipal = 50_000m;

    // Share of each month's supply purchases consumed (and expensed) that same month;
    // the rest stays in inventory.
    private const decimal CostOfSalesShareOfPurchases = 0.95m;

    public static IReadOnlyList<JournalEntry> BuildEntries(
        IReadOnlyDictionary<string, Account> accountsByCode,
        IReadOnlyList<Period> periods,
        IReadOnlyDictionary<DateOnly, ExchangeRate> ratesByDate,
        Guid createdByUserId,
        DateOnly today)
    {
        var accountsById = accountsByCode.Values.ToDictionary(a => a.Id);
        var entries = new List<JournalEntry>();
        var random = new Random(20260615);
        var number = 0;

        // Carried from one month into the next: what the month left owed, settled in
        // the following month.
        var loanBalance = 0m;
        var owedToSuppliers = 0m;
        var owedToCcss = 0m;
        var vatCharged = 0m;
        var vatCredit = 0m;

        Account Acc(string code) => accountsByCode[code];
        Money Crc(decimal amount) => new(amount, Currency.CRC);
        decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        // The given day of the period's month (capped to the month's length), never
        // later than today — the current month is only partly elapsed (ADR-0005).
        DateOnly DayOf(Period period, int dayOfMonth)
        {
            var date = period.StartDate.AddDays(Math.Min(dayOfMonth, period.EndDate.Day) - 1);
            return date > today ? today : date;
        }

        JournalEntry NewDraft(DateOnly date, string description, string? reference = null) =>
            new(date, description, reference, createdByUserId, date.ToDateTime(TimeOnly.MinValue));

        void Post(JournalEntry entry, Period period)
        {
            number++;
            entry.Post(number, period, accountsById, createdByUserId, entry.CreatedAtUtc);
            entries.Add(entry);
        }

        // A two-line entry: debit one account, credit another, same amount.
        void PostSimple(Period period, DateOnly date, string description, string debitCode, string creditCode, decimal amount, string? reference = null)
        {
            var entry = NewDraft(date, description, reference);
            entry.AddLine(JournalLine.CreateDebit(Acc(debitCode).Id, Crc(amount)));
            entry.AddLine(JournalLine.CreateCredit(Acc(creditCode).Id, Crc(amount)));
            Post(entry, period);
        }

        foreach (var period in periods)
        {
            var isFirstMonth = period == periods[0];
            DateOnly Day(int dayOfMonth) => DayOf(period, dayOfMonth);

            if (isFirstMonth)
            {
                var opening = NewDraft(Day(1), "Aporte de capital inicial", "APERTURA-1");
                opening.AddLine(JournalLine.CreateDebit(Acc("1.1.02").Id, Crc(4_800_000m)));
                opening.AddLine(JournalLine.CreateDebit(Acc("1.1.01").Id, Crc(200_000m))); // petty-cash float
                opening.AddLine(JournalLine.CreateCredit(Acc("3.1").Id, Crc(5_000_000m)));
                Post(opening, period);

                PostSimple(period, Day(2), "Préstamo bancario para equipo de panadería", "1.1.02", "2.2.01", 3_000_000m, "PRESTAMO-1");
                loanBalance = 3_000_000m;

                PostSimple(period, Day(2), "Compra de horno, máquina de espresso y mobiliario", "1.2.01", "1.1.02", 5_400_000m, "FC-EQUIPO-1");
            }
            else
            {
                PayLastMonthsObligations(period);
            }

            PostSimple(period, Day(3), "Alquiler del local", "6.3", "1.1.02", 450_000m);

            // Supplies (coffee beans, flour, dairy) bought on credit, with IVA acreditable.
            var purchaseNet = 1_600_000m + random.Next(0, 200_000);
            {
                var iva = Round(purchaseNet * VatRate);
                var purchase = NewDraft(Day(8), "Compra de café, harina y lácteos", $"FC-{period.Year}{period.Month:D2}");
                purchase.AddLine(JournalLine.CreateDebit(Acc("1.1.05").Id, Crc(purchaseNet)));
                purchase.AddLine(JournalLine.CreateDebit(Acc("1.1.06").Id, Crc(iva)));
                purchase.AddLine(JournalLine.CreateCredit(Acc("2.1.01").Id, Crc(purchaseNet + iva)));
                Post(purchase, period);
                owedToSuppliers += purchaseNet + iva;
                vatCredit += iva;
            }

            // Weekly CRC sales, with IVA: card/SINPE weeks go straight to the bank, cash
            // weeks to Caja (deposited at month-end, keeping the petty-cash float).
            var cashToDeposit = 0m;
            foreach (var day in new[] { 5, 12, 19, 26 })
            {
                var net = 1_050_000m + random.Next(0, 250_000);
                var iva = Round(net * VatRate);
                var paidInCash = day is 12 or 26;
                var cashAccount = paidInCash ? "1.1.01" : "1.1.02";

                var sale = NewDraft(Day(day), "Ventas de la semana: café y repostería", $"FV-{period.Year}{period.Month:D2}-{day:D2}");
                sale.AddLine(JournalLine.CreateDebit(Acc(cashAccount).Id, Crc(net + iva)));
                sale.AddLine(JournalLine.CreateCredit(Acc("4.1").Id, Crc(net)));
                sale.AddLine(JournalLine.CreateCredit(Acc("2.1.02").Id, Crc(iva)));
                Post(sale, period);
                vatCharged += iva;
                if (paidInCash)
                {
                    cashToDeposit += net + iva;
                }
            }

            // USD sales to tourists. The breakdown (Ventas + IVA) is computed as a
            // remainder of the already-converted total (ADR-0002), never by converting
            // the net and IVA portions independently.
            {
                var date = Day(16);
                var rate = ratesByDate[date].Sell;
                var usdTotal = 350m + random.Next(0, 300);

                var collectionLine = JournalLine.CreateForeignDebit(Acc("1.1.03").Id, new Money(usdTotal, Currency.USD), rate);
                var totalCrc = collectionLine.Debit;
                var netCrc = Round(totalCrc / (1 + VatRate));
                var ivaCrc = totalCrc - netCrc;

                var usdSale = NewDraft(date, "Ventas a turistas en dólares", $"FV-USD-{period.Year}{period.Month:D2}");
                usdSale.AddLine(collectionLine);
                usdSale.AddLine(JournalLine.CreateCredit(Acc("4.1").Id, Crc(netCrc)));
                usdSale.AddLine(JournalLine.CreateCredit(Acc("2.1.02").Id, Crc(ivaCrc)));
                Post(usdSale, period);
                vatCharged += ivaCrc;
            }

            PostSimple(period, Day(20), "Servicios públicos (agua y luz)", "6.4", "1.1.02", 110_000m + random.Next(0, 25_000));
            PostSimple(period, Day(21), "Internet y telefonía", "6.5", "1.1.02", 35_000m);

            // Loan installment: fixed principal plus interest on the outstanding balance.
            {
                var interest = Round(loanBalance * LoanMonthlyInterestRate);
                var installment = NewDraft(Day(25), "Cuota del préstamo bancario", "PRESTAMO-1");
                installment.AddLine(JournalLine.CreateDebit(Acc("2.2.01").Id, Crc(LoanMonthlyPrincipal)));
                installment.AddLine(JournalLine.CreateDebit(Acc("6.9").Id, Crc(interest)));
                installment.AddLine(JournalLine.CreateCredit(Acc("1.1.02").Id, Crc(LoanMonthlyPrincipal + interest)));
                Post(installment, period);
                loanBalance -= LoanMonthlyPrincipal;
            }

            // Payroll accrual: gross salary and the employer's CCSS charge are expenses;
            // the employee's CCSS deduction is withheld, so only the net is owed to staff.
            var grossSalaries = 1_800_000m + random.Next(-50_000, 50_000);
            var workerDeduction = Round(grossSalaries * WorkerDeductionRate);
            var employerCharges = Round(grossSalaries * EmployerChargesRate);
            var netSalaries = grossSalaries - workerDeduction;
            {
                var payroll = NewDraft(Day(27), "Planilla de baristas y panaderos");
                payroll.AddLine(JournalLine.CreateDebit(Acc("6.1").Id, Crc(grossSalaries)));
                payroll.AddLine(JournalLine.CreateDebit(Acc("6.2").Id, Crc(employerCharges)));
                payroll.AddLine(JournalLine.CreateCredit(Acc("2.1.05").Id, Crc(netSalaries)));
                payroll.AddLine(JournalLine.CreateCredit(Acc("2.1.03").Id, Crc(workerDeduction + employerCharges)));
                Post(payroll, period);
                owedToCcss += workerDeduction + employerCharges;
            }

            PostSimple(period, Day(28), "Depósito del efectivo de ventas", "1.1.02", "1.1.01", cashToDeposit);
            PostSimple(period, Day(28), "Comisiones bancarias y de datáfono", "6.7", "1.1.02", 9_500m);

            // Month-end: pay salaries, expense the supplies consumed, depreciate.
            var monthEnd = Day(period.EndDate.Day);
            PostSimple(period, monthEnd, "Pago de salarios", "2.1.05", "1.1.02", netSalaries);
            PostSimple(period, monthEnd, "Costo de ventas del mes", "5.1", "1.1.05", Round(purchaseNet * CostOfSalesShareOfPurchases));
            PostSimple(period, monthEnd, "Depreciación mensual de mobiliario y equipo", "6.6", "1.2.04", 45_000m);
        }

        return entries;

        // Suppliers, the CCSS and the D-104 VAT return are all paid in the month after
        // they were incurred, so each month-end only carries one month of payables.
        void PayLastMonthsObligations(Period period)
        {
            DateOnly Day(int dayOfMonth) => DayOf(period, dayOfMonth);

            PostSimple(period, Day(7), "Pago a proveedores del mes anterior", "2.1.01", "1.1.02", owedToSuppliers);
            owedToSuppliers = 0m;

            PostSimple(period, Day(10), "Pago de cargas sociales a la CCSS", "2.1.03", "1.1.02", owedToCcss);
            owedToCcss = 0m;

            // Sales always exceed purchases here, so the return is always a payment.
            var vatReturn = NewDraft(Day(15), "Pago de IVA del mes anterior (D-104)");
            vatReturn.AddLine(JournalLine.CreateDebit(Acc("2.1.02").Id, Crc(vatCharged)));
            vatReturn.AddLine(JournalLine.CreateCredit(Acc("1.1.06").Id, Crc(vatCredit)));
            vatReturn.AddLine(JournalLine.CreateCredit(Acc("1.1.02").Id, Crc(vatCharged - vatCredit)));
            Post(vatReturn, period);
            vatCharged = 0m;
            vatCredit = 0m;
        }
    }
}

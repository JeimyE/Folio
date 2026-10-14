using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;

namespace Folio.Infrastructure.Seed;

/// <summary>
/// Builds 12 months of realistic, balanced journal entries for Sleepy Sloth Café &amp;
/// Bakery S.A. (SPEC §5, demo data). Every entry is posted through the real
/// <see cref="JournalEntry.Post"/> — if any amount here didn't balance, seeding would
/// fail loudly at first launch instead of quietly writing bad data.
/// </summary>
public static class DemoEntriesSeeder
{
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

        Account Acc(string code) => accountsByCode[code];
        DateOnly ClampToToday(DateOnly date) => date > today ? today : date;
        int DaysInPeriod(Period period) => period.EndDate.DayNumber - period.StartDate.DayNumber + 1;

        JournalEntry NewDraft(DateOnly date, string description, string? reference = null) =>
            new(date, description, reference, createdByUserId, date.ToDateTime(TimeOnly.MinValue));

        void Post(JournalEntry entry, Period period)
        {
            number++;
            entry.Post(number, period, accountsById, createdByUserId, entry.CreatedAtUtc);
            entries.Add(entry);
        }

        for (var i = 0; i < periods.Count; i++)
        {
            var period = periods[i];
            var monthStart = period.StartDate;
            var daysInPeriod = DaysInPeriod(period);

            if (i == 0)
            {
                var opening = NewDraft(ClampToToday(monthStart), "Aporte de capital inicial", "APERTURA-1");
                opening.AddLine(JournalLine.CreateDebit(Acc("1.1.02").Id, new Money(5_000_000m, Currency.CRC)));
                opening.AddLine(JournalLine.CreateCredit(Acc("3.1").Id, new Money(5_000_000m, Currency.CRC)));
                Post(opening, period);

                var loan = NewDraft(ClampToToday(monthStart.AddDays(1)), "Préstamo bancario para equipo de panadería", "PRESTAMO-1");
                loan.AddLine(JournalLine.CreateDebit(Acc("1.1.02").Id, new Money(3_000_000m, Currency.CRC)));
                loan.AddLine(JournalLine.CreateCredit(Acc("2.2.01").Id, new Money(3_000_000m, Currency.CRC)));
                Post(loan, period);
            }

            // Rent.
            var rentDate = ClampToToday(monthStart.AddDays(Math.Min(2, daysInPeriod - 1)));
            var rent = NewDraft(rentDate, "Alquiler del local");
            rent.AddLine(JournalLine.CreateDebit(Acc("6.3").Id, new Money(350_000m, Currency.CRC)));
            rent.AddLine(JournalLine.CreateCredit(Acc("1.1.02").Id, new Money(350_000m, Currency.CRC)));
            Post(rent, period);

            // Payroll with cargas sociales (~26.33%, a realistic Costa Rican employer rate).
            var payrollDate = ClampToToday(monthStart.AddDays(Math.Min(26, daysInPeriod - 1)));
            var salaries = 1_800_000m + random.Next(-50_000, 50_000);
            var cargas = Math.Round(salaries * 0.2633m, 2, MidpointRounding.AwayFromZero);
            var payroll = NewDraft(payrollDate, "Planilla de baristas y panaderos");
            payroll.AddLine(JournalLine.CreateDebit(Acc("6.1").Id, new Money(salaries, Currency.CRC)));
            payroll.AddLine(JournalLine.CreateDebit(Acc("6.2").Id, new Money(cargas, Currency.CRC)));
            payroll.AddLine(JournalLine.CreateCredit(Acc("2.1.05").Id, new Money(salaries, Currency.CRC)));
            payroll.AddLine(JournalLine.CreateCredit(Acc("2.1.03").Id, new Money(cargas, Currency.CRC)));
            Post(payroll, period);

            // A handful of CRC sales of coffee and baked goods, with IVA.
            foreach (var day in new[] { 5, 12, 19 })
            {
                if (day > daysInPeriod)
                {
                    continue;
                }

                var date = ClampToToday(monthStart.AddDays(day - 1));
                var net = 40_000m + random.Next(0, 30_000);
                var iva = Math.Round(net * 0.13m, 2, MidpointRounding.AwayFromZero);
                var total = net + iva;
                var cashAccount = day % 2 == 0 ? "1.1.02" : "1.1.01";

                var sale = NewDraft(date, "Venta de café y repostería", $"FV-{period.Year}{period.Month:D2}-{day:D2}");
                sale.AddLine(JournalLine.CreateDebit(Acc(cashAccount).Id, new Money(total, Currency.CRC)));
                sale.AddLine(JournalLine.CreateCredit(Acc("4.1").Id, new Money(net, Currency.CRC)));
                sale.AddLine(JournalLine.CreateCredit(Acc("2.1.02").Id, new Money(iva, Currency.CRC)));
                Post(sale, period);
            }

            // One USD sale to tourists per month. The breakdown (Ventas + IVA) is computed
            // as a remainder of the already-converted total (ADR-0002), never by converting
            // the net and IVA portions independently.
            {
                var date = ClampToToday(monthStart.AddDays(Math.Min(15, daysInPeriod - 1)));
                var rate = ratesByDate[date].Sell;
                var usdTotal = 15m + random.Next(0, 20);

                var collectionLine = JournalLine.CreateForeignDebit(Acc("1.1.03").Id, new Money(usdTotal, Currency.USD), rate);
                var totalCrc = collectionLine.Debit;
                var netCrc = Math.Round(totalCrc / 1.13m, 2, MidpointRounding.AwayFromZero);
                var ivaCrc = totalCrc - netCrc;

                var usdSale = NewDraft(date, "Venta a turistas en dólares", $"FV-USD-{period.Year}{period.Month:D2}");
                usdSale.AddLine(collectionLine);
                usdSale.AddLine(JournalLine.CreateCredit(Acc("4.1").Id, new Money(netCrc, Currency.CRC)));
                usdSale.AddLine(JournalLine.CreateCredit(Acc("2.1.02").Id, new Money(ivaCrc, Currency.CRC)));
                Post(usdSale, period);
            }

            // Purchase of supplies (coffee beans, flour, dairy), with IVA acreditable.
            {
                var date = ClampToToday(monthStart.AddDays(Math.Min(8, daysInPeriod - 1)));
                var net = 180_000m + random.Next(0, 40_000);
                var iva = Math.Round(net * 0.13m, 2, MidpointRounding.AwayFromZero);
                var total = net + iva;

                var purchase = NewDraft(date, "Compra de café, harina y lácteos", $"FC-{period.Year}{period.Month:D2}");
                purchase.AddLine(JournalLine.CreateDebit(Acc("1.1.05").Id, new Money(net, Currency.CRC)));
                purchase.AddLine(JournalLine.CreateDebit(Acc("1.1.06").Id, new Money(iva, Currency.CRC)));
                purchase.AddLine(JournalLine.CreateCredit(Acc("2.1.01").Id, new Money(total, Currency.CRC)));
                Post(purchase, period);
            }

            // Utilities.
            {
                var date = ClampToToday(monthStart.AddDays(Math.Min(20, daysInPeriod - 1)));
                var amount = 95_000m + random.Next(0, 15_000);

                var utilities = NewDraft(date, "Servicios públicos (agua, luz, internet)");
                utilities.AddLine(JournalLine.CreateDebit(Acc("6.4").Id, new Money(amount, Currency.CRC)));
                utilities.AddLine(JournalLine.CreateCredit(Acc("1.1.02").Id, new Money(amount, Currency.CRC)));
                Post(utilities, period);
            }

            // Month-end depreciation.
            {
                var date = ClampToToday(period.EndDate);
                var depreciation = NewDraft(date, "Depreciación mensual de mobiliario y equipo");
                depreciation.AddLine(JournalLine.CreateDebit(Acc("6.6").Id, new Money(45_000m, Currency.CRC)));
                depreciation.AddLine(JournalLine.CreateCredit(Acc("1.2.04").Id, new Money(45_000m, Currency.CRC)));
                Post(depreciation, period);
            }
        }

        return entries;
    }
}

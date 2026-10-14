namespace Folio.Domain.Entities;

/// <summary>
/// A fiscal year, divided into monthly <see cref="Period"/>s.
/// </summary>
public sealed class FiscalYear
{
    public Guid Id { get; }

    public int Year { get; }

    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }

    public bool IsClosed { get; private set; }

    public FiscalYear(int year, DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date.", nameof(endDate));
        }

        Id = Guid.NewGuid();
        Year = year;
        StartDate = startDate;
        EndDate = endDate;
        IsClosed = false;
    }

    /// <summary>
    /// Generates one <see cref="Period"/> per calendar month between <see cref="StartDate"/>
    /// and <see cref="EndDate"/>, the last one truncated to <see cref="EndDate"/> if needed.
    /// </summary>
    public IReadOnlyList<Period> GeneratePeriods()
    {
        var periods = new List<Period>();
        var monthStart = StartDate;

        while (monthStart <= EndDate)
        {
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var monthEnd = new DateOnly(monthStart.Year, monthStart.Month, daysInMonth);
            if (monthEnd > EndDate)
            {
                monthEnd = EndDate;
            }

            periods.Add(new Period(Id, monthStart.Year, monthStart.Month, monthStart, monthEnd));
            monthStart = monthEnd.AddDays(1);
        }

        return periods;
    }

    public void Close()
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Fiscal year is already closed.");
        }

        IsClosed = true;
    }
}

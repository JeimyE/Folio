using Folio.Domain.Enums;

namespace Folio.Domain.Entities;

/// <summary>
/// A monthly accounting period. Closing it blocks any posting dated inside it.
/// </summary>
public sealed class Period
{
    public Guid Id { get; }

    public Guid FiscalYearId { get; }

    public int Year { get; }

    public int Month { get; }

    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }

    public PeriodStatus Status { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    // Used by EF Core to materialize an existing row; see Account's private constructor.
    private Period()
    {
    }

    public Period(Guid fiscalYearId, int year, int month, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must not be before start date.", nameof(endDate));
        }

        Id = Guid.NewGuid();
        FiscalYearId = fiscalYearId;
        Year = year;
        Month = month;
        StartDate = startDate;
        EndDate = endDate;
        Status = PeriodStatus.Open;
    }

    /// <summary>
    /// Whether this period is open and <paramref name="date"/> falls within its range.
    /// </summary>
    public bool IsOpenFor(DateOnly date) =>
        Status == PeriodStatus.Open && date >= StartDate && date <= EndDate;

    public void Close(Guid closedByUserId, DateTime closedAtUtc)
    {
        if (Status == PeriodStatus.Closed)
        {
            throw new InvalidOperationException("Period is already closed.");
        }

        Status = PeriodStatus.Closed;
        ClosedAtUtc = closedAtUtc;
        ClosedByUserId = closedByUserId;
    }

    /// <summary>
    /// Reopens the period. Whether the caller is authorized (SPEC §2.5: Administrador
    /// only) is an Application-layer concern, enforced before calling this method.
    /// </summary>
    public void Reopen()
    {
        if (Status == PeriodStatus.Open)
        {
            throw new InvalidOperationException("Period is already open.");
        }

        Status = PeriodStatus.Open;
        ClosedAtUtc = null;
        ClosedByUserId = null;
    }
}

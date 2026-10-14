using Folio.Domain.Common;

namespace Folio.Infrastructure.Common;

/// <summary>The real, wall-clock implementation of <see cref="IClock"/>.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

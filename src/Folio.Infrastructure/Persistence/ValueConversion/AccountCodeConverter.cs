using Folio.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Folio.Infrastructure.Persistence.ValueConversion;

/// <summary>Stores an <see cref="AccountCode"/> as its plain string value.</summary>
public sealed class AccountCodeConverter : ValueConverter<AccountCode, string>
{
    public AccountCodeConverter()
        : base(code => code.Value, value => new AccountCode(value))
    {
    }
}

namespace Folio.Application.Abstractions;

/// <summary>
/// Hashes and verifies passwords. Implemented in Infrastructure with PBKDF2
/// (SPEC §2.1); the Domain never sees a plain-text password.
/// </summary>
public interface IPasswordHasher
{
    (string Hash, string Salt) Hash(string plainTextPassword);

    bool Verify(string plainTextPassword, string hash, string salt);
}

using System.Security.Cryptography;
using Folio.Application.Abstractions;

namespace Folio.Infrastructure.Security;

/// <summary>
/// Hashes passwords with PBKDF2 / SHA-256, a per-user salt and 100,000 iterations
/// (SPEC §2.1). Never logs or stores a plain-text password.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSizeInBytes = 16;
    private const int HashSizeInBytes = 32;
    private const int Iterations = 100_000;

    public (string Hash, string Salt) Hash(string plainTextPassword)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(plainTextPassword, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSizeInBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool Verify(string plainTextPassword, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHashBytes = Convert.FromBase64String(hash);
        var actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(plainTextPassword, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSizeInBytes);
        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }
}

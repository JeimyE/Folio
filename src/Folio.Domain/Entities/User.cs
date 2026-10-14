using Folio.Domain.Enums;

namespace Folio.Domain.Entities;

/// <summary>
/// A system user. Never holds a plain-text password: callers must hash it
/// (PBKDF2, an Infrastructure concern) before constructing or changing it here.
/// </summary>
public sealed class User
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public Guid Id { get; }

    public string Username { get; }

    public string FullName { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string Salt { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public int FailedAttempts { get; private set; }

    public DateTime? LockedUntilUtc { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public User(
        string username,
        string fullName,
        string email,
        string passwordHash,
        string salt,
        UserRole role,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(salt))
        {
            throw new ArgumentException("A password hash and salt are required.");
        }

        Id = Guid.NewGuid();
        Username = username;
        FullName = fullName;
        Email = email;
        PasswordHash = passwordHash;
        Salt = salt;
        Role = role;
        IsActive = true;
        FailedAttempts = 0;
        CreatedAtUtc = createdAtUtc;
    }

    public bool IsLockedOut(DateTime nowUtc) => LockedUntilUtc is { } lockedUntil && nowUtc < lockedUntil;

    /// <summary>
    /// Records a failed login attempt. Locks the account for
    /// <see cref="LockoutDuration"/> once <see cref="MaxFailedAttempts"/> is reached.
    /// </summary>
    public void RecordFailedLogin(DateTime nowUtc)
    {
        FailedAttempts++;
        if (FailedAttempts >= MaxFailedAttempts)
        {
            LockedUntilUtc = nowUtc.Add(LockoutDuration);
        }
    }

    public void RecordSuccessfulLogin()
    {
        FailedAttempts = 0;
        LockedUntilUtc = null;
    }

    public void ChangePassword(string newPasswordHash, string newSalt)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash) || string.IsNullOrWhiteSpace(newSalt))
        {
            throw new ArgumentException("A password hash and salt are required.");
        }

        PasswordHash = newPasswordHash;
        Salt = newSalt;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

namespace Folio.Domain.Exceptions;

/// <summary>
/// Base type for exceptions that signal a violated accounting rule, as opposed to
/// an invalid argument (which uses the standard BCL exception types).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

namespace Folio.Desktop.Messages;

/// <summary>Published by <c>LoginViewModel</c> after a successful login.</summary>
public sealed record UserSignedInMessage(Guid UserId, string FullName);

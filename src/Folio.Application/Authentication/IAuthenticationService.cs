namespace Folio.Application.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}

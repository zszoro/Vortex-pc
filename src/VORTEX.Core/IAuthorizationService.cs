namespace VORTEX.Core;

public interface IAuthorizationService
{
    Task<bool> RequestAsync(AuthorizationRequest request, CancellationToken cancellationToken = default);
}

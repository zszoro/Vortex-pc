using System.Windows;
using VORTEX.Core;

namespace VORTEX.UI;

public sealed class AuthorizationService : IAuthorizationService
{
    public async Task<bool> RequestAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var window = new AuthorizationWindow(request);
            if (Application.Current.MainWindow is { IsVisible: true } owner)
                window.Owner = owner;
            return window.ShowDialog() == true;
        });
    }
}

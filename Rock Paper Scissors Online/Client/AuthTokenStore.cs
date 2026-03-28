using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Rock_Paper_Scissors_Online.Client;

public class AuthTokenStore(ProtectedLocalStorage storage)
{
    private const string Key = "rps.portal.auth";

    public async Task<PortalAuthState?> LoadAsync()
    {
        try
        {
            var result = await storage.GetAsync<PortalAuthState>(Key);
            return result.Success ? result.Value : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task SaveAsync(PortalAuthState state) => await storage.SetAsync(Key, state);

    public async Task ClearAsync() => await storage.DeleteAsync(Key);
}

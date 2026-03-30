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
        catch (Exception)
        {
            // Prerender / JS chưa sẵn sàng / trình duyệt chặn storage — không làm sập circuit.
            return null;
        }
    }

    public async Task SaveAsync(PortalAuthState state)
    {
        try
        {
            await storage.SetAsync(Key, state);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Không lưu được phiên đăng nhập.", ex);
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await storage.DeleteAsync(Key);
        }
        catch (Exception)
        {
            /* bỏ qua */
        }
    }
}

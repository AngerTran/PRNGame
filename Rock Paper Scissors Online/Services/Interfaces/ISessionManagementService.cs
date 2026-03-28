namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface ISessionManagementService
    {
        Task<bool> IsUserLoggedInAsync(string userId);
        Task<bool> RegisterUserSessionAsync(string userId, string connectionId);
        Task<bool> UnregisterUserSessionAsync(string userId, string connectionId);
        Task<bool> ForceLogoutUserAsync(string userId);
        Task<List<string>> GetUserConnectionsAsync(string userId);
        Task<bool> IsConnectionValidAsync(string userId, string connectionId);
        Task<int> GetActiveSessionCountAsync();
        Task<List<string>> GetAllActiveUsersAsync();
        Task<List<ActiveSession>> GetAllActiveSessionsAsync();
        void ClearAllSessions();
    }
}

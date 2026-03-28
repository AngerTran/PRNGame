using System.Collections.Concurrent;
using Rock_Paper_Scissors_Online.Services.Interfaces;

namespace Rock_Paper_Scissors_Online.Services
{
    public class ActiveSession
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> ConnectionIds { get; set; } = new();
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }
    public class SessionManagementService : ISessionManagementService
    {
        // Store active sessions: userId -> List of connectionIds
        private static readonly ConcurrentDictionary<string, List<string>> _activeSessions = new();
        // Store connection to user mapping for quick lookup
        private static readonly ConcurrentDictionary<string, string> _connectionToUser = new();

        public async Task<bool> IsUserLoggedInAsync(string userId)
        {
            await Task.CompletedTask;
            return _activeSessions.ContainsKey(userId) && _activeSessions[userId].Count > 0;
        }

        public async Task<bool> RegisterUserSessionAsync(string userId, string connectionId)
        {
            await Task.CompletedTask;
            
            try
            {
                // Add connection to user mapping
                _connectionToUser.TryAdd(connectionId, userId);
                
                // Add connection to user's session list
                _activeSessions.AddOrUpdate(
                    userId,
                    new List<string> { connectionId },
                    (key, existingList) =>
                    {
                        if (!existingList.Contains(connectionId))
                        {
                            existingList.Add(connectionId);
                        }
                        return existingList;
                    }
                );

                Console.WriteLine($"\u001b[35m[SESSION]\u001b[0m User {userId} registered with connection {connectionId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[35m[SESSION]\u001b[0m Error registering session for user {userId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UnregisterUserSessionAsync(string userId, string connectionId)
        {
            await Task.CompletedTask;
            
            try
            {
                // Remove connection from user mapping
                _connectionToUser.TryRemove(connectionId, out _);
                
                // Remove connection from user's session list
                if (_activeSessions.TryGetValue(userId, out var connections))
                {
                    connections.Remove(connectionId);
                    
                    // If no more connections, remove user from active sessions
                    if (connections.Count == 0)
                    {
                        _activeSessions.TryRemove(userId, out _);
                    }
                }

                Console.WriteLine($"\u001b[35m[SESSION]\u001b[0m User {userId} unregistered connection {connectionId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[35m[SESSION]\u001b[0m Error unregistering session for user {userId}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ForceLogoutUserAsync(string userId)
        {
            await Task.CompletedTask;
            
            try
            {
                if (_activeSessions.TryGetValue(userId, out var connections))
                {
                    // Remove all connections for this user
                    foreach (var connectionId in connections)
                    {
                        _connectionToUser.TryRemove(connectionId, out _);
                    }
                    
                    // Remove user from active sessions
                    _activeSessions.TryRemove(userId, out _);
                    
                    Console.WriteLine($"\u001b[35m[SESSION]\u001b[0m Force logged out user {userId} from {connections.Count} connections");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\u001b[35m[SESSION]\u001b[0m Error force logging out user {userId}: {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> GetUserConnectionsAsync(string userId)
        {
            await Task.CompletedTask;
            
            if (_activeSessions.TryGetValue(userId, out var connections))
            {
                return new List<string>(connections);
            }
            
            return new List<string>();
        }

        public async Task<bool> IsConnectionValidAsync(string userId, string connectionId)
        {
            await Task.CompletedTask;
            
            if (_activeSessions.TryGetValue(userId, out var connections))
            {
                return connections.Contains(connectionId);
            }
            
            return false;
        }

        public static string? GetUserIdFromConnection(string connectionId)
        {
            _connectionToUser.TryGetValue(connectionId, out var userId);
            return userId;
        }

        public static int GetActiveSessionCount()
        {
            return _activeSessions.Count;
        }

        public static int GetTotalConnectionCount()
        {
            return _connectionToUser.Count;
        }

        public async Task<List<string>> GetAllActiveUsersAsync()
        {
            await Task.CompletedTask;
            return _activeSessions.Keys.ToList();
        }

        public async Task<List<ActiveSession>> GetAllActiveSessionsAsync()
        {
            await Task.CompletedTask;
            return _activeSessions.Select(kvp => new ActiveSession
            {
                UserId = kvp.Key,
                ConnectionIds = new List<string>(kvp.Value),
                LastActivity = DateTime.UtcNow // TODO: Track actual last activity
            }).ToList();
        }

        public async Task<int> GetActiveSessionCountAsync()
        {
            await Task.CompletedTask;
            return _activeSessions.Count;
        }

        public void ClearAllSessions()
        {
            var sessionCount = _activeSessions.Count;
            var connectionCount = _connectionToUser.Count;
            
            _activeSessions.Clear();
            _connectionToUser.Clear();
            
            Console.WriteLine($"\u001b[35m[SESSION]\u001b[0m Cleared all sessions: {sessionCount} sessions, {connectionCount} connections");
        }
    }
}

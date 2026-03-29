using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Rock_Paper_Scissors_Online.Controllers;
using Rock_Paper_Scissors_Online.DTOs;

namespace Rock_Paper_Scissors_Online.Client;

public sealed class GameApiService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static readonly JsonSerializerOptions CamelWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

    private readonly HttpClient _http;
    private readonly AuthTokenStore _auth;

    public GameApiService(NavigationManager navigation, AuthTokenStore auth)
    {
        _http = new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
        _auth = auth;
    }

    public void Dispose() => _http.Dispose();

    public static string PrettyJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, PrettyOptions);
        }
        catch
        {
            return json;
        }
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativeUri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, relativeUri) { Content = content };
        var session = await _auth.LoadAsync();
        if (session != null && !string.IsNullOrEmpty(session.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return request;
    }

    /// <summary>Gọi API tùy ý (đủ cho các trang công cụ). relativePath: api/v1/...</summary>
    public async Task<(bool Ok, string Body, string? Error)> CallAsync(
        HttpMethod method,
        string relativePath,
        object? jsonBody = null,
        bool useAuth = true)
    {
        var content = jsonBody == null ? null : JsonContent.Create(jsonBody, options: CamelWrite);
        HttpResponseMessage response;
        if (useAuth)
        {
            using var request = await CreateRequestAsync(method, relativePath, content);
            response = await _http.SendAsync(request);
        }
        else
        {
            using var request = new HttpRequestMessage(method, relativePath) { Content = content };
            response = await _http.SendAsync(request);
        }

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, body, TryParseMessage(body) ?? response.ReasonPhrase);
        return (true, body, null);
    }

    public Task<(bool Ok, string Body, string? Error)> PublicGetAsync(string relativePath) =>
        CallAsync(HttpMethod.Get, relativePath, null, useAuth: false);

    public async Task<(bool Ok, string? Error)> RefreshTokenAsync()
    {
        var session = await _auth.LoadAsync();
        if (session == null)
            return (false, "Chưa đăng nhập");

        var body = JsonContent.Create(new RefreshTokenRequest { RefreshToken = session.RefreshToken }, options: CamelWrite);
        using var response = await _http.PostAsync("api/v1/user/refresh-token", body);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, TryParseMessage(json) ?? response.ReasonPhrase);

        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        session.AccessToken = data.GetProperty("token").GetString() ?? session.AccessToken;
        if (data.TryGetProperty("refreshToken", out var rt))
            session.RefreshToken = rt.GetString() ?? session.RefreshToken;
        await _auth.SaveAsync(session);
        return (true, null);
    }

    public async Task<(bool Ok, UserDto? User, string? Error)> GetUserProfileAsync()
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/v1/user/profile");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        using var doc = JsonDocument.Parse(json);
        var user = doc.RootElement.GetProperty("data").Deserialize<UserDto>(JsonOptions);
        return (true, user, null);
    }

    public async Task<(bool Ok, string? Error, PortalAuthState? State)> LoginAsync(string credential, string password)
    {
        var body = JsonContent.Create(
            new LoginDto { login_credential = credential, password = password },
            options: CamelWrite);
        using var response = await _http.PostAsync("api/v1/user/login", body);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, TryParseMessage(json) ?? response.ReasonPhrase, null);

        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var state = new PortalAuthState
        {
            AccessToken = data.GetProperty("token").GetString() ?? "",
            RefreshToken = data.GetProperty("refreshToken").GetString() ?? "",
            UserId = data.GetProperty("user").GetProperty("id").GetGuid(),
            Username = data.GetProperty("user").GetProperty("username").GetString() ?? ""
        };
        await _auth.SaveAsync(state);
        return (true, null, state);
    }

    public async Task<(bool Ok, string? Error)> RegisterAsync(RegisterDto dto)
    {
        var body = JsonContent.Create(dto, options: CamelWrite);
        using var response = await _http.PostAsync("api/v1/user/register", body);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, TryParseMessage(json) ?? response.ReasonPhrase);
        return (true, null);
    }

    public async Task LogoutAsync()
    {
        var session = await _auth.LoadAsync();
        if (session != null && !string.IsNullOrEmpty(session.RefreshToken))
        {
            var body = JsonContent.Create(new RefreshTokenRequest { RefreshToken = session.RefreshToken }, options: CamelWrite);
            using var request = await CreateRequestAsync(HttpMethod.Post, "api/v1/user/logout", body);
            try
            {
                await _http.SendAsync(request);
            }
            catch
            {
                /* ignore */
            }
        }

        await _auth.ClearAsync();
    }

    public async Task<(bool Ok, List<RoomResponseDto> Rooms, string? Error)> GetRoomsAsync()
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/v1/rooms");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, new List<RoomResponseDto>(), TryParseMessage(json));

        using var doc = JsonDocument.Parse(json);
        var rooms = doc.RootElement.GetProperty("data").GetProperty("rooms").Deserialize<List<RoomResponseDto>>(JsonOptions)
            ?? new List<RoomResponseDto>();
        return (true, rooms, null);
    }

    public async Task<(bool Ok, RoomResponseDto? Room, string? Error)> CreateRoomAsync(CreateRoomDto dto)
    {
        var body = JsonContent.Create(dto, options: CamelWrite);
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/v1/rooms", body);
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        using var doc = JsonDocument.Parse(json);
        var room = doc.RootElement.GetProperty("data").GetProperty("room").Deserialize<RoomResponseDto>(JsonOptions);
        return (true, room, null);
    }

    public async Task<(bool Ok, RoomResponseDto? Room, string? Error)> JoinRoomAsync(string roomId, string? pinCode = null)
    {
        var body = JsonContent.Create(new JoinRoomDto { RoomId = roomId, PinCode = pinCode ?? "" }, options: CamelWrite);
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/v1/rooms/join", body);
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        using var doc = JsonDocument.Parse(json);
        var room = doc.RootElement.GetProperty("data").GetProperty("room").Deserialize<RoomResponseDto>(JsonOptions);
        return (true, room, null);
    }

    public async Task<(bool Ok, RoomResponseDto? Room, string? Error)> JoinRoomByPinAsync(string pinCode)
    {
        var body = JsonContent.Create(new JoinRoomByPinDto { PinCode = pinCode }, options: CamelWrite);
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/v1/rooms/join-by-pin", body);
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        using var doc = JsonDocument.Parse(json);
        var room = doc.RootElement.GetProperty("data").GetProperty("room").Deserialize<RoomResponseDto>(JsonOptions);
        return (true, room, null);
    }

    public Task<(bool Ok, string Body, string? Error)> JoinAsSpectatorAsync(string roomId, string? pinCode = null) =>
        CallAsync(HttpMethod.Post, "api/v1/rooms/join-as-spectator",
            new JoinAsSpectatorDto { RoomId = roomId, PinCode = pinCode ?? "" });

    public Task<(bool Ok, string Body, string? Error)> LeaveRoomAsync(string roomId) =>
        CallAsync(HttpMethod.Post, "api/v1/rooms/leave", new LeaveRoomDto { RoomId = roomId });

    public Task<(bool Ok, string Body, string? Error)> DeleteRoomAsync(string roomId) =>
        CallAsync(HttpMethod.Delete, "api/v1/rooms/delete", new DeleteRoomDto { RoomId = roomId });

    public async Task<(bool Ok, RoomResponseDto? Room, string? Error)> GetRoomAsync(string roomId)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/v1/rooms/{Uri.EscapeDataString(roomId)}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        using var doc = JsonDocument.Parse(json);
        var room = doc.RootElement.GetProperty("data").GetProperty("room").Deserialize<RoomResponseDto>(JsonOptions);
        return (true, room, null);
    }

    public Task<(bool Ok, string Body, string? Error)> GetRoomDetailsRawAsync(string roomId) =>
        CallAsync(HttpMethod.Get, $"api/v1/rooms/{Uri.EscapeDataString(roomId)}/details");

    public async Task<(bool Ok, MatchHistoryResponse? Data, string? Error)> GetMatchHistoryAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get,
            $"api/v1/match-history/{userId}?page={page}&pageSize={pageSize}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<MatchHistoryResponse>>(json, JsonOptions);
        if (wrapped?.Success != true || wrapped.Data == null)
            return (false, null, wrapped?.Message ?? "Unknown error");
        return (true, wrapped.Data, null);
    }

    public async Task<(bool Ok, PointTransactionHistoryResponse? Data, string? Error)> GetPointTransactionsAsync(
        Guid userId, int page = 1, int pageSize = 20)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get,
            $"api/v1/match-history/{userId}/transactions?page={page}&pageSize={pageSize}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<PointTransactionHistoryResponse>>(json, JsonOptions);
        if (wrapped?.Success != true || wrapped.Data == null)
            return (false, null, wrapped?.Message ?? "Unknown error");
        return (true, wrapped.Data, null);
    }

    public async Task<(bool Ok, DashboardStatsDto? Data, string? Error)> GetDashboardStatsAsync(Guid userId)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/v1/dashboard/{userId}/stats");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<DashboardStatsDto>>(json, JsonOptions);
        return wrapped?.Success == true ? (true, wrapped.Data, null) : (false, null, wrapped?.Message);
    }

    public async Task<(bool Ok, RecentGamesDto? Data, string? Error)> GetRecentGamesAsync(Guid userId, int limit = 10, int offset = 0)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get,
            $"api/v1/dashboard/{userId}/recent-games?limit={limit}&offset={offset}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<RecentGamesDto>>(json, JsonOptions);
        return wrapped?.Success == true ? (true, wrapped.Data, null) : (false, null, wrapped?.Message);
    }

    public async Task<(bool Ok, AchievementsDto? Data, string? Error)> GetAchievementsAsync(Guid userId, int limit = 20, int offset = 0)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get,
            $"api/v1/dashboard/{userId}/achievements?limit={limit}&offset={offset}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<AchievementsDto>>(json, JsonOptions);
        return wrapped?.Success == true ? (true, wrapped.Data, null) : (false, null, wrapped?.Message);
    }

    public async Task<(bool Ok, OnlinePlayersResponse? Data, string? Error)> GetOnlinePlayersStatsAsync()
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/v1/player-stats/Online");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<OnlinePlayersResponse>>(json, JsonOptions);
        return wrapped?.Success == true ? (true, wrapped.Data, null) : (false, null, wrapped?.Message);
    }

    public async Task<(bool Ok, OnlinePlayersResponse? Data, string? Error)> SearchPlayersStatsAsync(string query)
    {
        var q = Uri.EscapeDataString(query);
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/v1/player-stats/search?query={q}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<OnlinePlayersResponse>>(json, JsonOptions);
        return wrapped?.Success == true ? (true, wrapped.Data, null) : (false, null, wrapped?.Message);
    }

    public async Task<(bool Ok, DetailedPlayerStatsDto? Data, string? Error)> GetDetailedPlayerStatsAsync(Guid playerId)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/v1/player-stats/{playerId}/stats");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<DetailedPlayerStatsDto>>(json, JsonOptions);
        return wrapped?.Success == true ? (true, wrapped.Data, null) : (false, null, wrapped?.Message);
    }

    public async Task<(bool Ok, List<LeaderBoardDto>? Data, string? Error)> GetLeaderboardAsync()
    {
        using var response = await _http.GetAsync("api/v1/leaderboard");
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var list = JsonSerializer.Deserialize<List<LeaderBoardDto>>(json, JsonOptions);
        return (true, list, null);
    }

    public async Task<(bool Ok, List<LeaderBoardDto>? Data, string? Error)> GetLeaderboardTopAsync(int top = 5)
    {
        using var response = await _http.GetAsync($"api/v1/leaderboard/top");
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var list = JsonSerializer.Deserialize<List<LeaderBoardDto>>(json, JsonOptions);
        return (true, list?.Take(top).ToList(), null);
    }

    public Task<(bool Ok, string Body, string? Error)> GetMyLeaderboardRankAsync(Guid userId) =>
        CallAsync(HttpMethod.Get, $"api/v1/leaderboard/player/{userId}/rank");

    public async Task<(bool Ok, CompleteProfileDto? Data, string? Error)> GetCompleteProfileAsync(Guid userId)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/v1/profile/{userId}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<CompleteProfileDto>>(json, JsonOptions);
        return wrapped?.Success == true ? (true, wrapped.Data, null) : (false, null, wrapped?.Message);
    }

    public Task<(bool Ok, string Body, string? Error)> GetProfileStatsRawAsync(Guid userId) =>
        CallAsync(HttpMethod.Get, $"api/v1/profile/{userId}/stats");

    public async Task<(bool Ok, GameHistoryResponseDto? Data, string? Error)> GetProfileHistoryAsync(Guid userId, int limit = 20, int offset = 0)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get,
            $"api/v1/profile/{userId}/history?limit={limit}&offset={offset}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, null, TryParseMessage(json));

        var wrapped = JsonSerializer.Deserialize<ApiResponse<GameHistoryResponseDto>>(json, JsonOptions);
        return wrapped?.Success == true ? (true, wrapped.Data, null) : (false, null, wrapped?.Message);
    }

    public async Task<(bool Ok, long Points, string? Error)> GetCurrentPointsAsync()
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/v1/points/current");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, 0, TryParseMessage(json));

        using var doc = JsonDocument.Parse(json);
        var pts = ReadPointsNumber(doc.RootElement.GetProperty("data").GetProperty("points"));
        return (true, pts, null);
    }

    public async Task<(bool Ok, long Points, string? Error)> GetUserPointsAsync(Guid userId)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"api/v1/points/{userId}");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return (false, 0, TryParseMessage(json));

        using var doc = JsonDocument.Parse(json);
        var pts = ReadPointsNumber(doc.RootElement.GetProperty("data").GetProperty("points"));
        return (true, pts, null);
    }

    private static long ReadPointsNumber(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : (long)el.GetDouble(),
        _ => 0
    };

    public Task<(bool Ok, string Body, string? Error)> GetGlobalChatMessagesAsync(int limit = 100) =>
        CallAsync(HttpMethod.Get, $"api/v1/chat/global/messages?limit={limit}", null, useAuth: false);

    public async Task<(bool Ok, string Body, string? Error)> SendGlobalChatAsync(string content)
    {
        var session = await _auth.LoadAsync();
        if (session == null)
            return (false, "", "Cần đăng nhập để gửi chat (API yêu cầu UserId).");

        var dto = new SendMessageRequest
        {
            UserId = session.UserId.ToString(),
            Username = session.Username,
            Content = content
        };
        return await CallAsync(HttpMethod.Post, "api/v1/chat/global/messages", dto, useAuth: false);
    }

    public Task<(bool Ok, string Body, string? Error)> GetActivityFeedAsync(int limit = 20) =>
        CallAsync(HttpMethod.Get, $"api/v1/chat/activity-feed?limit={limit}", null, useAuth: false);

    public Task<(bool Ok, string Body, string? Error)> GetRoomChatMessagesAsync(string roomId, int limit = 100) =>
        CallAsync(HttpMethod.Get, $"api/v1/chat/rooms/{Uri.EscapeDataString(roomId)}/messages?limit={limit}");

    public async Task<(bool Ok, string Body, string? Error)> SendRoomChatAsync(string roomId, string content)
    {
        var session = await _auth.LoadAsync();
        if (session == null)
            return (false, "", "Chưa đăng nhập");

        var dto = new SendMessageRequest
        {
            UserId = session.UserId.ToString(),
            Username = session.Username,
            Content = content
        };
        return await CallAsync(HttpMethod.Post, $"api/v1/chat/rooms/{Uri.EscapeDataString(roomId)}/messages", dto);
    }

    public Task<(bool Ok, string Body, string? Error)> GameStartAsync(string roomId) =>
        CallAsync(HttpMethod.Post, $"api/v1/game/rooms/{Uri.EscapeDataString(roomId)}/start");

    public Task<(bool Ok, string Body, string? Error)> GameSetReadyAsync(string roomId, bool ready) =>
        CallAsync(HttpMethod.Post, $"api/v1/game/rooms/{Uri.EscapeDataString(roomId)}/ready", new SetReadyRequest { IsReady = ready });

    public Task<(bool Ok, string Body, string? Error)> GameSubmitMoveAsync(string roomId, string move) =>
        CallAsync(HttpMethod.Post, $"api/v1/game/rooms/{Uri.EscapeDataString(roomId)}/move", new SubmitMoveRequest { Move = move });

    public Task<(bool Ok, string Body, string? Error)> GameEndAsync(string roomId, string? winnerId) =>
        CallAsync(HttpMethod.Post, $"api/v1/game/rooms/{Uri.EscapeDataString(roomId)}/end", new EndGameRequest { WinnerId = winnerId });

    public Task<(bool Ok, string Body, string? Error)> GameGetStateAsync(string roomId) =>
        CallAsync(HttpMethod.Get, $"api/v1/game/rooms/{Uri.EscapeDataString(roomId)}/state");

    public Task<(bool Ok, string Body, string? Error)> GameGetReadyStatesAsync(string roomId) =>
        CallAsync(HttpMethod.Get, $"api/v1/game/rooms/{Uri.EscapeDataString(roomId)}/ready-states");

    public Task<(bool Ok, string Body, string? Error)> GameProcessRoundAsync(string roomId) =>
        CallAsync(HttpMethod.Post, $"api/v1/game/rooms/{Uri.EscapeDataString(roomId)}/round/result");

    public Task<(bool Ok, string Body, string? Error)> GameProcessResultAsync(string roomId, string? winnerId) =>
        CallAsync(HttpMethod.Post, $"api/v1/game/rooms/{Uri.EscapeDataString(roomId)}/result", new GameResultRequest { WinnerId = winnerId });

    public Task<(bool Ok, string Body, string? Error)> BettingPlaceAsync(string roomId, decimal amount, string targetPlayerId, string? pinCode = null) =>
        CallAsync(HttpMethod.Post, $"api/v1/Betting/rooms/{Uri.EscapeDataString(roomId)}/place-bet",
            new PlaceBetRequest { Amount = amount, TargetPlayerId = targetPlayerId, PinCode = string.IsNullOrWhiteSpace(pinCode) ? null : pinCode.Trim() });

    public Task<(bool Ok, string Body, string? Error)> BettingGetPoolAsync(string roomId) =>
        CallAsync(HttpMethod.Get, $"api/v1/Betting/rooms/{Uri.EscapeDataString(roomId)}/pool");

    public Task<(bool Ok, string Body, string? Error)> BettingGetStatisticsAsync(string roomId) =>
        CallAsync(HttpMethod.Get, $"api/v1/Betting/rooms/{Uri.EscapeDataString(roomId)}/statistics");

    public Task<(bool Ok, string Body, string? Error)> PlayersLegacyOnlineAsync() =>
        CallAsync(HttpMethod.Get, "api/v1/players/Online", null, useAuth: false);

    public Task<(bool Ok, string Body, string? Error)> PlayersLegacySearchAsync(string name) =>
        CallAsync(HttpMethod.Get, $"api/v1/players/search?Name={Uri.EscapeDataString(name)}", null, useAuth: false);

    public Task<(bool Ok, string Body, string? Error)> PlayersLegacyStatsAsync(string playerId) =>
        CallAsync(HttpMethod.Get, $"api/v1/players/{Uri.EscapeDataString(playerId)}/stats", null, useAuth: false);

    public Task<(bool Ok, string Body, string? Error)> PlayersInviteAsync(string playerId, string message) =>
        CallAsync(HttpMethod.Post, $"api/v1/players/{Uri.EscapeDataString(playerId)}/invite", new InviteRequestDto { Message = message });

    public Task<(bool Ok, string Body, string? Error)> PlayersGetInvitationsAsync(string playerId) =>
        CallAsync(HttpMethod.Get, $"api/v1/players/{Uri.EscapeDataString(playerId)}/invitations");

    public Task<(bool Ok, string Body, string? Error)> RoomMgmtStatsAsync() =>
        CallAsync(HttpMethod.Get, "api/v1/room-management/stats");

    public Task<(bool Ok, string Body, string? Error)> RoomMgmtByStatusAsync(string status) =>
        CallAsync(HttpMethod.Get, $"api/v1/room-management/rooms/by-status/{Uri.EscapeDataString(status)}");

    public Task<(bool Ok, string Body, string? Error)> RoomMgmtByCreatorAsync(string creatorId) =>
        CallAsync(HttpMethod.Get, $"api/v1/room-management/rooms/by-creator/{Uri.EscapeDataString(creatorId)}");

    public Task<(bool Ok, string Body, string? Error)> RoomMgmtCleanupInactiveAsync() =>
        CallAsync(HttpMethod.Post, "api/v1/room-management/cleanup/inactive");

    public Task<(bool Ok, string Body, string? Error)> RoomMgmtForceCloseAsync(string roomId, string reason) =>
        CallAsync(HttpMethod.Post, $"api/v1/room-management/rooms/{Uri.EscapeDataString(roomId)}/force-close",
            new ForceCloseRequest { Reason = reason });

    public Task<(bool Ok, string Body, string? Error)> RoomMgmtLowActivityAsync(int minutesThreshold = 30) =>
        CallAsync(HttpMethod.Get, $"api/v1/room-management/rooms/low-activity?minutesThreshold={minutesThreshold}");

    public Task<(bool Ok, string Body, string? Error)> RoomMgmtRoomHealthAsync(string roomId) =>
        CallAsync(HttpMethod.Get, $"api/v1/room-management/rooms/{Uri.EscapeDataString(roomId)}/health");

    public Task<(bool Ok, string Body, string? Error)> RoomMgmtTransferOwnershipAsync(string roomId, string toUserId) =>
        CallAsync(HttpMethod.Post, $"api/v1/room-management/rooms/{Uri.EscapeDataString(roomId)}/transfer-ownership",
            new TransferOwnershipRequest { ToUserId = toUserId });

    public Task<(bool Ok, string Body, string? Error)> HealthBasicAsync() => PublicGetAsync("api/v1/health");
    public Task<(bool Ok, string Body, string? Error)> HealthDeepAsync() => PublicGetAsync("api/v1/health/deep");
    public Task<(bool Ok, string Body, string? Error)> HealthReadyAsync() => PublicGetAsync("api/v1/health/ready");
    public Task<(bool Ok, string Body, string? Error)> HealthLiveAsync() => PublicGetAsync("api/v1/health/live");
    public Task<(bool Ok, string Body, string? Error)> HealthStatusAsync() => PublicGetAsync("api/v1/health/status");
    public Task<(bool Ok, string Body, string? Error)> HealthStatusDetailedAsync() => PublicGetAsync("api/v1/health/status/detailed");
    public Task<(bool Ok, string Body, string? Error)> HealthMetricsAsync() => PublicGetAsync("api/v1/health/metrics");
    public Task<(bool Ok, string Body, string? Error)> HealthVersionAsync() => PublicGetAsync("api/v1/health/version");

    private static string? TryParseMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var m))
                return m.GetString();
        }
        catch
        {
            /* ignore */
        }

        return null;
    }
}

namespace Rock_Paper_Scissors_Online.Utilities;

/// <summary>
/// Best-of-N: cần ceil(N/2) lần thắng có điểm; đồng thời không vượt quá N hiệp (mỗi hiệp gồm cả hòa) — tránh kéo 5–6 ván khi chỉ chọn 3 hiệp.
/// </summary>
public static class RpsMatchRules
{
    public static int RequiredWins(int bestOfRounds)
    {
        var n = Math.Max(1, bestOfRounds);
        return (n + 1) / 2;
    }

    /// <param name="currentRound">Số hiệp đã hoàn thành (sau khi cộng 1 cho ván vừa xong).</param>
    public static bool IsMatchOver(int bestOfRounds, int currentRound, int p1Score, int p2Score)
    {
        var cap = Math.Max(1, bestOfRounds);
        var req = RequiredWins(cap);
        return p1Score >= req || p2Score >= req || currentRound >= cap;
    }

    /// <param name="lastRoundWinner">player1Id, player2Id, hoặc "tie" (GameHub).</param>
    public static string ResolveWinnerUserId(string player1Id, string player2Id, int p1Score, int p2Score, string lastRoundWinner)
    {
        if (p1Score > p2Score) return player1Id;
        if (p2Score > p1Score) return player2Id;
        if (lastRoundWinner != "tie" && (lastRoundWinner == player1Id || lastRoundWinner == player2Id))
            return lastRoundWinner;
        return player1Id;
    }
}

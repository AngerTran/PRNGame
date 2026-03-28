using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Rock_Paper_Scissors_Online.Models;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Bet> Bets { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<History> Histories { get; set; }

    public virtual DbSet<PointTransaction> PointTransactions { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bet>(entity =>
        {
            entity.ToTable("bets");

            entity.HasIndex(e => e.GameId, "idx_bets_game");

            entity.HasIndex(e => e.PlayerId, "idx_bets_player");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.GameId)
                .HasMaxLength(255)
                .HasColumnName("game_id");
            entity.Property(e => e.Payout)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("payout");
            entity.Property(e => e.PlayerId)
                .HasMaxLength(255)
                .HasColumnName("player_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("pending")
                .HasColumnName("status");
            entity.Property(e => e.TargetPlayerId)
                .HasMaxLength(255)
                .HasColumnName("target_player_id");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("(sysdatetimeoffset())")
                .HasColumnName("timestamp");

            entity.HasOne(d => d.Game).WithMany(p => p.Bets)
                .HasForeignKey(d => d.GameId)
                .HasConstraintName("FK_bets_rooms");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");

            entity.HasIndex(e => new { e.RoomId, e.Timestamp }, "idx_chat_room_time").IsDescending(false, true);

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.RoomId)
                .HasMaxLength(255)
                .HasDefaultValue("")
                .HasColumnName("room_id");
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("(sysdatetimeoffset())")
                .HasColumnName("timestamp");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasDefaultValue("user")
                .HasColumnName("type");
            entity.Property(e => e.UserId)
                .HasMaxLength(255)
                .HasColumnName("user_id");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasColumnName("username");
        });

        modelBuilder.Entity<History>(entity =>
        {
            entity.ToTable("History");

            entity.HasIndex(e => e.CreatedAt, "idx_history_created_at").IsDescending();

            entity.HasIndex(e => e.CreatorUserId, "idx_history_creator");

            entity.HasIndex(e => e.OpponentId, "idx_history_opponent");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatorUserId).HasColumnName("creator_user_id");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.MaxRounds).HasColumnName("max_rounds");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.OpponentId).HasColumnName("opponent_id");
            entity.Property(e => e.OpponentScore).HasColumnName("opponent_score");
            entity.Property(e => e.Points).HasColumnName("points");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Waiting")
                .HasColumnName("status");

            entity.HasOne(d => d.CreatorUser).WithMany(p => p.HistoryCreatorUsers)
                .HasForeignKey(d => d.CreatorUserId)
                .HasConstraintName("FK_History_Users_creator");

            entity.HasOne(d => d.Opponent).WithMany(p => p.HistoryOpponents)
                .HasForeignKey(d => d.OpponentId)
                .HasConstraintName("FK_History_Users_opponent");
        });

        modelBuilder.Entity<PointTransaction>(entity =>
        {
            entity.ToTable("Point_transactions");

            entity.HasIndex(e => e.CreatedAt, "idx_point_tx_created").IsDescending();

            entity.HasIndex(e => e.HistoryId, "idx_point_tx_history");

            entity.HasIndex(e => e.UserId, "idx_point_tx_user");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Delta).HasColumnName("delta");
            entity.Property(e => e.HistoryId).HasColumnName("history_id");
            entity.Property(e => e.Reason)
                .HasMaxLength(255)
                .HasColumnName("reason");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.History).WithMany(p => p.PointTransactions)
                .HasForeignKey(d => d.HistoryId)
                .HasConstraintName("FK_Point_transactions_History");

            entity.HasOne(d => d.User).WithMany(p => p.PointTransactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Point_transactions_Users");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasIndex(e => e.Token, "idx_refresh_tokens_token");

            entity.HasIndex(e => e.UserId, "idx_refresh_tokens_user");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.Token).HasColumnName("token");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_refresh_tokens_Users");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("rooms");

            entity.HasIndex(e => e.Name, "UQ_rooms_name").IsUnique();

            entity.HasIndex(e => e.CreatedAt, "idx_rooms_created_at").IsDescending();

            entity.HasIndex(e => e.CreatedBy, "idx_rooms_created_by");

            entity.HasIndex(e => e.Status, "idx_rooms_status");

            entity.Property(e => e.Id)
                .HasMaxLength(255)
                .HasColumnName("id");
            entity.Property(e => e.AllowBetting).HasColumnName("allow_betting");
            entity.Property(e => e.AllowSpectators)
                .HasDefaultValue(true)
                .HasColumnName("allow_spectators");
            entity.Property(e => e.BestOfRounds).HasColumnName("best_of_rounds");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetimeoffset())")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(255)
                .HasColumnName("created_by");
            entity.Property(e => e.CreatedByDisplayName)
                .HasMaxLength(255)
                .HasDefaultValue("")
                .HasColumnName("created_by_display_name");
            entity.Property(e => e.CreatedByUsername)
                .HasMaxLength(255)
                .HasColumnName("created_by_username");
            entity.Property(e => e.CurrentPlayers).HasColumnName("current_players");
            entity.Property(e => e.CurrentRound).HasColumnName("current_round");
            entity.Property(e => e.GameState).HasColumnName("game_state");
            entity.Property(e => e.IsPrivate).HasColumnName("is_private");
            entity.Property(e => e.MaxPlayers).HasColumnName("max_players");
            entity.Property(e => e.MaxSpectators)
                .HasDefaultValue(50)
                .HasColumnName("max_spectators");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.PinCode)
                .HasMaxLength(50)
                .HasColumnName("pin_code");
            entity.Property(e => e.PointsPerWin).HasColumnName("points_per_win");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Waiting")
                .HasColumnName("status");
            entity.Property(e => e.TimeoutAt).HasColumnName("timeout_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "IX_Users_email").IsUnique();

            entity.HasIndex(e => e.Username, "IX_Users_username").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Avatar)
                .HasDefaultValue(1)
                .HasColumnName("avatar");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CurrentWinStreak).HasColumnName("current_win_streak");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255)
                .HasColumnName("display_name");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.LastPlayedAt).HasColumnName("last_played_at");
            entity.Property(e => e.LongestWinStreak).HasColumnName("longest_win_streak");
            entity.Property(e => e.Losses).HasColumnName("losses");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Points)
                .HasDefaultValue(1000L)
                .HasColumnName("points");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasDefaultValue("User")
                .HasColumnName("role");
            entity.Property(e => e.Ties).HasColumnName("ties");
            entity.Property(e => e.TotalGames).HasColumnName("total_games");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(255)
                .HasColumnName("username");
            entity.Property(e => e.Wins).HasColumnName("wins");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

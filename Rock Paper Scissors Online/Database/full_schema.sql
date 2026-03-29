-- RPS Online — schema SQL Server đầy đủ (khớp ApplicationDbContext / EF Core)
-- Tạo lại bằng: dotnet ef dbcontext script --output Database/full_schema.sql
-- (cần package Microsoft.EntityFrameworkCore.Design và dotnet-ef tool)
--
-- USE [RockPaperScissors];
-- GO

CREATE TABLE [chat_messages] (
    [id] nvarchar(255) NOT NULL,
    [room_id] nvarchar(255) NOT NULL DEFAULT N'',
    [user_id] nvarchar(255) NOT NULL,
    [username] nvarchar(255) NOT NULL,
    [content] nvarchar(max) NOT NULL,
    [type] nvarchar(50) NOT NULL DEFAULT N'user',
    [timestamp] datetimeoffset NOT NULL DEFAULT ((sysdatetimeoffset())),
    CONSTRAINT [PK_chat_messages] PRIMARY KEY ([id])
);
GO


CREATE TABLE [rooms] (
    [id] nvarchar(255) NOT NULL,
    [name] nvarchar(255) NOT NULL,
    [max_players] int NOT NULL,
    [current_players] int NOT NULL,
    [is_private] bit NOT NULL,
    [pin_code] nvarchar(50) NULL,
    [best_of_rounds] int NOT NULL,
    [points_per_win] int NOT NULL,
    [allow_spectators] bit NOT NULL DEFAULT CAST(1 AS bit),
    [max_spectators] int NOT NULL DEFAULT 50,
    [allow_betting] bit NOT NULL,
    [status] nvarchar(50) NOT NULL DEFAULT N'Waiting',
    [created_at] datetimeoffset NOT NULL DEFAULT ((sysdatetimeoffset())),
    [created_by] nvarchar(255) NOT NULL,
    [created_by_username] nvarchar(255) NOT NULL,
    [created_by_display_name] nvarchar(255) NOT NULL DEFAULT N'',
    [current_round] int NOT NULL,
    [timeout_at] datetimeoffset NULL,
    [game_state] nvarchar(max) NULL,
    CONSTRAINT [PK_rooms] PRIMARY KEY ([id])
);
GO


CREATE TABLE [Users] (
    [id] uniqueidentifier NOT NULL,
    [username] nvarchar(255) NOT NULL,
    [password_hash] nvarchar(max) NOT NULL,
    [email] nvarchar(255) NOT NULL,
    [display_name] nvarchar(255) NULL,
    [avatar] int NOT NULL DEFAULT 1,
    [points] bigint NOT NULL DEFAULT CAST(1000 AS bigint),
    [role] nvarchar(50) NOT NULL DEFAULT N'User',
    [total_games] int NOT NULL,
    [wins] int NOT NULL,
    [losses] int NOT NULL,
    [ties] int NOT NULL,
    [current_win_streak] int NOT NULL,
    [longest_win_streak] int NOT NULL,
    [last_played_at] datetimeoffset NULL,
    [created_at] datetimeoffset NOT NULL,
    [updated_at] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([id])
);
GO


CREATE TABLE [bets] (
    [id] nvarchar(255) NOT NULL,
    [game_id] nvarchar(255) NOT NULL,
    [player_id] nvarchar(255) NOT NULL,
    [target_player_id] nvarchar(255) NOT NULL,
    [amount] decimal(18,2) NOT NULL,
    [timestamp] datetimeoffset NOT NULL DEFAULT ((sysdatetimeoffset())),
    [status] nvarchar(50) NOT NULL DEFAULT N'pending',
    [payout] decimal(18,2) NULL,
    CONSTRAINT [PK_bets] PRIMARY KEY ([id]),
    CONSTRAINT [FK_bets_rooms] FOREIGN KEY ([game_id]) REFERENCES [rooms] ([id]) ON DELETE CASCADE
);
GO


CREATE TABLE [History] (
    [id] uniqueidentifier NOT NULL,
    [name] nvarchar(255) NOT NULL,
    [creator_user_id] uniqueidentifier NULL,
    [max_rounds] int NOT NULL,
    [points] int NOT NULL,
    [status] nvarchar(50) NOT NULL DEFAULT N'Waiting',
    [opponent_id] uniqueidentifier NULL,
    [opponent_score] int NOT NULL,
    [started_at] datetimeoffset NOT NULL,
    [finished_at] datetimeoffset NOT NULL,
    [created_at] datetimeoffset NOT NULL,
    CONSTRAINT [PK_History] PRIMARY KEY ([id]),
    CONSTRAINT [FK_History_Users_creator] FOREIGN KEY ([creator_user_id]) REFERENCES [Users] ([id]),
    CONSTRAINT [FK_History_Users_opponent] FOREIGN KEY ([opponent_id]) REFERENCES [Users] ([id])
);
GO


CREATE TABLE [refresh_tokens] (
    [id] uniqueidentifier NOT NULL,
    [user_id] uniqueidentifier NOT NULL,
    [token] nvarchar(450) NOT NULL,
    [expires_at] datetimeoffset NOT NULL,
    [created_at] datetimeoffset NOT NULL,
    CONSTRAINT [PK_refresh_tokens] PRIMARY KEY ([id]),
    CONSTRAINT [FK_refresh_tokens_Users] FOREIGN KEY ([user_id]) REFERENCES [Users] ([id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Point_transactions] (
    [id] bigint NOT NULL IDENTITY,
    [user_id] uniqueidentifier NOT NULL,
    [delta] bigint NOT NULL,
    [reason] nvarchar(255) NOT NULL,
    [type] nvarchar(50) NOT NULL,
    [history_id] uniqueidentifier NULL,
    [created_at] datetimeoffset NOT NULL,
    CONSTRAINT [PK_Point_transactions] PRIMARY KEY ([id]),
    CONSTRAINT [FK_Point_transactions_History] FOREIGN KEY ([history_id]) REFERENCES [History] ([id]),
    CONSTRAINT [FK_Point_transactions_Users] FOREIGN KEY ([user_id]) REFERENCES [Users] ([id])
);
GO


CREATE INDEX [idx_bets_game] ON [bets] ([game_id]);
GO


CREATE INDEX [idx_bets_player] ON [bets] ([player_id]);
GO


CREATE INDEX [idx_chat_room_time] ON [chat_messages] ([room_id], [timestamp] DESC);
GO


CREATE INDEX [idx_history_created_at] ON [History] ([created_at] DESC);
GO


CREATE INDEX [idx_history_creator] ON [History] ([creator_user_id]);
GO


CREATE INDEX [idx_history_opponent] ON [History] ([opponent_id]);
GO


CREATE INDEX [idx_point_tx_created] ON [Point_transactions] ([created_at] DESC);
GO


CREATE INDEX [idx_point_tx_history] ON [Point_transactions] ([history_id]);
GO


CREATE INDEX [idx_point_tx_user] ON [Point_transactions] ([user_id]);
GO


CREATE INDEX [idx_refresh_tokens_token] ON [refresh_tokens] ([token]);
GO


CREATE INDEX [idx_refresh_tokens_user] ON [refresh_tokens] ([user_id]);
GO


CREATE INDEX [idx_rooms_created_at] ON [rooms] ([created_at] DESC);
GO


CREATE INDEX [idx_rooms_created_by] ON [rooms] ([created_by]);
GO


CREATE INDEX [idx_rooms_status] ON [rooms] ([status]);
GO


CREATE UNIQUE INDEX [UQ_rooms_name] ON [rooms] ([name]);
GO


CREATE UNIQUE INDEX [IX_Users_email] ON [Users] ([email]);
GO


CREATE UNIQUE INDEX [IX_Users_username] ON [Users] ([username]);
GO

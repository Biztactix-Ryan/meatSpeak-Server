using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeatSpeak.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMsgIdAndRedactionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    TopicSetBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TopicSetAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    Modes = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "chat_logs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Target = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Sender = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    MessageType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SentAt = table.Column<long>(type: "INTEGER", nullable: false),
                    MsgId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsRedacted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    RedactedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RedactedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MsgId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Sender = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Reaction = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    ServerPermissions = table.Column<ulong>(type: "INTEGER", nullable: false),
                    DefaultChannelPermissions = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "server_bans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mask = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SetBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SetAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_bans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "topic_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SetBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SetAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Account = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ConnectedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DisconnectedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    QuitReason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "channel_overrides",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Allow = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Deny = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_overrides", x => new { x.RoleId, x.ChannelName });
                    table.ForeignKey(
                        name: "FK_channel_overrides_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    Account = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.Account, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_Actor",
                table: "audit_log",
                column: "Actor");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_Timestamp",
                table: "audit_log",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_channels_Name",
                table: "channels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_ChannelName",
                table: "chat_logs",
                column: "ChannelName");

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_MsgId",
                table: "chat_logs",
                column: "MsgId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_Sender",
                table: "chat_logs",
                column: "Sender");

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_SentAt",
                table: "chat_logs",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_reactions_MsgId",
                table: "reactions",
                column: "MsgId");

            migrationBuilder.CreateIndex(
                name: "IX_reactions_MsgId_Sender_Reaction",
                table: "reactions",
                columns: new[] { "MsgId", "Sender", "Reaction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_Name",
                table: "roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_server_bans_Mask",
                table: "server_bans",
                column: "Mask");

            migrationBuilder.CreateIndex(
                name: "IX_topic_history_ChannelName",
                table: "topic_history",
                column: "ChannelName");

            migrationBuilder.CreateIndex(
                name: "IX_user_history_Account",
                table: "user_history",
                column: "Account");

            migrationBuilder.CreateIndex(
                name: "IX_user_history_Nickname",
                table: "user_history",
                column: "Nickname");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "channel_overrides");

            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "chat_logs");

            migrationBuilder.DropTable(
                name: "reactions");

            migrationBuilder.DropTable(
                name: "server_bans");

            migrationBuilder.DropTable(
                name: "topic_history");

            migrationBuilder.DropTable(
                name: "user_history");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}

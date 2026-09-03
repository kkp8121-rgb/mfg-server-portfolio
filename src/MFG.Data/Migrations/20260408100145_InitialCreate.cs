using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace MFG.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "guilds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    guild_name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    level = table.Column<int>(type: "int", nullable: false),
                    exp = table.Column<int>(type: "int", nullable: false),
                    leader_id = table.Column<long>(type: "bigint", nullable: false),
                    boss_hp_remaining = table.Column<long>(type: "bigint", nullable: false),
                    is_boss_defeated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    last_boss_reset_date = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guilds", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    firebase_uid = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    nickname = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    level = table.Column<int>(type: "int", nullable: false),
                    exp = table.Column<long>(type: "bigint", nullable: false),
                    job_id = table.Column<string>(type: "longtext", nullable: false),
                    job_tier = table.Column<int>(type: "int", nullable: false),
                    combat_power = table.Column<long>(type: "bigint", nullable: false),
                    current_floor = table.Column<int>(type: "int", nullable: false),
                    max_floor = table.Column<int>(type: "int", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_logout_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "arena_players",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    current_tier = table.Column<int>(type: "int", nullable: false),
                    rating = table.Column<int>(type: "int", nullable: false),
                    total_victories = table.Column<int>(type: "int", nullable: false),
                    total_defeats = table.Column<int>(type: "int", nullable: false),
                    current_win_streak = table.Column<int>(type: "int", nullable: false),
                    best_win_streak = table.Column<int>(type: "int", nullable: false),
                    used_free_entries = table.Column<int>(type: "int", nullable: false),
                    last_reset_date = table.Column<DateOnly>(type: "date", nullable: false),
                    season_id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    season_start_time = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_players", x => x.id);
                    table.UniqueConstraint("ak_arena_players_player_id", x => x.player_id);
                    table.ForeignKey(
                        name: "fk_arena_players_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "attendance_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    check_date = table.Column<DateOnly>(type: "date", nullable: false),
                    consecutive_days = table.Column<int>(type: "int", nullable: false),
                    reward_type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    reward_amount = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_records_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currencies", x => x.id);
                    table.ForeignKey(
                        name: "fk_currencies_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "currency_transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    currency_type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    reference_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currency_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_currency_transactions_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gacha_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    pool_type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    result_item_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    result_grade = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    pity_count = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gacha_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_gacha_histories_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "gacha_pities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    pool_type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    total_pulls = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gacha_pities", x => x.id);
                    table.ForeignKey(
                        name: "fk_gacha_pities_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "guild_members",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    guild_id = table.Column<long>(type: "bigint", nullable: false),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    gold_donate_count = table.Column<int>(type: "int", nullable: false),
                    ruby_donate_count = table.Column<int>(type: "int", nullable: false),
                    ticket_donate_count = table.Column<int>(type: "int", nullable: false),
                    last_donate_reset_date = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    boss_attempts_this_week = table.Column<int>(type: "int", nullable: false),
                    boss_total_damage = table.Column<long>(type: "bigint", nullable: false),
                    raid_used_this_week = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    joined_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guild_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_guild_members_guilds_guild_id",
                        column: x => x.guild_id,
                        principalTable: "guilds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_guild_members_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "iap_receipts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    platform = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    product_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    receipt_hash = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    is_valid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_iap_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_iap_receipts_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "progress_data",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    save_json = table.Column<string>(type: "json", nullable: false),
                    version = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progress_data", x => x.id);
                    table.ForeignKey(
                        name: "fk_progress_data_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "arena_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    player_id = table.Column<long>(type: "bigint", nullable: false),
                    opponent_name = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    opponent_job = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    opponent_cp = table.Column<long>(type: "bigint", nullable: false),
                    is_victory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    rating_change = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_arena_records_arena_players_player_id",
                        column: x => x.player_id,
                        principalTable: "arena_players",
                        principalColumn: "player_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_arena_players_player_id",
                table: "arena_players",
                column: "player_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_arena_records_player_id_created_at",
                table: "arena_records",
                columns: new[] { "player_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_player_id_check_date",
                table: "attendance_records",
                columns: new[] { "player_id", "check_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currencies_player_id_type",
                table: "currencies",
                columns: new[] { "player_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_currency_transactions_player_id_created_at",
                table: "currency_transactions",
                columns: new[] { "player_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gacha_histories_player_id_created_at",
                table: "gacha_histories",
                columns: new[] { "player_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gacha_pities_player_id_pool_type",
                table: "gacha_pities",
                columns: new[] { "player_id", "pool_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guild_members_guild_id",
                table: "guild_members",
                column: "guild_id");

            migrationBuilder.CreateIndex(
                name: "ix_guild_members_player_id",
                table: "guild_members",
                column: "player_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guilds_guild_name",
                table: "guilds",
                column: "guild_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_iap_receipts_player_id",
                table: "iap_receipts",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_iap_receipts_receipt_hash",
                table: "iap_receipts",
                column: "receipt_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_players_firebase_uid",
                table: "players",
                column: "firebase_uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_progress_data_player_id",
                table: "progress_data",
                column: "player_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arena_records");

            migrationBuilder.DropTable(
                name: "attendance_records");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "currency_transactions");

            migrationBuilder.DropTable(
                name: "gacha_histories");

            migrationBuilder.DropTable(
                name: "gacha_pities");

            migrationBuilder.DropTable(
                name: "guild_members");

            migrationBuilder.DropTable(
                name: "iap_receipts");

            migrationBuilder.DropTable(
                name: "progress_data");

            migrationBuilder.DropTable(
                name: "arena_players");

            migrationBuilder.DropTable(
                name: "guilds");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}

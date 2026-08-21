using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenS4L.Database.Migrations.Auth
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Nickname = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Password = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Salt = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SecurityLevel = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<long>(type: "bigint", nullable: false),
                    Duration = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bans_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "login_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<long>(type: "bigint", nullable: false),
                    IP = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_login_history_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nickname_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExpireDate = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nickname_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nickname_history_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Nickname",
                table: "accounts",
                column: "Nickname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Username",
                table: "accounts",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bans_AccountId",
                table: "bans",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_login_history_AccountId",
                table: "login_history",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_nickname_history_AccountId",
                table: "nickname_history",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bans");

            migrationBuilder.DropTable(
                name: "login_history");

            migrationBuilder.DropTable(
                name: "nickname_history");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}

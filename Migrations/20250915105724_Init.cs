using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trecom.Backend.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_heads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_heads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_aliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_aliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_aliases_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_revisions",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AMId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Margin = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProbabilityPercent = table.Column<int>(type: "int", nullable: false),
                    DueQuarter = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    InvoiceMonth = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    PaymentQuarter = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchitectureId = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCanceled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_revisions", x => new { x.ProjectId, x.Version });
                    table.ForeignKey(
                        name: "FK_project_revisions_project_heads_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "project_heads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_participants_rev",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsOwner = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_participants_rev", x => new { x.ProjectId, x.Version, x.UserId });
                    table.ForeignKey(
                        name: "FK_project_participants_rev_project_revisions_ProjectId_Version",
                        columns: x => new { x.ProjectId, x.Version },
                        principalTable: "project_revisions",
                        principalColumns: new[] { "ProjectId", "Version" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LeaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    xmin = table.Column<long>(type: "bigint", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_users_users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_aliases_Alias",
                table: "client_aliases",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_aliases_ClientId",
                table: "client_aliases",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_clients_CanonicalName",
                table: "clients",
                column: "CanonicalName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_participants_rev_UserId",
                table: "project_participants_rev",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_AMId",
                table: "project_revisions",
                column: "AMId");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_ArchitectureId",
                table: "project_revisions",
                column: "ArchitectureId");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_ClientId",
                table: "project_revisions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_DueQuarter",
                table: "project_revisions",
                column: "DueQuarter");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_EffectiveAt",
                table: "project_revisions",
                column: "EffectiveAt");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_InvoiceMonth",
                table: "project_revisions",
                column: "InvoiceMonth");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_IsCanceled",
                table: "project_revisions",
                column: "IsCanceled");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_MarketId",
                table: "project_revisions",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_ProjectId_EffectiveAt",
                table: "project_revisions",
                columns: new[] { "ProjectId", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_StatusId",
                table: "project_revisions",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_project_revisions_VendorId",
                table: "project_revisions",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_LeaderId",
                table: "teams",
                column: "LeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_teams_Name",
                table: "teams",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_ManagerId",
                table: "users",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_users_TeamId",
                table: "users",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_project_participants_rev_users_UserId",
                table: "project_participants_rev",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_teams_users_LeaderId",
                table: "teams",
                column: "LeaderId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teams_users_LeaderId",
                table: "teams");

            migrationBuilder.DropTable(
                name: "client_aliases");

            migrationBuilder.DropTable(
                name: "project_participants_rev");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "project_revisions");

            migrationBuilder.DropTable(
                name: "project_heads");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "teams");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QBC.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDayPasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultSquareCardId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCardBrand",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCardLast4",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DayPasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PriceCents = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SquarePaymentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SquareCustomerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CardBrand = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CardLast4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RedeemedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayPasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DayPasses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DayPasses_UserId",
                table: "DayPasses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DayPasses_VisitDate",
                table: "DayPasses",
                column: "VisitDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DayPasses");

            migrationBuilder.DropColumn(
                name: "DefaultSquareCardId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DefaultCardBrand",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DefaultCardLast4",
                table: "AspNetUsers");
        }
    }
}

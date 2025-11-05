using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBankingTest.DAL.Migrations
{
    /// <inheritdoc />
    public partial class @in : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_PhoneNumber",
                table: "Customers");

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "AccountId",
                keyValue: new Guid("c3d4e5f6-3456-7890-cde1-345678901cde"),
                column: "DateOpened",
                value: new DateTime(2025, 10, 16, 16, 16, 11, 286, DateTimeKind.Utc).AddTicks(8989));

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "CustomerId",
                keyValue: new Guid("a1b2c3d4-1234-5678-9abc-123456789abc"),
                columns: new[] { "DateCreated", "DateOfBirth" },
                values: new object[] { new DateTime(2025, 10, 6, 16, 16, 11, 286, DateTimeKind.Utc).AddTicks(8732), new DateTime(2025, 10, 6, 16, 16, 11, 286, DateTimeKind.Utc).AddTicks(8740) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "AccountId",
                keyValue: new Guid("c3d4e5f6-3456-7890-cde1-345678901cde"),
                column: "DateOpened",
                value: new DateTime(2025, 10, 16, 8, 49, 19, 578, DateTimeKind.Utc).AddTicks(7167));

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "CustomerId",
                keyValue: new Guid("a1b2c3d4-1234-5678-9abc-123456789abc"),
                columns: new[] { "DateCreated", "DateOfBirth" },
                values: new object[] { new DateTime(2025, 10, 6, 8, 49, 19, 578, DateTimeKind.Utc).AddTicks(6808), new DateTime(2025, 10, 6, 8, 49, 19, 578, DateTimeKind.Utc).AddTicks(6817) });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PhoneNumber",
                table: "Customers",
                column: "PhoneNumber",
                unique: true);
        }
    }
}

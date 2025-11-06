using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoreBankingTest.DAL.Migrations
{
    /// <inheritdoc />
    public partial class po : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "BVN",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CreditScore",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "AccountId",
                keyValue: new Guid("c3d4e5f6-3456-7890-cde1-345678901cde"),
                column: "DateOpened",
                value: new DateTime(2025, 10, 17, 22, 47, 51, 413, DateTimeKind.Utc).AddTicks(137));

            migrationBuilder.UpdateData(
                table: "Customers",
                keyColumn: "CustomerId",
                keyValue: new Guid("a1b2c3d4-1234-5678-9abc-123456789abc"),
                columns: new[] { "Address", "BVN", "CreditScore", "DateCreated", "DateOfBirth", "PhoneNumber" },
                values: new object[] { "13, oshinowo street , abue osho", "20000000000", 40, new DateTime(2025, 10, 7, 22, 47, 51, 412, DateTimeKind.Utc).AddTicks(9688), new DateTime(1995, 11, 6, 22, 47, 51, 412, DateTimeKind.Utc).AddTicks(9660), "555-0101" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BVN",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditScore",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

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
                columns: new[] { "Address", "DateCreated", "DateOfBirth", "PhoneNumber" },
                values: new object[] { "13,Oshinowo street abule osho", new DateTime(2025, 10, 6, 16, 16, 11, 286, DateTimeKind.Utc).AddTicks(8732), new DateTime(2025, 10, 6, 16, 16, 11, 286, DateTimeKind.Utc).AddTicks(8740), "08134570701" });
        }
    }
}

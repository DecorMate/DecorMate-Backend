using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DecorMateBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetOtpVerifiedAtToUserTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetOtpVerifiedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetOtpVerifiedAt",
                table: "AspNetUsers");
        }
    }
}

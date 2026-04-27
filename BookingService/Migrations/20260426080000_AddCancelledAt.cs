using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using Microsoft.EntityFrameworkCore.Infrastructure;
using BookingService.Data;

namespace BookingService.Migrations
{
    [DbContext(typeof(BookingDbContext))]
    [Migration("20260426080000_AddCancelledAt")]
    public partial class AddCancelledAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Bookings");
        }
    }
}

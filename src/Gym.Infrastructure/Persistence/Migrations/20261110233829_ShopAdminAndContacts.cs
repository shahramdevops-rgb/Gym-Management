using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopAdminAndContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mobile_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    landline_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    whats_app_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    telegram = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    rubika = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    eitaa = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    bale = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    instagram = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_settings", x => x.id);
                    table.CheckConstraint("ck_shop_settings_handles", "(telegram IS NULL OR telegram ~ '^[A-Za-z0-9_.]{3,32}$') AND (rubika IS NULL OR rubika ~ '^[A-Za-z0-9_.]{3,32}$') AND (eitaa IS NULL OR eitaa ~ '^[A-Za-z0-9_.]{3,32}$') AND (bale IS NULL OR bale ~ '^[A-Za-z0-9_.]{3,32}$') AND (instagram IS NULL OR instagram ~ '^[A-Za-z0-9_.]{3,32}$')");
                    table.CheckConstraint("ck_shop_settings_landline", "(landline_phone IS NULL OR landline_phone ~ '^0[1-8][0-9]{9}$')");
                    table.CheckConstraint("ck_shop_settings_mobiles", "(mobile_phone IS NULL OR mobile_phone ~ '^\\+989[0-9]{9}$') AND (whats_app_phone IS NULL OR whats_app_phone ~ '^\\+989[0-9]{9}$')");
                    table.CheckConstraint("ck_shop_settings_single_row", "id = '5d0c7a13-8e2f-4b6a-9c41-7f3e2a9b6d18'");
                });

            migrationBuilder.InsertData(
                table: "shop_settings",
                columns: new[] { "id", "bale", "created_at", "created_by", "eitaa", "instagram", "landline_phone", "mobile_phone", "rubika", "telegram", "updated_at", "updated_by", "whats_app_phone" },
                values: new object[] { new Guid("5d0c7a13-8e2f-4b6a-9c41-7f3e2a9b6d18"), null, new DateTimeOffset(new DateTime(2026, 11, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, null, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_settings");
        }
    }
}

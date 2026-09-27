using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_categories", x => x.id);
                    table.CheckConstraint("ck_expense_categories_name_not_blank", "btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reference_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    voided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expenses", x => x.id);
                    table.CheckConstraint("ck_expenses_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_expenses_description_not_blank", "btrim(description) <> ''");
                    table.CheckConstraint("ck_expenses_void", "(voided_at IS NULL) = (void_reason IS NULL) AND (voided_at IS NULL) = (voided_by_user_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_expenses_asp_net_users_recorded_by_user_id",
                        column: x => x.recorded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expenses_asp_net_users_voided_by_user_id",
                        column: x => x.voided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expenses_expense_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "expense_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "expense_categories",
                columns: new[] { "id", "created_at", "created_by", "name", "normalized_name", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("0b3f6685-b1a2-4979-acd9-c1fe848f156b"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "اجاره", "اجاره", null, null },
                    { new Guid("1dfd1cc7-0587-4096-a384-73e0449b23cd"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "سایر", "سایر", null, null },
                    { new Guid("270c9d15-6962-4eae-a492-483541b8d516"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "تجهیزات", "تجهیزات", null, null },
                    { new Guid("5e5cf0e4-0d67-4b55-830a-8d0a6919fd00"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "حقوق", "حقوق", null, null },
                    { new Guid("627d2be7-d824-422a-ba2c-8d17c98633b5"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "آب", "آب", null, null },
                    { new Guid("97813c23-681f-4e8e-84eb-fa8ecd73c3ac"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "تعمیر و نگهداری", "تعمیر و نگهداری", null, null },
                    { new Guid("b74beca9-8e56-440f-9a19-e3ab97690134"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "برق", "برق", null, null },
                    { new Guid("c4e2af4c-deb5-420e-8087-a7780b0356fd"), new DateTimeOffset(new DateTime(2026, 9, 27, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "خرید بوفه", "خرید بوفه", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_categories_normalized_name",
                table: "expense_categories",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expenses_category_id",
                table: "expenses",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_expense_date_category_id",
                table: "expenses",
                columns: new[] { "expense_date", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_recorded_by_user_id",
                table: "expenses",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_voided_by_user_id",
                table: "expenses",
                column: "voided_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "expense_categories");
        }
    }
}

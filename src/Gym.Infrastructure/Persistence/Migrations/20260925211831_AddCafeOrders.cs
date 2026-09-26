using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCafeOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cafe_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ordered_on = table.Column<DateOnly>(type: "date", nullable: false),
                    placed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cafe_orders", x => x.id);
                    table.CheckConstraint("ck_cafe_orders_cancel", "(cancelled_at IS NULL) = (cancel_reason IS NULL) AND (cancelled_at IS NULL) = (cancelled_by_user_id IS NULL)");
                    table.CheckConstraint("ck_cafe_orders_total_not_negative", "total_amount >= 0");
                    table.ForeignKey(
                        name: "fk_cafe_orders_asp_net_users_cancelled_by_user_id",
                        column: x => x.cancelled_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cafe_orders_asp_net_users_placed_by_user_id",
                        column: x => x.placed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cafe_orders_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cafe_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cafe_order_items", x => x.id);
                    table.CheckConstraint("ck_cafe_order_items_line_total", "line_total = unit_price * quantity");
                    table.CheckConstraint("ck_cafe_order_items_quantity_range", "quantity BETWEEN 1 AND 999");
                    table.CheckConstraint("ck_cafe_order_items_unit_price_not_negative", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_cafe_order_items_cafe_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "cafe_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cafe_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cafe_order_items_order_id",
                table: "cafe_order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_cafe_order_items_product_id",
                table: "cafe_order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_cafe_orders_cancelled_by_user_id",
                table: "cafe_orders",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cafe_orders_member_id",
                table: "cafe_orders",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_cafe_orders_ordered_on",
                table: "cafe_orders",
                column: "ordered_on");

            migrationBuilder.CreateIndex(
                name: "ix_cafe_orders_placed_by_user_id",
                table: "cafe_orders",
                column: "placed_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cafe_order_items");

            migrationBuilder.DropTable(
                name: "cafe_orders");
        }
    }
}

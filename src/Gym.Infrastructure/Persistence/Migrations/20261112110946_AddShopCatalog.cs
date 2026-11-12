using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShopCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_categories", x => x.id);
                    table.CheckConstraint("ck_shop_categories_name_not_blank", "btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "shop_products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_products", x => x.id);
                    table.CheckConstraint("ck_shop_products_description_not_blank", "description IS NULL OR btrim(description) <> ''");
                    table.CheckConstraint("ck_shop_products_name_not_blank", "btrim(name) <> ''");
                    table.ForeignKey(
                        name: "fk_shop_products_shop_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "shop_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_photos", x => x.id);
                    table.CheckConstraint("ck_shop_photos_position_range", "position BETWEEN 0 AND 7");
                    table.CheckConstraint("ck_shop_photos_size_positive", "width > 0 AND height > 0");
                    table.ForeignKey(
                        name: "fk_shop_photos_shop_products_product_id",
                        column: x => x.product_id,
                        principalTable: "shop_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    normalized_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    stock = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_variants", x => x.id);
                    table.CheckConstraint("ck_shop_variants_label_trimmed", "label = btrim(label)");
                    table.CheckConstraint("ck_shop_variants_position_range", "position BETWEEN 0 AND 19");
                    table.CheckConstraint("ck_shop_variants_price_positive", "price > 0");
                    table.CheckConstraint("ck_shop_variants_stock_range", "stock BETWEEN 0 AND 999999");
                    table.ForeignKey(
                        name: "fk_shop_variants_shop_products_product_id",
                        column: x => x.product_id,
                        principalTable: "shop_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_shop_categories_normalized_name",
                table: "shop_categories",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_photos_product_id",
                table: "shop_photos",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_products_category_id",
                table: "shop_products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_shop_products_normalized_name",
                table: "shop_products",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_variants_product_id",
                table: "shop_variants",
                column: "product_id");

            // Written by hand: EF Core cannot describe a deferred unique constraint. Checked at the
            // commit, so a reorder may pass through two rows on one position, and two variants may
            // swap labels, in one save. With the position checks above, at most 8 photos and 20
            // variants per product. Names in ShopCatalogConstraints.
            migrationBuilder.Sql("""
                ALTER TABLE shop_photos
                    ADD CONSTRAINT ux_shop_photos_product_id_position UNIQUE (product_id, position)
                    DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE shop_variants
                    ADD CONSTRAINT ux_shop_variants_product_id_position UNIQUE (product_id, position)
                    DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE shop_variants
                    ADD CONSTRAINT ux_shop_variants_product_id_normalized_label UNIQUE (product_id, normalized_label)
                    DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_photos");

            migrationBuilder.DropTable(
                name: "shop_variants");

            migrationBuilder.DropTable(
                name: "shop_products");

            migrationBuilder.DropTable(
                name: "shop_categories");
        }
    }
}

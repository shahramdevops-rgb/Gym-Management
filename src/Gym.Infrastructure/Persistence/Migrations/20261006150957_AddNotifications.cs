using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gym.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    recipient = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    jalali_year = table.Column<int>(type: "integer", nullable: true),
                    template_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    token3 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    token10 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    token20 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<int>(type: "integer", nullable: true),
                    provider_message_id = table.Column<long>(type: "bigint", nullable: true),
                    cost_rial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivery = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_attempts_not_negative", "attempts >= 0");
                    table.CheckConstraint("ck_notifications_cost_not_negative", "cost_rial IS NULL OR cost_rial >= 0");
                    table.CheckConstraint("ck_notifications_delivery", "delivery IS NULL OR delivery IN ('Delivered', 'NotDelivered', 'BlockedByReceiver')");
                    table.CheckConstraint("ck_notifications_delivery_only_when_sent", "delivery IS NULL OR status = 'Sent'");
                    table.CheckConstraint("ck_notifications_event_matches_kind", "(kind IN ('SubscriptionExpiring', 'LowSessions') AND member_id IS NOT NULL AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL) OR (kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL AND subscription_id IS NULL AND payable_id IS NULL) OR (kind = 'PayableDue' AND payable_id IS NOT NULL AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL)");
                    table.CheckConstraint("ck_notifications_kind", "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue')");
                    table.CheckConstraint("ck_notifications_recipient_not_blank", "btrim(recipient) <> ''");
                    table.CheckConstraint("ck_notifications_sent_has_provider_id", "status <> 'Sent' OR (provider_message_id IS NOT NULL AND sent_at IS NOT NULL AND cost_rial IS NOT NULL)");
                    table.CheckConstraint("ck_notifications_status", "status IN ('Pending', 'Sent', 'Failed', 'Unknown')");
                    table.CheckConstraint("ck_notifications_template_name_not_blank", "btrim(template_name) <> ''");
                    table.CheckConstraint("ck_notifications_token_not_blank", "btrim(token) <> ''");
                    table.ForeignKey(
                        name: "fk_notifications_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_payables_payable_id",
                        column: x => x.payable_id,
                        principalTable: "payables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_birthday_per_member_and_year",
                table: "notifications",
                columns: new[] { "member_id", "jalali_year" },
                unique: true,
                filter: "kind = 'Birthday'");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_per_payable",
                table: "notifications",
                column: "payable_id",
                unique: true,
                filter: "payable_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_one_per_subscription_and_kind",
                table: "notifications",
                columns: new[] { "subscription_id", "kind" },
                unique: true,
                filter: "subscription_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");
        }
    }
}

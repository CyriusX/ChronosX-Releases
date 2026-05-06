using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundFieldsToBillingInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'billing_invoices' AND column_name = 'RefundAmountCents') THEN
                        ALTER TABLE billing_invoices ADD COLUMN ""RefundAmountCents"" bigint NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'billing_invoices' AND column_name = 'RefundId') THEN
                        ALTER TABLE billing_invoices ADD COLUMN ""RefundId"" text NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'billing_invoices' AND column_name = 'RefundStatus') THEN
                        ALTER TABLE billing_invoices ADD COLUMN ""RefundStatus"" text NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'billing_invoices' AND column_name = 'RefundedAt') THEN
                        ALTER TABLE billing_invoices ADD COLUMN ""RefundedAt"" timestamp with time zone NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundAmountCents",
                table: "billing_invoices");

            migrationBuilder.DropColumn(
                name: "RefundId",
                table: "billing_invoices");

            migrationBuilder.DropColumn(
                name: "RefundStatus",
                table: "billing_invoices");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "billing_invoices");

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                table: "billing_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_invoices_SubscriptionId",
                table: "billing_invoices",
                column: "SubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_billing_invoices_org_subscriptions_SubscriptionId",
                table: "billing_invoices",
                column: "SubscriptionId",
                principalTable: "org_subscriptions",
                principalColumn: "id");
        }
    }
}

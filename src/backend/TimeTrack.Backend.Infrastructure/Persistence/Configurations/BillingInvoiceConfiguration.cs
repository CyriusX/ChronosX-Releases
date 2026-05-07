using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class BillingInvoiceConfiguration : IEntityTypeConfiguration<BillingInvoice>
{
    public void Configure(EntityTypeBuilder<BillingInvoice> builder)
    {
        builder.ToTable("billing_invoices");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(e => e.StripeInvoiceId)
            .HasColumnName("stripe_invoice_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.AmountCents)
            .HasColumnName("amount_cents")
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .HasDefaultValue("usd")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.PlanName)
            .HasColumnName("plan_name")
            .HasMaxLength(100);

        builder.Property(e => e.Quantity)
            .HasColumnName("quantity")
            .HasDefaultValue(1);

        builder.Property(e => e.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.Property(e => e.PeriodEnd)
            .HasColumnName("period_end")
            .IsRequired();

        builder.Property(e => e.PdfUrl)
            .HasColumnName("pdf_url")
            .HasMaxLength(500);

        builder.Property(e => e.PaidAt)
            .HasColumnName("paid_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(e => e.StripeInvoiceId).IsUnique();
        builder.HasIndex(e => new { e.OrgId, e.PeriodStart });
    }
}

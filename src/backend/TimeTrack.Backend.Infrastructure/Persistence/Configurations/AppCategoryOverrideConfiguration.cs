using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for AppCategoryOverride entity
///
/// SRP: Apenas configura o mapeamento da entidade
/// </summary>
public sealed class AppCategoryOverrideConfiguration : IEntityTypeConfiguration<AppCategoryOverride>
{
    public void Configure(EntityTypeBuilder<AppCategoryOverride> builder)
    {
        builder.ToTable("app_category_override");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(x => x.Identifier)
            .HasColumnName("identifier")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.IdentifierType)
            .HasColumnName("identifier_type")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<AppIdentifierType>(v, true))
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100);

        builder.Property(x => x.Productivity)
            .HasColumnName("productivity")
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<AppProductivityCategory>(v, true))
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Subcategory)
            .HasColumnName("subcategory")
            .HasConversion(
                v => ToSnakeCase(v.ToString()),
                v => Enum.Parse<AppSubcategory>(ToPascalCase(v), true))
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.Note)
            .HasColumnName("note")
            .HasMaxLength(500);

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Relationships
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrgId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint on (org_id, identifier)
        builder.HasIndex(x => new { x.OrgId, x.Identifier })
            .IsUnique()
            .HasDatabaseName("uq_app_category_override_org_identifier");

        // Index for listing overrides by org
        builder.HasIndex(x => x.OrgId)
            .HasDatabaseName("ix_app_category_override_org_id");

        // Index for productivity filtering
        builder.HasIndex(x => new { x.OrgId, x.Productivity })
            .HasDatabaseName("ix_app_category_override_org_productivity");
    }

    private static string ToSnakeCase(string input)
    {
        return string.Concat(
            input.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString())
        );
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var parts = input.Split('_');
        return string.Concat(parts.Select(p =>
            char.ToUpper(p[0]) + (p.Length > 1 ? p.Substring(1).ToLower() : "")));
    }
}

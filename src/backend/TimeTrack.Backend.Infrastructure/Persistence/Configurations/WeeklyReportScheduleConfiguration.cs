using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Infrastructure.Persistence.Configurations;

internal sealed class WeeklyReportScheduleConfiguration : IEntityTypeConfiguration<WeeklyReportSchedule>
{
    public void Configure(EntityTypeBuilder<WeeklyReportSchedule> builder)
    {
        builder.ToTable("weekly_report_schedules");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(s => s.OrgId)
            .HasColumnName("org_id")
            .IsRequired();

        builder.Property(s => s.DayOfWeek)
            .HasColumnName("day_of_week")
            .IsRequired();

        builder.Property(s => s.TimeOfDay)
            .HasColumnName("time_of_day")
            .IsRequired();

        builder.Property(s => s.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.PreferencesJson)
            .HasColumnName("preferences_json")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        // One schedule per user
        builder.HasIndex(s => s.UserId).IsUnique();
        builder.HasIndex(s => new { s.IsEnabled, s.DayOfWeek });
    }
}

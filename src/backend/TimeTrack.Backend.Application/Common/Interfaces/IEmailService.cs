namespace TimeTrack.Backend.Application.Common.Interfaces;

/// <summary>
/// Interface para envio de emails
/// </summary>
public interface IEmailService
{
    Task SendInvitationEmailAsync(string email, string inviterName, string organizationName, string temporaryPassword, string acceptInviteUrl, CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string email, string displayName, string temporaryPassword, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken cancellationToken = default);

    Task SendWeeklyReportEmailAsync(
        string email,
        string displayName,
        string htmlReport,
        string weekPeriod,
        CancellationToken cancellationToken = default);
}

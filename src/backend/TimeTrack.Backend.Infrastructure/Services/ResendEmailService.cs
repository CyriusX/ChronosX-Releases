using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Services;

/// <summary>
/// Implementação de IEmailService usando Resend .NET SDK oficial
/// https://resend.com/docs/send-with-dotnet
/// </summary>
public sealed class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _frontendBaseUrl;

    public ResendEmailService(
        IResend resend,
        IConfiguration configuration,
        ILogger<ResendEmailService> logger)
    {
        _resend = resend;
        _logger = logger;
        _fromEmail = configuration["Email:From"] ?? "noreply@cyriusx.com";
        _fromName = configuration["Email:FromName"] ?? "CyriusX TimeTrack";
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "https://app.timetrack.com";
    }

    public async Task SendInvitationEmailAsync(
        string email,
        string inviterName,
        string organizationName,
        string temporaryPassword,
        string acceptInviteUrl,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Você foi convidado para {organizationName}";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
    <h2>Bem-vindo ao TimeTrack!</h2>
    <p><strong>{inviterName}</strong> convidou você para participar da organização <strong>{organizationName}</strong>.</p>
    <p style=""margin: 24px 0;"">
        <a href=""{acceptInviteUrl}"" style=""display: inline-block; background-color: #2563eb; color: white; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: bold;"">
            Aceitar Convite
        </a>
    </p>
    <p style=""color: #666; font-size: 13px;"">O link expira em 24 horas.</p>
    <hr style=""border: none; border-top: 1px solid #eee; margin: 20px 0;"">
    <p style=""color: #888; font-size: 12px;"">Ou acesse com a senha temporária: <strong>{temporaryPassword}</strong></p>
    <p style=""color: #666; font-size: 12px; margin-top: 20px;"">Se você não esperava este convite, pode ignorar este email.</p>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlBody, cancellationToken);
    }

    public async Task SendWelcomeEmailAsync(
        string email,
        string displayName,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        var subject = "Bem-vindo ao TimeTrack!";
        var loginUrl = $"{_frontendBaseUrl}/login";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
    <h2>Bem-vindo ao TimeTrack, {displayName}!</h2>
    <p>Sua conta foi criada com sucesso.</p>
    <div style=""background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;"">
        <p style=""margin: 0;""><strong>Sua senha temporária:</strong></p>
        <p style=""font-size: 18px; font-weight: bold; margin: 10px 0; color: #2563eb;"">{temporaryPassword}</p>
    </div>
    <p>Acesse <a href=""{loginUrl}"">{loginUrl}</a> para fazer login.</p>
    <p style=""color: #666; font-size: 12px; margin-top: 30px;"">Se você não criou esta conta, pode ignorar este email.</p>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlBody, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var subject = "Redefinição de Senha - TimeTrack";
        var resetUrl = $"{_frontendBaseUrl}/reset-password?token={resetToken}";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
    <h2>Redefinição de Senha</h2>
    <p>Recebemos uma solicitação para redefinir sua senha.</p>
    <p>
        <a href=""{resetUrl}"" style=""display: inline-block; background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px;"">
            Redefinir Senha
        </a>
    </p>
    <p>Ou copie este link: {resetUrl}</p>
    <p style=""color: #666; font-size: 12px; margin-top: 30px;"">Este link expira em 1 hora. Se você não solicitou, ignore este email.</p>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlBody, cancellationToken);
    }

    private async Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = new EmailMessage
            {
                From = $"{_fromName} <{_fromEmail}>",
                Subject = subject,
                HtmlBody = htmlBody
            };
            message.To.Add(to);

            var response = await _resend.EmailSendAsync(message, cancellationToken);

            if (response.Success)
            {
                _logger.LogInformation(
                    "Email sent successfully to {To}. MessageId: {MessageId}",
                    to, response.Content);
            }
            else
            {
                _logger.LogError(
                    response.Exception,
                    "Failed to send email to {To}",
                    to);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {To}", to);
        }
    }

    public async Task SendWeeklyReportEmailAsync(
        string email,
        string displayName,
        string htmlReport,
        string weekPeriod,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Seu Relatorio Semanal — {weekPeriod}";
        var dashboardUrl = $"{_frontendBaseUrl}/reports";

        var formattedReport = FormatReportForEmail(htmlReport);

        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #f8f9fa; padding: 20px;"">
    <div style=""background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.08);"">
        <!-- Header -->
        <div style=""background: linear-gradient(135deg, #6366f1, #8b5cf6); padding: 32px 24px;"">
            <h1 style=""color: #ffffff; margin: 0; font-size: 22px; font-weight: 600;"">Relatorio Semanal</h1>
            <p style=""color: rgba(255,255,255,0.85); margin: 8px 0 0; font-size: 14px;"">Ola, {displayName}! Aqui esta seu resumo da semana.</p>
            <p style=""color: rgba(255,255,255,0.7); margin: 4px 0 0; font-size: 12px;"">{weekPeriod}</p>
        </div>

        <!-- Report Content -->
        <div style=""padding: 24px;"">
            {formattedReport}
        </div>

        <!-- CTA -->
        <div style=""padding: 0 24px 24px; text-align: center;"">
            <a href=""{dashboardUrl}"" style=""display: inline-block; background: linear-gradient(135deg, #6366f1, #8b5cf6); color: white; padding: 12px 32px; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 14px;"">
                Ver Dashboard Completo
            </a>
        </div>

        <!-- Footer -->
        <div style=""background-color: #f1f3f5; padding: 16px 24px; border-top: 1px solid #e9ecef;"">
            <p style=""color: #868e96; font-size: 11px; margin: 0; text-align: center;"">
                Este relatorio foi gerado automaticamente pelo TimeTrack.
                <a href=""{_frontendBaseUrl}/settings"" style=""color: #8b5cf6;"">Configurar preferencias</a>
            </p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlBody, cancellationToken);
    }

    private static string FormatReportForEmail(string rawReport)
    {
        if (string.IsNullOrWhiteSpace(rawReport))
            return "<p style='color: #868e96;'>Relatorio indisponivel esta semana.</p>";

        var lines = rawReport.Split('\n');
        var html = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("## "))
            {
                var title = trimmed[3..].Trim();
                html.AppendLine($@"
            <div style=""margin-top: 24px; margin-bottom: 12px;"">
                <h2 style=""color: #1c1f2e; font-size: 16px; font-weight: 600; margin: 0; padding-bottom: 8px; border-bottom: 2px solid #8b5cf6;"">{title}</h2>
            </div>");
            }
            else
            {
                html.AppendLine($"            <p style=\"color: #495057; font-size: 14px; line-height: 1.6; margin: 8px 0;\">{trimmed}</p>");
            }
        }

        return html.ToString();
    }
}

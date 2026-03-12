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
        CancellationToken cancellationToken = default)
    {
        var subject = $"Você foi convidado para {organizationName}";
        var loginUrl = $"{_frontendBaseUrl}/login";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
    <h2>Bem-vindo ao TimeTrack!</h2>
    <p><strong>{inviterName}</strong> convidou você para participar da organização <strong>{organizationName}</strong>.</p>
    <div style=""background-color: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0;"">
        <p style=""margin: 0;""><strong>Sua senha temporária:</strong></p>
        <p style=""font-size: 18px; font-weight: bold; margin: 10px 0; color: #2563eb;"">{temporaryPassword}</p>
    </div>
    <p>Acesse <a href=""{loginUrl}"">{loginUrl}</a> para fazer login.</p>
    <p>Você será solicitado a trocar a senha no primeiro acesso.</p>
    <p style=""color: #666; font-size: 12px; margin-top: 30px;"">Se você não criou esta conta, pode ignorar este email.</p>
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
}

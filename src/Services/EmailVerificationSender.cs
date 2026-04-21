using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace TicketPrime.Server.Services;

public sealed class EmailDeliveryOptions
{
    public string FromAddress { get; set; } = "no-reply@ticketprime.local";
    public string FromName { get; set; } = "TicketPrime";
    public int VerificationCodeExpiryMinutes { get; set; } = 10;
    public string PickupDirectory { get; set; } = "logs/emails";
    public SmtpDeliveryOptions Smtp { get; set; } = new();
}

public sealed class SmtpDeliveryOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? User { get; set; }
    public string? Password { get; set; }
}

public sealed record EmailDispatchResult(bool Success, string DeliveryMode, string UserMessage);

public interface IEmailVerificationSender
{
    Task<EmailDispatchResult> SendVerificationCodeAsync(string recipientName, string recipientEmail, string verificationCode, DateTime expiresAtUtc);
}

public sealed class EmailVerificationSender(
    IOptions<EmailDeliveryOptions> options,
    ILogger<EmailVerificationSender> logger,
    IHostEnvironment environment) : IEmailVerificationSender
{
    public async Task<EmailDispatchResult> SendVerificationCodeAsync(string recipientName, string recipientEmail, string verificationCode, DateTime expiresAtUtc)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(options.Value.FromAddress, options.Value.FromName),
            Subject = "Codigo de validacao da conta TicketPrime",
            Body = BuildHtmlBody(recipientName, verificationCode, expiresAtUtc),
            IsBodyHtml = true,
        };

        message.To.Add(new MailAddress(recipientEmail, recipientName));

        using var client = CreateClient(out var deliveryMode, out var userMessage);

        try
        {
            await client.SendMailAsync(message);
            logger.LogInformation("Codigo de verificacao preparado para {Email} usando modo {DeliveryMode}.", recipientEmail, deliveryMode);
            return new EmailDispatchResult(true, deliveryMode, userMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao enviar codigo de verificacao para {Email}.", recipientEmail);
            return new EmailDispatchResult(false, deliveryMode, "Nao foi possivel enviar o codigo de verificacao agora.");
        }
    }

    private SmtpClient CreateClient(out string deliveryMode, out string userMessage)
    {
        var smtp = options.Value.Smtp;
        if (!string.IsNullOrWhiteSpace(smtp.Host))
        {
            var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };

            if (!string.IsNullOrWhiteSpace(smtp.User))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(smtp.User, smtp.Password);
            }

            deliveryMode = "smtp";
            userMessage = "Codigo enviado para o e-mail informado.";
            return client;
        }

        var pickupDirectory = options.Value.PickupDirectory;
        if (!Path.IsPathRooted(pickupDirectory))
            pickupDirectory = Path.Combine(environment.ContentRootPath, pickupDirectory);

        Directory.CreateDirectory(pickupDirectory);

        deliveryMode = "pickup";
        userMessage = "Ambiente local sem SMTP configurado: o e-mail foi gravado na pasta logs/emails.";
        return new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupDirectory,
        };
    }

    private static string BuildHtmlBody(string recipientName, string verificationCode, DateTime expiresAtUtc)
    {
        var expiresAtLocal = expiresAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        return $"""
            <html>
            <body style=\"font-family: Arial, sans-serif; background:#0f172a; color:#e2e8f0; padding:24px;\">
                <div style=\"max-width:560px; margin:0 auto; background:#1e293b; border:1px solid #334155; border-radius:16px; padding:24px;\">
                    <h2 style=\"margin-top:0; color:#f8fafc;\">Validacao de conta TicketPrime</h2>
                    <p>Ola, {WebUtility.HtmlEncode(recipientName)}.</p>
                    <p>Use o codigo abaixo para validar sua conta:</p>
                    <div style=\"font-size:32px; font-weight:700; letter-spacing:8px; margin:24px 0; color:#67e8f9;\">{verificationCode}</div>
                    <p>Este codigo expira em <strong>{expiresAtLocal}</strong>.</p>
                    <p>Se voce nao solicitou este cadastro, ignore esta mensagem.</p>
                </div>
            </body>
            </html>
            """;
    }
}
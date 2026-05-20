using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GameAuth.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public SmtpEmailSender(IConfiguration config){
        _config = config;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_config["Email:FromName"],_config["Email:FromAddress"]!));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder();
        builder.HtmlBody = htmlBody;
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        var host = _config["Email:Host"]!;
        var port = int.Parse(_config["Email:Port"]!);
        var security = Enum.Parse<SecureSocketOptions>(_config["Email:Security"] ?? "StartTls");

        await client.ConnectAsync(host, port, security, ct);

        var username = _config["Email:Username"];
        var password = _config["Email:Password"];
        if(!string.IsNullOrEmpty(username))
            await client.AuthenticateAsync(username, password!, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

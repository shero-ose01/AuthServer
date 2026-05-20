namespace GameAuth.Services;

public class TestEmailSender : IEmailSender{

    private readonly ILogger<TestEmailSender> _log;
    public TestEmailSender(ILogger<TestEmailSender> log){
        _log = log;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default){
        _log.LogInformation("Email: {to} | {subject}\n{htmlBody}",to,subject,htmlBody);
        return Task.CompletedTask;
    }
}

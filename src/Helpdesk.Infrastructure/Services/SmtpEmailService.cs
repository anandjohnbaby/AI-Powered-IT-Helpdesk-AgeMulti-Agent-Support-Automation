using System.Net.Mail;
using Helpdesk.Application.Common;
using Microsoft.Extensions.Options;

namespace Helpdesk.Infrastructure.Services;

public class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string From { get; set; } = "helpdesk@corp.local";
}

public class SmtpEmailService(IOptions<SmtpOptions> options) : IEmailService
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(options.Value.Host, options.Value.Port) { EnableSsl = false };
        using var msg = new MailMessage(options.Value.From, to, subject, body);
        await client.SendMailAsync(msg, ct);
    }
}

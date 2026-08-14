using Ledgerly.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ledgerly.Infrastructure.Email;

public class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _log;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> log) => _log = log;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _log.LogInformation("[EMAIL] To={To} Subject={Subject} Body={Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
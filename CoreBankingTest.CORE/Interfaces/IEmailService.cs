namespace CoreBankingTest.CORE.Interfaces;

public interface IEmailService
{
    Task SendStatementNotificationAsync(string email, string fullName, DateTime statementDate, byte[] statementPdf, CancellationToken cancellationToken = default);
    Task SendJobFailureAlertAsync(string subject, string message, string details, CancellationToken cancellationToken = default);
    Task SendCriticalAlertAsync(string subject, string message, string details, CancellationToken cancellationToken = default);
}

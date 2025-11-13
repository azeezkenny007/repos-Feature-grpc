using Microsoft.Extensions.Logging;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Interfaces;

namespace CoreBanking.Infrastructure.Services;

public class PdfGenerationService : IPdfGenerationService
{
    private readonly ILogger<PdfGenerationService> _logger;

    public PdfGenerationService(ILogger<PdfGenerationService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GenerateAccountStatementAsync(Account account, List<Transaction> transactions, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating PDF statement for account {AccountNumber} from {StartDate} to {EndDate}",
            account.AccountNumber, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

        // TODO: Implement actual PDF generation logic using a library like iTextSharp, QuestPDF, or similar
        // For now, this is a placeholder that returns a dummy PDF byte array

        await Task.Delay(200, cancellationToken); // Simulate PDF generation

        // Create a simple dummy PDF content
        var dummyPdfContent = $"ACCOUNT STATEMENT\n" +
                            $"Account: {account.AccountNumber}\n" +
                            $"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}\n" +
                            $"Balance: {account.Balance.Amount:C}\n" +
                            $"Transactions: {transactions.Count}\n";

        _logger.LogInformation("PDF statement generated for account {AccountNumber}", account.AccountNumber);

        return System.Text.Encoding.UTF8.GetBytes(dummyPdfContent);
    }
}

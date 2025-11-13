using Hangfire;
using Microsoft.Extensions.Logging;
using CoreBankingTest.CORE.Interfaces;

namespace CoreBankingTest.APP.BackgroundJobs;

public class AccountMaintenanceService : IAccountMaintenanceService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<AccountMaintenanceService> _logger;

    public AccountMaintenanceService(
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        ILogger<AccountMaintenanceService> logger)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task CleanupInactiveAccountsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting inactive account cleanup");

        var inactivityThreshold = DateTime.UtcNow.AddYears(-2);
        var inactiveAccounts = await _accountRepository.GetInactiveAccountsSinceAsync(inactivityThreshold, cancellationToken);

        _logger.LogInformation("Found {Count} inactive accounts to process", inactiveAccounts.Count);

        var archivedCount = 0;
        foreach (var account in inactiveAccounts)
        {
            try
            {
                account.UpdateStatusBasedOnRules();

                if (account.CurrentBalance == 0 && account.LastActivityDate < DateTime.UtcNow.AddYears(-3))
                {
                    account.MarkAsArchived();
                    archivedCount++;
                }

                await _accountRepository.UpdateAsync(account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process inactive account {AccountId}", account.AccountId);
            }
        }

        _logger.LogInformation("Completed inactive account cleanup. Archived: {ArchivedCount}", archivedCount);
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ArchiveOldTransactionsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting transaction archival for transactions before {CutoffDate}", cutoffDate);

        var oldTransactions = await _transactionRepository.GetTransactionsBeforeAsync(cutoffDate, cancellationToken);

        _logger.LogInformation("Found {Count} old transactions to archive", oldTransactions.Count);

        // In a real implementation, you would move these to an archive table or storage
        // For now, we'll just log the count
        _logger.LogInformation("Would archive {Count} transactions (implementation pending)", oldTransactions.Count);
    }
}

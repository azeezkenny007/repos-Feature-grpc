namespace CoreBankingTest.APP.BackgroundJobs;

public interface IAccountMaintenanceService
{
    Task CleanupInactiveAccountsAsync(CancellationToken cancellationToken = default);
    Task ArchiveOldTransactionsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
    
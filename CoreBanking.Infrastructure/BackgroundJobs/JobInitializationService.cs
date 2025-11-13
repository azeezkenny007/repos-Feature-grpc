using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.APP.BackgroundJobs;

namespace CoreBanking.Infrastructure.BackgroundJobs;

public interface IJobInitializationService
{
    Task InitializeRecurringJobsAsync();
    Task RegisterOneTimeJobsAsync();
}

public class JobInitializationService : IJobInitializationService
{
    private readonly IHangfireService _hangfireService;
    private readonly HangfireConfiguration _config;
    private readonly ILogger<JobInitializationService> _logger;

    public JobInitializationService(
        IHangfireService hangfireService,
        IOptions<HangfireConfiguration> config,
        ILogger<JobInitializationService> logger)
    {
        _hangfireService = hangfireService;
        _config = config.Value;
        _logger = logger;
    }

    public async Task InitializeRecurringJobsAsync()
    {
        _logger.LogInformation("Initializing recurring jobs");

        try
        {
            // Daily Statement Generation
            await _hangfireService.ScheduleRecurringJobAsync<IDailyStatementService>(
                "DailyStatementGeneration",
                x => x.GenerateDailyStatementsAsync(DateTime.UtcNow.Date, CancellationToken.None),
                _config.ScheduledJobs["DailyStatementGeneration"]);

            // Monthly Interest Calculation
            await _hangfireService.ScheduleRecurringJobAsync<IInterestCalculationService>(
                "MonthlyInterestCalculation",
                x => x.CalculateMonthlyInterestAsync(DateTime.UtcNow.Date, CancellationToken.None),
                _config.ScheduledJobs["MonthlyInterestCalculation"]);

            // Account Cleanup
            await _hangfireService.ScheduleRecurringJobAsync<IAccountMaintenanceService>(
                "AccountCleanup",
                x => x.CleanupInactiveAccountsAsync(CancellationToken.None),
                _config.ScheduledJobs["AccountCleanup"]);

            _logger.LogInformation("Successfully initialized all recurring jobs");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize recurring jobs");
            throw;
        }
    }

    public async Task RegisterOneTimeJobsAsync()
    {
        _logger.LogInformation("Registering one-time jobs");

        // Example: Schedule end-of-month reporting
        var endOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)
            .AddMonths(1)
            .AddDays(-1);

        _logger.LogInformation("One-time jobs registered successfully");
        await Task.CompletedTask;
    }
}

namespace CoreBanking.Infrastructure.BackgroundJobs;

public class HangfireConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;
    public int WorkerCount { get; set; } = 5;
    public TimeSpan InvisibilityTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(5);

    // Scheduled job configurations
    public Dictionary<string, string> ScheduledJobs { get; set; } = new()
    {
        ["DailyStatementGeneration"] = "0 2 * * *",        // 2 AM daily
        ["MonthlyInterestCalculation"] = "0 1 1 * *",      // 1 AM on 1st of month
        ["AccountCleanup"] = "0 0 * * 0",                  // Midnight every Sunday
        ["TransactionArchive"] = "0 3 1 * *",              // 3 AM on 1st of month
        ["CreditScoreRefresh"] = "0 4 * * 1"               // 4 AM every Monday
    };
}

using System.Linq.Expressions;
using Hangfire;
using CoreBankingTest.CORE.Interfaces;

namespace CoreBankingTest.API.Services;

public class HangfireService : IHangfireService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<HangfireService> _logger;

    public HangfireService(
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager,
        ILogger<HangfireService> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    public async Task<string> ScheduleJobAsync<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay)
    {
        try
        {
            var jobId = _backgroundJobClient.Schedule(methodCall, delay);
            _logger.LogInformation("Scheduled one-time job {JobId} for type {Type} with delay {Delay}",
                jobId, typeof(T).Name, delay);
            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule job for type {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task<string> ScheduleRecurringJobAsync<T>(string jobId, Expression<Func<T, Task>> methodCall, string cronExpression)
    {
        try
        {
            _recurringJobManager.AddOrUpdate(jobId, methodCall, cronExpression, TimeZoneInfo.Utc);
            _logger.LogInformation("Scheduled recurring job {JobId} for type {Type} with cron {Cron}",
                jobId, typeof(T).Name, cronExpression);
            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule recurring job {JobId}", jobId);
            throw;
        }
    }

    public async Task<bool> DeleteJobAsync(string jobId)
    {
        try
        {
            var result = _backgroundJobClient.Delete(jobId);
            _logger.LogInformation("Deleted job {JobId}", jobId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete job {JobId}", jobId);
            return false;
        }
    }

    public async Task TriggerJobAsync<T>(Expression<Func<T, Task>> methodCall)
    {
        try
        {
            _backgroundJobClient.Enqueue(methodCall);
            _logger.LogInformation("Triggered immediate job for type {Type}", typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger job for type {Type}", typeof(T).Name);
            throw;
        }
    }
}

# Hangfire Background Jobs - Quick Start Guide
## Get Your Background Processing Up and Running in 5 Minutes

---

## What You Have Now ✅

A **production-ready background job processing system** with:
- ✅ Daily statement generation
- ✅ Monthly interest calculation
- ✅ Account maintenance & cleanup
- ✅ Real-time job monitoring dashboard
- ✅ Automatic retry & error handling
- ✅ Comprehensive logging

---

## Before You Start

### Required Actions (Critical!)

**1. Install EF Core Tools** (if not already installed):
```bash
dotnet tool install --global dotnet-ef
```

**2. Run Database Migration**:
```bash
cd CoreBanking.Infrastructure
dotnet ef migrations add AddHangfireAccountProperties --startup-project ../CoreBankingTest.API
dotnet ef database update --startup-project ../CoreBankingTest.API
```

See [DATABASE_MIGRATION_INSTRUCTIONS.md](DATABASE_MIGRATION_INSTRUCTIONS.md) for detailed steps.

---

## Start the Application

```bash
cd CoreBankingTest.API
dotnet run
```

The application will start on:
- **REST API**: https://localhost:7288
- **gRPC**: https://localhost:7288 (HTTP/2)
- **Swagger**: https://localhost:7288/swagger
- **Hangfire Dashboard**: https://localhost:7288/hangfire 👈 **NEW!**

---

## Access the Hangfire Dashboard

1. Navigate to: `https://localhost:7288/hangfire`
2. You'll see the Hangfire Dashboard with:
   - **Jobs**: View all scheduled, processing, and completed jobs
   - **Recurring Jobs**: See your scheduled background tasks
   - **Servers**: Active Hangfire processing servers
   - **Retries**: Failed jobs and retry attempts

### Dashboard Sections

#### Recurring Jobs Tab
You should see these jobs registered:
- `DailyStatementGeneration` - Runs at 2:00 AM daily
- `MonthlyInterestCalculation` - Runs on the 1st of each month at 1:00 AM
- `AccountCleanup` - Runs every Sunday at midnight

#### Servers Tab
- Shows active Hangfire background processors
- Worker count (default: 5)
- Queues being processed (default, critical, low)

---

## Test a Job Manually

### Option 1: Trigger from Dashboard (Easiest)

1. Go to **Recurring jobs** tab in Hangfire dashboard
2. Click the ▶️ (Play/Trigger) button next to any job
3. Watch it execute in real-time under the **Jobs** tab

### Option 2: Trigger via API (Create a Test Endpoint)

Add this to a controller for testing:

```csharp
// CoreBankingTest.API/Controllers/BackgroundJobsController.cs
[ApiController]
[Route("api/[controller]")]
public class BackgroundJobsController : ControllerBase
{
    private readonly IHangfireService _hangfireService;

    public BackgroundJobsController(IHangfireService hangfireService)
    {
        _hangfireService = hangfireService;
    }

    [HttpPost("trigger-statements")]
    public async Task<IActionResult> TriggerStatementGeneration()
    {
        await _hangfireService.TriggerJobAsync<IDailyStatementService>(
            x => x.GenerateDailyStatementsAsync(DateTime.UtcNow.Date, CancellationToken.None)
        );
        return Ok("Statement generation job triggered");
    }

    [HttpPost("trigger-interest")]
    public async Task<IActionResult> TriggerInterestCalculation()
    {
        await _hangfireService.TriggerJobAsync<IInterestCalculationService>(
            x => x.CalculateMonthlyInterestAsync(DateTime.UtcNow.Date, CancellationToken.None)
        );
        return Ok("Interest calculation job triggered");
    }

    [HttpPost("trigger-cleanup")]
    public async Task<IActionResult> TriggerAccountCleanup()
    {
        await _hangfireService.TriggerJobAsync<IAccountMaintenanceService>(
            x => x.CleanupInactiveAccountsAsync(CancellationToken.None)
        );
        return Ok("Account cleanup job triggered");
    }
}
```

Then call via Swagger or curl:
```bash
curl -X POST https://localhost:7288/api/BackgroundJobs/trigger-statements
```

---

## Verify Everything Works

### 1. Check Hangfire Tables Created

Open SQL Server Management Studio and run:

```sql
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE 'Hangfire%'
ORDER BY TABLE_NAME;

-- You should see tables like:
-- HangfireAggregatedCounter
-- HangfireCounter
-- HangfireHash
-- HangfireJob
-- HangfireJobParameter
-- HangfireJobQueue
-- HangfireList
-- HangfireSchema
-- HangfireServer
-- HangfireSet
-- HangfireState
```

### 2. Check New Account Columns

```sql
SELECT TOP 5
    AccountNumber,
    LastActivityDate,
    Status,
    IsInterestBearing,
    IsArchived
FROM Accounts;

-- All accounts should have:
-- - LastActivityDate populated
-- - Status = 'Active'
-- - IsInterestBearing = 1 or 0
-- - IsArchived = 0
```

### 3. Check Application Logs

Look for these log messages in your console:

```
[Startup] Hangfire recurring jobs initialized successfully
Job [JobId] created and queued successfully
Starting execution of job [JobId]
Job [JobId] completed successfully in [Duration]
```

---

## Scheduled Job Times

| Job | Schedule | When It Runs | Purpose |
|-----|----------|--------------|---------|
| Daily Statements | `0 2 * * *` | 2:00 AM every day | Generate account statements |
| Monthly Interest | `0 1 1 * *` | 1:00 AM on the 1st | Calculate & credit interest |
| Account Cleanup | `0 0 * * 0` | Midnight every Sunday | Archive inactive accounts |

### Change Job Schedules

Edit [appsettings.json](CoreBankingTest.API/appsettings.json):

```json
{
  "Hangfire": {
    "ScheduledJobs": {
      "DailyStatementGeneration": "0 2 * * *",     // Change the time here
      "MonthlyInterestCalculation": "0 1 1 * *",
      "AccountCleanup": "0 0 * * 0"
    }
  }
}
```

Use [crontab.guru](https://crontab.guru/) to generate cron expressions.

---

## Monitor Job Execution

### In the Dashboard

**Jobs Tab**: Shows all job executions
- **Succeeded**: Green checkmarks
- **Failed**: Red X's with error details
- **Processing**: Currently running
- **Enqueued**: Waiting to run

**Click any job** to see:
- Execution time
- Parameters
- State history
- Exception details (if failed)

### In Application Logs

Jobs log extensively:

```log
[Info] Starting daily statement generation for 2025-11-13
[Info] Found 150 active accounts for statement generation
[Info] Processed batch 1, Success: 100, Failed: 0
[Info] Processed batch 2, Success: 50, Failed: 0
[Info] Completed daily statement generation. Processed: 150, Failed: 0, Duration: 00:02:15
```

---

## Common Scenarios

### Scenario 1: Job Failed

**In Dashboard**:
1. Go to **Failed** jobs tab
2. Click the job to see error details
3. Click **Requeue** to retry

**In Code**:
- Jobs automatically retry up to 3 times (configurable)
- Check application logs for detailed error messages

### Scenario 2: Job Running Too Long

**Solution**: Increase timeout or worker count

Edit [appsettings.json](CoreBankingTest.API/appsettings.json):
```json
{
  "Hangfire": {
    "WorkerCount": 10  // Increase from 5 to 10
  }
}
```

### Scenario 3: Need to Run Job Immediately

**Option 1**: Dashboard - Click ▶️ next to recurring job

**Option 2**: Code - Call the service directly:
```csharp
var statementService = serviceProvider.GetRequiredService<IDailyStatementService>();
await statementService.GenerateDailyStatementsAsync(DateTime.UtcNow.Date);
```

---

## What's Next?

### Immediate Actions (Required)

1. ✅ **Run database migration** (see instructions above)
2. ✅ **Test all jobs** manually from dashboard
3. ✅ **Review logs** for any errors

### Production Setup (Before Going Live)

1. **Implement Real Services**:
   - Replace `EmailService` with SendGrid/AWS SES
   - Replace `PdfGenerationService` with QuestPDF/iText7
   - See [HANGFIRE_IMPLEMENTATION_GUIDE.md](HANGFIRE_IMPLEMENTATION_GUIDE.md) for details

2. **Configure Production Settings**:
   ```json
   {
     "Hangfire": {
       "WorkerCount": 20,  // Increase for production
       "RetryAttempts": 5  // More retries for prod
     }
   }
   ```

3. **Secure Dashboard**:
   - Update `HangfireAuthorizationFilter` to require authentication
   - Currently allows all access (development only!)

4. **Set Up Monitoring**:
   - Configure email alerts for job failures
   - Set up Application Insights
   - Add custom metrics

### Optional Enhancements

- Add more background jobs (transaction archival, reporting)
- Implement job chaining (run jobs in sequence)
- Add job parameters for flexibility
- Create admin UI for job management

---

## Architecture Overview

```
┌─────────────────────────────────────────┐
│         Your Application                │
│  ┌───────────────────────────────────┐ │
│  │  Program.cs                        │ │
│  │  - Registers Hangfire              │ │
│  │  - Initializes recurring jobs      │ │
│  └───────────────────────────────────┘ │
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│       Hangfire Server (Background)      │
│  ┌───────────────────────────────────┐ │
│  │  Job Scheduler                     │ │
│  │  - Monitors cron schedules         │ │
│  │  - Enqueues jobs at scheduled time │ │
│  └───────────────────────────────────┘ │
└─────────────────────────────────────────┘
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
  ┌─────────┐ ┌─────────┐ ┌─────────┐
  │Statement│ │Interest │ │ Cleanup │
  │  Job    │ │  Job    │ │  Job    │
  └─────────┘ └─────────┘ └─────────┘
        │           │           │
        └───────────┴───────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│         SQL Server Database             │
│  - Accounts                             │
│  - Transactions                         │
│  - Hangfire tables                      │
└─────────────────────────────────────────┘
```

---

## Files You Need to Know

| File | Purpose | Location |
|------|---------|----------|
| appsettings.json | Hangfire configuration | CoreBankingTest.API/ |
| Program.cs | Setup & registration | CoreBankingTest.API/ |
| Dashboard | Job monitoring | https://localhost:7288/hangfire |
| DailyStatementService.cs | Statement generation | CoreBankingTest.APP/BackgroundJobs/ |
| InterestCalculationService.cs | Interest calculation | CoreBankingTest.APP/BackgroundJobs/ |
| AccountMaintenanceService.cs | Account cleanup | CoreBankingTest.APP/BackgroundJobs/ |

---

## Troubleshooting Quick Reference

| Problem | Solution |
|---------|----------|
| Dashboard shows 404 | Check Program.cs has `app.UseHangfireDashboardWithAuth()` |
| Jobs not running | Check `WorkerCount > 0` in appsettings.json |
| Database errors | Run migration: `dotnet ef database update` |
| Job failed | Check logs, click job in dashboard for details |
| Can't access dashboard | URL is `/hangfire` not `/Hangfire` (case-sensitive) |

---

## Resources

- **Full Implementation Guide**: [HANGFIRE_IMPLEMENTATION_GUIDE.md](HANGFIRE_IMPLEMENTATION_GUIDE.md)
- **Migration Instructions**: [DATABASE_MIGRATION_INSTRUCTIONS.md](DATABASE_MIGRATION_INSTRUCTIONS.md)
- **Hangfire Docs**: https://docs.hangfire.io/
- **Cron Generator**: https://crontab.guru/

---

## Support

If you encounter issues:
1. Check the [HANGFIRE_IMPLEMENTATION_GUIDE.md](HANGFIRE_IMPLEMENTATION_GUIDE.md) troubleshooting section
2. Review application logs
3. Check Hangfire dashboard for job details
4. Verify database migration was applied

---

**🎉 Congratulations!** You now have a production-ready background job processing system for your banking application!

---

**Created**: Day 11 - Hangfire Background Jobs
**Version**: 1.0
**Last Updated**: 2025-11-13

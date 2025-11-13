# Hangfire Background Jobs Implementation Guide - Day 11
## CoreBanking Production-Ready Background Processing

This document provides a comprehensive guide to the Hangfire background job processing system implemented in the CoreBanking application.

---

## Table of Contents
1. [Overview](#overview)
2. [What Was Implemented](#what-was-implemented)
3. [Architecture](#architecture)
4. [Configuration](#configuration)
5. [Background Jobs](#background-jobs)
6. [Testing & Verification](#testing--verification)
7. [Next Steps](#next-steps)
8. [Troubleshooting](#troubleshooting)

---

## Overview

Hangfire is a production-ready background job processing framework for .NET that allows you to execute time-consuming tasks outside of the main request-response cycle. This implementation provides:

- **Daily Statement Generation**: Automated account statement generation for all active accounts
- **Monthly Interest Calculation**: Automatic interest calculation and credit for interest-bearing accounts
- **Account Maintenance**: Cleanup and archival of inactive accounts
- **Job Monitoring**: Real-time dashboard for job tracking and failure handling
- **Resilience**: Automatic retry mechanisms and failure recovery

---

## What Was Implemented

### 1. Core Infrastructure ✅
- **Hangfire Configuration** ([HangfireConfiguration.cs](CoreBanking.Infrastructure/BackgroundJobs/HangfireConfiguration.cs))
  - SQL Server storage configuration
  - Worker count and retry settings
  - Cron expressions for scheduled jobs

- **Hangfire Service** ([HangfireService.cs](CoreBankingTest.API/Services/HangfireService.cs))
  - Job scheduling interface
  - One-time and recurring job management
  - Job deletion and triggering

- **Service Extensions** ([HangfireServiceExtensions.cs](CoreBankingTest.API/Extensions/HangfireServiceExtensions.cs))
  - Dependency injection setup
  - Custom job filters for logging
  - Server configuration

### 2. Background Job Services ✅

#### Daily Statement Generation
- **Interface**: `IDailyStatementService`
- **Implementation**: `DailyStatementService`
- **Location**: [CoreBankingTest.APP/BackgroundJobs/](CoreBankingTest.APP/BackgroundJobs/)
- **Features**:
  - Batch processing (100 accounts per batch)
  - PDF statement generation
  - Email notifications for opted-in customers
  - Comprehensive error handling
  - Progress tracking

#### Interest Calculation
- **Interface**: `IInterestCalculationService`
- **Implementation**: `InterestCalculationService`
- **Location**: [CoreBankingTest.APP/BackgroundJobs/](CoreBankingTest.APP/BackgroundJobs/)
- **Features**:
  - Monthly interest calculation
  - Variable interest rates by account type
  - Average daily balance calculation
  - Automatic interest credit transactions
  - Detailed logging

#### Account Maintenance
- **Interface**: `IAccountMaintenanceService`
- **Implementation**: `AccountMaintenanceService`
- **Location**: [CoreBankingTest.APP/BackgroundJobs/](CoreBankingTest.APP/BackgroundJobs/)
- **Features**:
  - Inactive account identification
  - Status-based updates
  - Account archival
  - Transaction archival

### 3. Entity Enhancements ✅

#### Account Entity Updates
**File**: [CoreBankingTest.CORE/Entities/Account.cs](CoreBankingTest.CORE/Entities/Account.cs)

**New Properties**:
```csharp
public DateTime LastActivityDate { get; private set; }
public string Status { get; private set; } // Active, Inactive, Closed, Suspended
public bool IsInterestBearing { get; private set; }
public bool IsArchived { get; private set; }
public decimal CurrentBalance => Balance.Amount;
```

**New Methods**:
- `MarkAsClosed()` - Close an account
- `MarkAsArchived()` - Archive old accounts
- `UpdateStatusBasedOnRules()` - Apply business rules
- `UpdateLastActivityDate()` - Track activity
- `SetInterestBearing()` - Configure interest

#### Customer Entity Updates
**File**: [CoreBankingTest.CORE/Entities/Customer.cs](CoreBankingTest.CORE/Entities/Customer.cs)

**New Properties**:
```csharp
public bool EmailOptIn { get; private set; } = true;
public string FullName => $"{FirstName} {LastName}";
```

#### Transaction Entity Updates
**File**: [CoreBankingTest.CORE/Entities/Transaction.cs](CoreBankingTest.CORE/Entities/Transaction.cs)

**New Method**:
```csharp
public static Transaction CreateInterestCredit(AccountId accountId, Money amount,
    DateTime calculationDate, string description)
```

### 4. Repository Enhancements ✅

#### Account Repository
**File**: [CoreBanking.Infrastructure/Repositories/AccountRepository.cs](CoreBanking.Infrastructure/Repositories/AccountRepository.cs)

**New Methods**:
- `GetActiveAccountsAsync()` - Get all active accounts
- `GetInterestBearingAccountsAsync()` - Get accounts eligible for interest
- `GetInactiveAccountsSinceAsync()` - Find inactive accounts
- `GetAccountsByStatusAsync()` - Filter by status
- `GetAccountsWithLowBalanceAsync()` - Low balance accounts

#### Transaction Repository
**File**: [CoreBanking.Infrastructure/Repositories/TransactionRepository.cs](CoreBanking.Infrastructure/Repositories/TransactionRepository.cs)

**New Methods**:
- `AddRangeAsync()` - Bulk transaction insert
- `SaveChangesAsync()` - Explicit save
- `GetTransactionsBeforeAsync()` - Archival queries
- `GetTransactionsByDateRangeAsync()` - Date range queries
- `GetTransactionsByAccountAndDateRangeAsync()` - Account-specific queries
- `GetAverageDailyBalanceAsync()` - Interest calculations

### 5. Supporting Services ✅

#### Email Service (Placeholder)
**File**: [CoreBanking.Infrastructure/Services/EmailService.cs](CoreBanking.Infrastructure/Services/EmailService.cs)
- Statement notifications
- Job failure alerts
- Critical alerts

#### PDF Generation Service (Placeholder)
**File**: [CoreBanking.Infrastructure/Services/PdfGenerationService.cs](CoreBanking.Infrastructure/Services/PdfGenerationService.cs)
- Account statement PDF generation

#### Job Initialization Service
**File**: [CoreBanking.Infrastructure/BackgroundJobs/JobInitializationService.cs](CoreBanking.Infrastructure/BackgroundJobs/JobInitializationService.cs)
- Recurring job registration
- One-time job scheduling

### 6. Dashboard & Monitoring ✅

#### Hangfire Dashboard
**File**: [CoreBankingTest.API/Extensions/HangfireDashboardExtensions.cs](CoreBankingTest.API/Extensions/HangfireDashboardExtensions.cs)
- Custom authorization filter
- Dashboard configuration
- Accessible at `/hangfire`

---

## Architecture

### Job Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     Application Startup                      │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Program.cs                                         │    │
│  │  - Register Hangfire services                       │    │
│  │  - Initialize recurring jobs                        │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                 Hangfire Server (Background)                 │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Job Scheduler                                      │    │
│  │  - Monitors cron expressions                        │    │
│  │  - Enqueues jobs at scheduled times                 │    │
│  └────────────────────────────────────────────────────┘    │
│                            │                                 │
│              ┌─────────────┼─────────────┐                 │
│              ▼             ▼             ▼                  │
│   ┌──────────────┐ ┌──────────────┐ ┌─────────────┐      │
│   │  Daily       │ │  Monthly     │ │  Account    │       │
│   │  Statements  │ │  Interest    │ │  Cleanup    │       │
│   │  (2 AM)      │ │  (1st, 1 AM) │ │  (Sun 12 AM)│       │
│   └──────────────┘ └──────────────┘ └─────────────┘       │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Job Execution Layer                        │
│  - Batch processing                                          │
│  - Error handling & retry                                    │
│  - Progress logging                                          │
│  - Transaction management                                    │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                     Data Layer                               │
│  - Account & Transaction repositories                        │
│  - Email & PDF services                                      │
│  - Database operations                                       │
└─────────────────────────────────────────────────────────────┘
```

---

## Configuration

### appsettings.json
**File**: [CoreBankingTest.API/appsettings.json](CoreBankingTest.API/appsettings.json)

```json
{
  "Hangfire": {
    "ConnectionString": "Your SQL Server connection string",
    "WorkerCount": 5,
    "RetryAttempts": 3,
    "ScheduledJobs": {
      "DailyStatementGeneration": "0 2 * * *",        // 2 AM daily
      "MonthlyInterestCalculation": "0 1 1 * *",      // 1 AM on 1st
      "AccountCleanup": "0 0 * * 0",                  // Midnight Sunday
      "TransactionArchive": "0 3 1 * *",              // 3 AM on 1st
      "CreditScoreRefresh": "0 4 * * 1"               // 4 AM Monday
    }
  }
}
```

### Cron Expression Guide

| Pattern | Description | Example |
|---------|-------------|---------|
| `* * * * *` | Every minute | Testing |
| `0 * * * *` | Every hour | Hourly tasks |
| `0 2 * * *` | Daily at 2 AM | Daily statements |
| `0 1 1 * *` | 1st of month at 1 AM | Monthly interest |
| `0 0 * * 0` | Sunday midnight | Weekly cleanup |
| `0 9 * * 1-5` | Weekdays at 9 AM | Business day tasks |

---

## Background Jobs

### Job Schedules

| Job Name | Schedule | Cron | Description |
|----------|----------|------|-------------|
| Daily Statement Generation | 2:00 AM daily | `0 2 * * *` | Generates account statements |
| Monthly Interest Calculation | 1st at 1:00 AM | `0 1 1 * *` | Calculates and credits interest |
| Account Cleanup | Sunday 12:00 AM | `0 0 * * 0` | Archives inactive accounts |

### Job Features

#### Automatic Retry
All jobs are configured with automatic retry using the `[AutomaticRetry]` attribute:
- Default: 3 attempts
- Configurable via appsettings.json
- Exponential backoff

#### Batch Processing
Large operations are processed in batches:
- Statement Generation: 100 accounts per batch
- Interest Calculation: Sequential with error isolation

#### Logging
Comprehensive logging at all levels:
- Job start/completion
- Batch progress
- Individual failures
- Performance metrics

---

## Testing & Verification

### 1. Verify Installation

```bash
# Check if Hangfire packages are installed
dotnet list package | grep Hangfire

# Expected output:
# > Hangfire.Core 1.8.22
# > Hangfire.SqlServer 1.8.22
# > Hangfire.AspNetCore 1.8.22
```

### 2. Access Hangfire Dashboard

1. Start the application
2. Navigate to: `https://localhost:7288/hangfire`
3. You should see the Hangfire Dashboard

### 3. Verify Database Tables

After first run, Hangfire creates these tables in your database:

```sql
-- Check if Hangfire tables exist
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE 'Hangfire%'

-- Expected tables:
-- HangfireJob
-- HangfireServer
-- HangfireState
-- HangfireJobParameter
-- HangfireJobQueue
-- etc.
```

### 4. Manual Job Trigger (Testing)

You can manually trigger jobs for testing:

```csharp
// In a controller or service
private readonly IHangfireService _hangfireService;

// Trigger a job immediately
await _hangfireService.TriggerJobAsync<IDailyStatementService>(
    x => x.GenerateDailyStatementsAsync(DateTime.UtcNow.Date, CancellationToken.None)
);
```

### 5. Monitor Job Execution

In the Hangfire Dashboard:
- **Jobs**: View all scheduled, processing, and completed jobs
- **Recurring Jobs**: See all recurring job schedules
- **Servers**: View active Hangfire servers
- **Retries**: Track failed jobs and retry attempts

---

## Next Steps

### 1. Create Database Migration ⚠️ IMPORTANT

The Account entity has new properties that need to be added to the database:

```bash
# Navigate to the Infrastructure project
cd CoreBanking.Infrastructure

# Add a migration
dotnet ef migrations add AddAccountBackgroundJobProperties --startup-project ../CoreBankingTest.API --context BankingDbContext

# Update the database
dotnet ef database update --startup-project ../CoreBankingTest.API --context BankingDbContext
```

### 2. Implement Real Email Service

Replace the placeholder `EmailService.cs` with a real implementation:
- Use SendGrid, AWS SES, or SMTP
- Implement templates for statements
- Add HTML formatting
- Handle attachments (PDF statements)

### 3. Implement Real PDF Generation

Replace the placeholder `PdfGenerationService.cs`:
- Use libraries like:
  - **QuestPDF** (recommended, modern API)
  - **iText7** (feature-rich)
  - **PdfSharp** (lightweight)
- Design professional statement templates
- Include branding and logos
- Add transaction tables and summaries

### 4. Add Job Monitoring Alerts

Implement proactive monitoring:
- Email alerts for job failures
- Slack/Teams webhooks
- Application Insights integration
- Custom metrics and dashboards

### 5. Implement Additional Jobs

Consider adding:
- Credit score refresh
- Transaction archival
- Compliance reporting
- Audit log generation
- Account balance verification

### 6. Configure Production Settings

For production deployment:

```json
{
  "Hangfire": {
    "WorkerCount": 20,  // Increase for production
    "RetryAttempts": 5,
    "ScheduledJobs": {
      // Adjust times for production time zones
    }
  }
}
```

Update the dashboard authorization filter:

```csharp
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // PRODUCTION: Add proper authentication
        return httpContext.User.Identity.IsAuthenticated &&
               httpContext.User.IsInRole("Admin");
    }
}
```

---

## Troubleshooting

### Issue: Hangfire tables not created

**Solution**:
- Ensure `PrepareSchemaIfNecessary = true` in HangfireServiceExtensions.cs
- Check database connection string
- Verify SQL Server permissions

### Issue: Jobs not executing

**Solution**:
- Check Hangfire Dashboard `/hangfire` for errors
- Verify `WorkerCount > 0`
- Check application logs
- Ensure `AddHangfireServer()` is called

### Issue: Database migration fails

**Solution**:
```bash
# Check existing migrations
dotnet ef migrations list --startup-project ../CoreBankingTest.API

# Remove last migration if needed
dotnet ef migrations remove --startup-project ../CoreBankingTest.API

# Try again
dotnet ef migrations add AddAccountBackgroundJobProperties --startup-project ../CoreBankingTest.API
```

### Issue: Jobs fail with dependency injection errors

**Solution**:
- Ensure all services are registered in Program.cs
- Use `Scoped` lifetime for services that use DbContext
- Check constructor dependencies

### Issue: Performance problems

**Solutions**:
- Increase `WorkerCount` in appsettings.json
- Add database indexes on frequently queried columns
- Optimize batch sizes
- Use `AsNoTracking()` for read-only queries

---

## Key Files Reference

| File | Location | Purpose |
|------|----------|---------|
| HangfireConfiguration.cs | CoreBanking.Infrastructure/BackgroundJobs/ | Configuration model |
| HangfireServiceExtensions.cs | CoreBankingTest.API/Extensions/ | DI setup |
| HangfireService.cs | CoreBankingTest.API/Services/ | Job management |
| DailyStatementService.cs | CoreBankingTest.APP/BackgroundJobs/ | Statement generation |
| InterestCalculationService.cs | CoreBankingTest.APP/BackgroundJobs/ | Interest calculation |
| AccountMaintenanceService.cs | CoreBankingTest.APP/BackgroundJobs/ | Account cleanup |
| JobInitializationService.cs | CoreBanking.Infrastructure/BackgroundJobs/ | Job registration |
| Program.cs | CoreBankingTest.API/ | Application startup |
| appsettings.json | CoreBankingTest.API/ | Configuration |

---

## Additional Resources

- **Hangfire Documentation**: https://docs.hangfire.io/
- **Cron Expression Generator**: https://crontab.guru/
- **QuestPDF Documentation**: https://www.questpdf.com/
- **SendGrid .NET Guide**: https://github.com/sendgrid/sendgrid-csharp

---

## Summary

✅ **Completed**:
- Hangfire infrastructure setup
- Three core background job services
- Entity enhancements for job processing
- Repository methods for efficient queries
- Dashboard and monitoring setup
- Program.cs configuration
- Comprehensive documentation

⚠️ **Required Actions**:
1. Run database migration for Account entity updates
2. Implement real Email service
3. Implement real PDF generation service
4. Test all jobs in development
5. Configure production settings

🎯 **Production Readiness Checklist**:
- [ ] Database migration completed
- [ ] Email service implemented
- [ ] PDF service implemented
- [ ] Jobs tested in development
- [ ] Dashboard authentication configured
- [ ] Monitoring alerts set up
- [ ] Production configuration reviewed
- [ ] Performance testing completed

---

**Generated**: Day 11 - Hangfire Implementation
**Version**: 1.0
**Last Updated**: 2025-11-13

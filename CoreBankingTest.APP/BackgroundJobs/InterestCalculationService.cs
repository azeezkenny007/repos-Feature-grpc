using Hangfire;
using Microsoft.Extensions.Logging;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;

namespace CoreBankingTest.APP.BackgroundJobs;

public class InterestCalculationService : IInterestCalculationService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<InterestCalculationService> _logger;

    public InterestCalculationService(
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        ILogger<InterestCalculationService> logger)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task CalculateMonthlyInterestAsync(DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting monthly interest calculation for {CalculationDate}", calculationDate.ToString("yyyy-MM-dd"));

        var startTime = DateTime.UtcNow;
        var interestBearingAccounts = await _accountRepository.GetInterestBearingAccountsAsync(cancellationToken);

        _logger.LogInformation("Found {AccountCount} interest-bearing accounts", interestBearingAccounts.Count);

        // Use factory method because the constructor is not publicly accessible
        var results = InterestCalculationResult.Success(0m);
        results.SuccessfulCalculations = 0;
        results.FailedCalculations = 0;
        results.TotalInterest = 0m;

        var interestTransactions = new List<Transaction>();

        foreach (var account in interestBearingAccounts)
        {
            try
            {
                var interestResult = await CalculateAccountInterestAsync(account.AccountId, calculationDate, cancellationToken);

                if (interestResult.IsSuccess)
                {
                    results.SuccessfulCalculations++;
                    results.TotalInterest += interestResult.InterestAmount;

                    // Create interest credit transaction
                    var interestTransaction = Transaction.CreateInterestCredit(
                        account.AccountId,
                        new Money(interestResult.InterestAmount),
                        calculationDate,
                        $"Monthly interest for {calculationDate:MMMM yyyy}");

                    interestTransactions.Add(interestTransaction);
                }
                else
                {
                    results.FailedCalculations++;
                    _logger.LogWarning("Failed to calculate interest for account {AccountId}: {Error}",
                        account.AccountId, interestResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                results.FailedCalculations++;
                _logger.LogError(ex, "Error calculating interest for account {AccountId}", account.AccountId);
            }
        }

        // Save all interest transactions
        if (interestTransactions.Any())
        {
            await _transactionRepository.AddRangeAsync(interestTransactions, cancellationToken);
            await _transactionRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created {TransactionCount} interest credit transactions", interestTransactions.Count);
        }

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation("Completed interest calculation. Successful: {Success}, Failed: {Failed}, Total Interest: {TotalInterest:C}, Duration: {Duration}",
            results.SuccessfulCalculations, results.FailedCalculations, results.TotalInterest, duration);
    }

    public async Task<InterestCalculationResult> CalculateAccountInterestAsync(AccountId accountId, DateTime calculationDate, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
            return InterestCalculationResult.Failure($"Account {accountId} not found");

        if (!account.IsInterestBearing)
            return InterestCalculationResult.Failure($"Account {accountId} is not interest-bearing");

        try
        {
            // Calculate average daily balance for the month
            var monthStart = new DateTime(calculationDate.Year, calculationDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var averageBalance = await CalculateAverageDailyBalanceAsync(accountId, monthStart, monthEnd, cancellationToken);

            // Calculate interest based on account type and balance
            var interestRate = GetInterestRate(account.AccountType, averageBalance);
            var interestAmount = CalculateInterestAmount(averageBalance, interestRate, monthStart, monthEnd);

            _logger.LogDebug("Calculated interest for account {AccountId}: {InterestAmount:C} at rate {InterestRate:P2}",
                accountId, interestAmount, interestRate);

            return InterestCalculationResult.Success(interestAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating interest for account {AccountId}", accountId);
            return InterestCalculationResult.Failure(ex.Message);
        }
    }

    private async Task<decimal> CalculateAverageDailyBalanceAsync(AccountId accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        // Use the repository method
        return await _transactionRepository.GetAverageDailyBalanceAsync(accountId, startDate, endDate, cancellationToken);
    }

    private decimal GetInterestRate(AccountType accountType, decimal balance)
    {
        return accountType switch
        {
            AccountType.Savings => balance >= 10000 ? 0.015m : 0.01m, // 1.5% or 1%
            AccountType.Checking => 0.001m, // 0.1%
            AccountType.FixedDeposit => 0.035m, // 3.5%
            _ => 0.0m
        };
    }

    private decimal CalculateInterestAmount(decimal principal, decimal annualRate, DateTime startDate, DateTime endDate)
    {
        var daysInYear = 365;
        var days = (endDate - startDate).Days + 1;
        return principal * annualRate * days / daysInYear;
    }
}

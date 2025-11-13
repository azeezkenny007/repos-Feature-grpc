using CoreBankingTest.CORE.ValueObjects;

namespace CoreBankingTest.APP.BackgroundJobs;

public interface IDailyStatementService
{
    Task GenerateDailyStatementsAsync(DateTime statementDate, CancellationToken cancellationToken = default);
    Task<StatementGenerationResult> GenerateCustomerStatementAsync(Guid customerId, DateTime statementDate, CancellationToken cancellationToken = default);
}

// Result classes
public class StatementGenerationResult
{
    public int ProcessedAccounts { get; set; }
    public int FailedAccounts { get; set; }
    public bool IsSuccess => FailedAccounts == 0;
    public TimeSpan Duration { get; set; }
}

public class AccountStatementResult
{
    public bool IsSuccess { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public AccountId? AccountId { get; private set; }

    public static AccountStatementResult Success(AccountId accountId)
        => new AccountStatementResult { IsSuccess = true, AccountId = accountId };

    public static AccountStatementResult Failure(string errorMessage)
        => new AccountStatementResult { IsSuccess = false, ErrorMessage = errorMessage };
}

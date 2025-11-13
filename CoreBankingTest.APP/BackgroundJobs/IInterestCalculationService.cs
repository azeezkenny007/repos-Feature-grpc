using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.ValueObjects;

namespace CoreBankingTest.APP.BackgroundJobs;

public interface IInterestCalculationService
{
    Task CalculateMonthlyInterestAsync(DateTime calculationDate, CancellationToken cancellationToken = default);
    Task<InterestCalculationResult> CalculateAccountInterestAsync(AccountId accountId, DateTime calculationDate, CancellationToken cancellationToken = default);
}

public class InterestCalculationResult
{
    public bool IsSuccess { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public decimal InterestAmount { get; private set; }
    public int SuccessfulCalculations { get; set; }
    public int FailedCalculations { get; set; }
    public decimal TotalInterest { get; set; }

    private InterestCalculationResult() { }

    public static InterestCalculationResult Success(decimal interestAmount)
    {
        return new InterestCalculationResult
        {
            IsSuccess = true,
            InterestAmount = interestAmount,
            ErrorMessage = string.Empty
        };
    }

    public static InterestCalculationResult Failure(string errorMessage)
    {
        return new InterestCalculationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            InterestAmount = 0
        };
    }
}

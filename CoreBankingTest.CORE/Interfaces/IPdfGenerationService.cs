using CoreBankingTest.CORE.Entities;

namespace CoreBankingTest.CORE.Interfaces;

public interface IPdfGenerationService
{
    Task<byte[]> GenerateAccountStatementAsync(Account account, List<Transaction> transactions, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

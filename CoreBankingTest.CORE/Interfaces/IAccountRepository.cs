using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Models;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Interfaces
{
    public interface IAccountRepository
    {
        Task<List<Account>> GetAllAsync();
        Task<Account?> GetByIdAsync(AccountId accountId);
        Task<Account?> GetByAccountNumberAsync(AccountNumber accountNumber);
        Task<IEnumerable<Account>> GetByCustomerIdAsync(CustomerId customerId);
        Task AddAsync(Account account);
        Task UpdateAsync(Account account);
        Task<bool> AccountNumberExistsAsync(AccountNumber account);

        // Background job related methods
        Task<List<Account>> GetActiveAccountsAsync(CancellationToken cancellationToken = default);
        Task<List<Account>> GetInterestBearingAccountsAsync(CancellationToken cancellationToken = default);
        Task<List<Account>> GetInactiveAccountsSinceAsync(DateTime sinceDate, CancellationToken cancellationToken = default);
        Task<List<Account>> GetAccountsByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<List<Account>> GetAccountsWithLowBalanceAsync(decimal minimumBalance, CancellationToken cancellationToken = default);
    }
}

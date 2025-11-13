using CoreBanking.Core.Exceptions;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.Models;
using CoreBankingTest.CORE.ValueObjects;
using CoreBankingTest.DAL.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly BankingDbContext _context;

        public AccountRepository(BankingDbContext context)
        {
            _context = context;
        }

        public async Task<Account?> GetByIdAsync(AccountId accountId)
        {
           return await _context.Accounts
                .Include(a => a.Customer)
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        public async Task<List<Account>> GetAllAsync()
        {
            return await _context.Accounts
            .Include(a => a.Customer)
            .Include(a => a.Transactions)
                .ToListAsync();
        }

        public async Task<Account?> GetByAccountNumberAsync(AccountNumber accountNumber)
        {
           return await _context.Accounts
                .Include(a => a.Customer)
                .Include(a => a.Transactions)
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
        }

        public async Task<IEnumerable<Account>> GetByCustomerIdAsync(CustomerId customerId)
        {
            return await _context.Accounts
                .Where(a => a.CustomerId == customerId)
                .Include(a => a.Transactions)
                .ToListAsync();
        }

        public async Task AddAsync(Account account)
        {
            await _context.Accounts.AddAsync(account);
        }

        public async Task UpdateAsync(Account account)
        {
            _context.Accounts.Update(account);
            await Task.CompletedTask;
        }

        public async Task UpdateAccountBalanceAsync(AccountId accountId, Money newBalance)
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (account == null)
                throw new InvalidOperationException("Account not found.");

            // Replace the value object
            account.UpdateBalance(newBalance);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyException("Account was modified by another user. Please refresh and try again.");
            }
        }

        public async Task<bool> AccountNumberExistsAsync(AccountNumber accountNumber)
        {
            return await _context.Accounts
                .AnyAsync(a => a.AccountNumber == accountNumber);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // Background job related methods
        public async Task<List<Account>> GetActiveAccountsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .Include(a => a.Customer)
                .Where(a => a.Status == "Active" && a.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Account>> GetInterestBearingAccountsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .Include(a => a.Customer)
                .Where(a => a.IsInterestBearing &&
                        a.Status == "Active" &&
                        a.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Account>> GetInactiveAccountsSinceAsync(DateTime sinceDate, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .Include(a => a.Customer)
                .Where(a => a.LastActivityDate < sinceDate &&
                        a.Status == "Active" &&
                        a.Balance.Amount == 0)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Account>> GetAccountsByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .Include(a => a.Customer)
                .Where(a => a.Status == status)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Account>> GetAccountsWithLowBalanceAsync(decimal minimumBalance, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .Include(a => a.Customer)
                .Where(a => a.Balance.Amount < minimumBalance &&
                        a.Status == "Active")
                .ToListAsync(cancellationToken);
        }
    }
}


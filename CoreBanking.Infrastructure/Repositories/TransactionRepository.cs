using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Interfaces;
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
    public class TransactionRepository : ITransactionRepository
    {
        private readonly BankingDbContext _context;

        public TransactionRepository(BankingDbContext context)
        {
            _context = context;
        }

        public async Task<Transaction?> GetByIdAsync(TransactionId transactionId, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId, cancellationToken);
        }

        public async Task<IEnumerable<Transaction>> GetByAccountIdAsync(AccountId accountId, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Transaction>> GetByAccountIdAndDateRangeAsync(AccountId accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .Where(t => t.AccountId == accountId &&
                           t.Timestamp >= startDate &&
                           t.Timestamp <= endDate)
                .OrderBy(t => t.Timestamp)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            await _context.Transactions.AddAsync(transaction, cancellationToken);
        }

        public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            _context.Transactions.Update(transaction);
            await Task.CompletedTask;
        }

        // Background job related methods
        public async Task AddRangeAsync(List<Transaction> transactions, CancellationToken cancellationToken = default)
        {
            await _context.Transactions.AddRangeAsync(transactions, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<Transaction>> GetTransactionsBeforeAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .Where(t => t.Timestamp < cutoffDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Transaction>> GetRecentTransactionsByAccountAsync(AccountId accountId, DateTime sinceDate, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .Where(t => t.AccountId == accountId && t.Timestamp >= sinceDate)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Transaction>> GetTransactionsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .Where(t => t.Timestamp >= startDate && t.Timestamp <= endDate)
                .OrderBy(t => t.Timestamp)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Transaction>> GetTransactionsByAccountAndDateRangeAsync(AccountId accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .Where(t => t.AccountId == accountId &&
                           t.Timestamp >= startDate &&
                           t.Timestamp <= endDate)
                .OrderBy(t => t.Timestamp)
                .ToListAsync(cancellationToken);
        }

        public async Task<decimal> GetAverageDailyBalanceAsync(AccountId accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            // Get the account's transactions in the period
            var transactions = await _context.Transactions
                .Where(t => t.AccountId == accountId && t.Timestamp <= endDate)
                .OrderBy(t => t.Timestamp)
                .ToListAsync(cancellationToken);

            if (!transactions.Any())
                return 0;

            // Calculate running balance for each day
            var days = (endDate - startDate).Days + 1;
            decimal totalBalance = 0;
            decimal currentBalance = 0;

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Get transactions for this day
                var dayTransactions = transactions.Where(t => t.Timestamp.Date == date.Date).ToList();

                foreach (var transaction in dayTransactions)
                {
                    currentBalance += transaction.Amount.Amount;
                }

                totalBalance += currentBalance;
            }

            return days > 0 ? totalBalance / days : 0;
        }
    }
}

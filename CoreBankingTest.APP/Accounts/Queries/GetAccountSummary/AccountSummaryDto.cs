using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Accounts.Queries.GetAccountSummary
{

    public record AccountSummaryDto
    {
        public AccountNumber AccountNumber { get; init; } = null!;
        public string AccountType { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public decimal Balance { get; init; }
        public string Currency { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime DateOpened { get; init; }
    }
}

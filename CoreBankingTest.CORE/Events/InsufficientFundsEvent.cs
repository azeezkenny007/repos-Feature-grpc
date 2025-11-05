using CoreBankingTest.CORE.Common;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Events
{
    public  record InsufficientFundsEvent: DomainEvent
    {
        public AccountNumber AccountNumber { get; }
        public Money RequestedAmount { get; }
        public Money CurrentBalance { get; }
        public string Operation { get; }

        public InsufficientFundsEvent(AccountNumber accountNumber, Money requestedAmount, Money currentBalance, string operation)
        {
            AccountNumber = accountNumber;
            RequestedAmount = requestedAmount;
            CurrentBalance = currentBalance;
            Operation = operation;
        }
    }
}

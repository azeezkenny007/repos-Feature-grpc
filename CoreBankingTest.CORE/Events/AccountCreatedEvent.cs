using CoreBankingTest.CORE.Common;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Events
{
    public record AccountCreatedEvent : DomainEvent
    {
        public AccountId AccountId { get; }
        public AccountNumber AccountNumber { get; }
        public CustomerId CustomerId { get; }
        public AccountType AccountType { get; }
        public Money InitialDeposit { get; }

        public AccountCreatedEvent(AccountId accountId, AccountNumber accountNumber, CustomerId customerId, AccountType accountType, Money initialDeposit)
        {
            AccountId = accountId;
            AccountNumber = accountNumber;
            CustomerId = customerId;
            AccountType = accountType;
            InitialDeposit = initialDeposit;
        }
    }
}

using CoreBankingTest.CORE.Common;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Events
{
    public record MoneyTransferredEvent : DomainEvent
    {
        public TransactionId TransactionId { get;}
        public AccountNumber SourceAccountNumber { get; }
        public AccountNumber DestinationAccountNumber { get; }
        public Money Amount { get; }
        public string Reference { get; }
        public DateTime OccurredOn { get; }

        public DateTime TransferDate { get; }

        public MoneyTransferredEvent(TransactionId transactionId,AccountNumber sourceAccount, AccountNumber destinationAccount, Money amount, string reference)
        {
             
            TransactionId = transactionId;
            SourceAccountNumber = sourceAccount;
            DestinationAccountNumber = destinationAccount;
            Amount = amount;
            Reference = reference;
            TransferDate = DateTime.Now;

        }
    }
}

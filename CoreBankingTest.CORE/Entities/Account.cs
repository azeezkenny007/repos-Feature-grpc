using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using CoreBankingTest.CORE.Events;
using CoreBankingTest.CORE.Common;
using CoreBanking.Core.Exceptions;




namespace CoreBankingTest.CORE.Entities
{
    public class Account : AggregateRoot<AccountId>, ISoftDelete
    {
        public AccountId AccountId { get; private set; }
        public AccountNumber AccountNumber { get; private set; }
        public AccountType AccountType { get; private set; }
        public Money Balance { get; private set; }
        public CustomerId CustomerId { get; private set; }
        public Customer Customer { get; private set; }
        public DateTime DateOpened { get; private set; }
        public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
        public bool IsActive { get; private set; }
        public bool IsDeleted { get; private set; }
        public DateTime? DeletedAt { get; private set; }
        public string? DeletedBy { get; private set; }

        // Properties for background job processing
        public DateTime LastActivityDate { get; private set; } = DateTime.UtcNow;
        public string Status { get; private set; } = "Active"; // Active, Inactive, Closed, Suspended
        public bool IsInterestBearing { get; private set; } = true;
        public bool IsArchived { get; private set; } = false;

        // Computed property for current balance
        public decimal CurrentBalance => Balance.Amount;

        private readonly List<DomainEvent> _domainEvents = new();
        public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        // Navigation properties - private to enforce aggregate boundary
        private readonly List<Transaction> _transactions = new();
        public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

        private Account() { } // EF Core needs this

        // Option 1: Keep existing constructor for internal use
        private Account(AccountNumber accountNumber, AccountType accountType, CustomerId customerId)
        {
            AccountId = AccountId.Create();
            AccountNumber = accountNumber;
            AccountType = accountType;
            CustomerId = customerId;
            Balance = new Money(0);
            DateOpened = DateTime.UtcNow;
            IsActive = true;
        }

        // Core banking operations - these are the aggregate's public API
        public Transaction Deposit(Money amount, Account account, string description = "Deposit")
        {
            if (!IsActive)
                throw new InvalidOperationException("Cannot deposit to inactive account");

            if (amount.Amount <= 0)
                throw new ArgumentException("Deposit amount must be positive");

            Balance += amount;

            var transaction = new Transaction(
                accountId: AccountId,
                account: account,
                type: TransactionType.Deposit,
                amount: amount,
                description: description
            );

            _transactions.Add(transaction);
            return transaction;
        }

        public Transaction Withdraw(Money amount, Account account, string description = "Withdrawal")
        {
            if (!IsActive)
                throw new InvalidOperationException("Cannot withdraw from inactive account");

            if (amount.Amount <= 0)
                throw new ArgumentException("Withdrawal amount must be positive");

            if (Balance.Amount < amount.Amount)
                throw new InvalidOperationException("Insufficient funds");

            // Special business rule for Savings accounts
            if (AccountType == AccountType.Savings && _transactions.Count(t => t.Type == TransactionType.Withdrawal) >= 6)
                throw new InvalidOperationException("Savings account withdrawal limit reached");

            Balance -= amount;

            var transaction = new Transaction(
                accountId: AccountId,
                account: account,
                type: TransactionType.Withdrawal,
                amount: amount,
                description: description
            );

            _transactions.Add(transaction);
            return transaction;
        }

        public static Account Create(
            CustomerId customerId,
            AccountNumber accountNumber,
            AccountType accountType,
            Money initialBalance)
        {
            // Domain validation
            if (initialBalance.Amount < 0)
                throw new InvalidOperationException("Initial balance cannot be negative");

            if (initialBalance.Amount > 1000000)
                throw new InvalidOperationException("Initial deposit too large");

            // Create account using private constructor
            var account = new Account(
                accountNumber: accountNumber,
                accountType: accountType,
                customerId: customerId
            )
            {
                Balance = initialBalance // Set initial balance after construction
            };

            // Raise domain event if needed
            account.AddDomainEvent(new AccountCreatedEvent(
                accountId: account.AccountId,
                accountNumber: account.AccountNumber,
                customerId: account.CustomerId,
                accountType: account.AccountType,
                initialDeposit: account.Balance
            ));

            return account;
        }

        public Result<Transaction> Transfer(Money transferAmount, Account destination, string reference, string transferDescription)
        {
            // Validate inputs
            if (destination == null)
                throw new ArgumentNullException(nameof(destination), "Destination account cannot be null");

            if (transferAmount.Amount <= 0)
                throw new InvalidOperationException("Transfer amount must be positive");

            if (this == destination)
                throw new InvalidOperationException("Cannot transfer to the same account");

            // Check source account conditions
            if (!IsActive)
                throw new InvalidOperationException("Source account is not active");

            if (!destination.IsActive)
                throw new InvalidOperationException("Destination account is not active");

            // Check sufficient funds
            if (Balance.Amount < transferAmount.Amount)
            {
                // Raise insufficient funds event
                _domainEvents.Add(new InsufficientFundsEvent(
                    AccountNumber, transferAmount, Balance, "Transfer"));

                return (Result<Transaction>)Result<Transaction>.Failure("Insufficient funds for transfer");
            }

            // Special business rules for Savings accounts
            if (AccountType == AccountType.Savings &&
                _transactions.Count(t => t.Type == Enums.TransactionType.Withdrawal) >= 6)
            {
                return (Result<Transaction>)Result<Transaction>.Failure("Savings account withdrawal limit reached");
            }

            // Execute the transfer as an atomic operation
            var debitResult = Debit(transferAmount, $"Transfer to {destination.AccountNumber}", reference);
            if (!debitResult.IsSuccess)
                return (Result<Transaction>)Result<Transaction>.Failure(debitResult.Error);

            var creditResult = destination.Credit(transferAmount, $"Transfer from {AccountNumber}", reference);
            if (!creditResult.IsSuccess)
                return (Result<Transaction>)Result<Transaction>.Failure(creditResult.Error);

            // Create transaction record
            var transactionId = TransactionId.Create();
            var transaction = new Transaction(
                this.AccountId,
                Enums.TransactionType.TransferOut,
                transferAmount,
                transferDescription,
                this,
                reference);

            // Add transaction to both accounts' transaction collections
            _transactions.Add(transaction);
            //destination.AddTransaction(transaction);

            // Raise money transferred event
            _domainEvents.Add(new MoneyTransferredEvent(
                transactionId,
                this.AccountNumber,
                destination.AccountNumber,
                transferAmount,
                reference));

            // Return success result with the created transaction
            return Result<Transaction>.Success(transaction);
        }

        public Result Debit(Money amount, string description, string reference)
        {
            if (IsDeleted)
                return Result.Failure("Cannot debit a deleted account");

            if (amount.Amount <= 0)
                return Result.Failure("Debit amount must be positive");

            if (Balance.Amount < amount.Amount)
                return Result.Failure("Insufficient funds");

            // Apply debit
            Balance -= amount;

            // Record transaction (matches your Transaction constructor)
            var transaction = new Transaction(
                AccountId,                            // AccountId
                TransactionType.Withdrawal,    // Transaction type
                amount,                        // Amount
                description,                   // Description
                this,                          // Account reference
                reference                      // Optional reference
            );

            _transactions.Add(transaction);

            // Raise domain event
            //AddDomainEvent(new AccountDebitedEvent(Id, amount, reference));

            return Result.Success();
        }

        public Result Credit(Money amount, string description, string reference)
        {
            if (IsDeleted)
                return Result.Failure("Cannot credit a deleted account");

            if (amount.Amount <= 0)
                return Result.Failure("Credit amount must be positive");

            // Apply credit
            Balance += amount;

            // Record transaction (matches your Transaction constructor)
            var transaction = new Transaction(
                AccountId,                          // AccountId
                TransactionType.Deposit,     // Transaction type
                amount,                      // Amount
                description,                 // Description
                this,                        // Account reference
                reference                    // Optional reference
            );

            _transactions.Add(transaction);

            // Raise domain event
            //AddDomainEvent(new AccountCreditedEvent(Id, amount, reference));

            return Result.Success();
        }

        // Domain event methods
        public void AddDomainEvent(DomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }

        public void CloseAccount()
        {
            if (Balance.Amount != 0)
                throw new InvalidOperationException("Cannot close account with non-zero balance");

            IsActive = false;
        }

        public void UpdateBalance(Money newBalance)
        {
            if (!IsActive)
                throw new InvalidOperationException("Cannot update balance for inactive account.");

            Balance = newBalance;
        }

        // Methods for background job maintenance
        public void MarkAsClosed()
        {
            Status = "Closed";
            LastActivityDate = DateTime.UtcNow;
            IsActive = false;
        }

        public void MarkAsArchived()
        {
            IsArchived = true;
            LastActivityDate = DateTime.UtcNow;
        }

        public void UpdateStatusBasedOnRules()
        {
            // Business rules for status updates
            if (LastActivityDate < DateTime.UtcNow.AddYears(-1) && Status == "Active")
            {
                Status = "Inactive";
            }
        }

        public void UpdateLastActivityDate()
        {
            LastActivityDate = DateTime.UtcNow;
        }

        public void SetInterestBearing(bool isInterestBearing)
        {
            IsInterestBearing = isInterestBearing;
        }
    }

}
# CoreBanking Project - Complete File Guide

## 📂 Project Structure

```
repos-Feature-grpc/
├── CoreBankingTest.CORE/              ← Domain Layer (Business Logic)
│   ├── Common/
│   │   ├── AggregateRoot.cs
│   │   ├── DomainEvent.cs
│   │   └── Result.cs
│   ├── Entities/
│   │   ├── Customer.cs
│   │   ├── Account.cs
│   │   └── Transaction.cs
│   ├── ValueObjects/
│   │   ├── CustomerId.cs
│   │   ├── AccountId.cs
│   │   ├── TransactionId.cs
│   │   ├── AccountNumber.cs
│   │   └── Money.cs
│   ├── Enums/
│   │   ├── AccountType.cs
│   │   └── TransactionType.cs
│   ├── Events/
│   │   ├── AccountCreatedEvent.cs
│   │   ├── MoneyTransferredEvent.cs
│   │   └── InsufficientFundsEvent.cs
│   ├── Interfaces/
│   │   ├── IAccountRepository.cs
│   │   ├── ICustomerRepository.cs
│   │   ├── ITransactionRepository.cs
│   │   ├── IUnitOfWork.cs
│   │   ├── IAggregateRoot.cs
│   │   └── ISoftDelete.cs
│   └── Exceptions/
│       ├── ConcurrencyException.cs
│       └── InsufficientFundsException.cs
│
├── CoreBankingTest.APP/               ← Application Layer (Use Cases)
│   ├── Common/
│   │   ├── Behaviors/
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── DomainEventsBehavior.cs
│   │   │   └── LoggingBehavior.cs
│   │   ├── Interfaces/
│   │   │   ├── ICommand.cs
│   │   │   ├── IDomainDispatcher.cs
│   │   │   └── IOutboxMessageProcessor.cs
│   │   ├── Mappings/
│   │   │   ├── AccountProfile.cs
│   │   │   ├── CustomerProfile.cs
│   │   │   └── TransactionProfile.cs
│   │   └── Models/
│   │       └── Result.cs
│   ├── Accounts/
│   │   ├── Commands/
│   │   │   ├── CreateAccount/
│   │   │   │   ├── CreateAccountCommand.cs
│   │   │   │   └── CreateAccountCommandValidator.cs
│   │   │   └── TransferMoney/
│   │   │       ├── TransferMoneyCommand.cs
│   │   │       └── TransferMoneyCommandValidator.cs
│   │   ├── Queries/
│   │   │   ├── GetAccountDetails/
│   │   │   │   └── GetAccountDetailsQuery.cs
│   │   │   ├── GetAccountSummary/
│   │   │   │   └── AccountSummaryDto.cs
│   │   │   └── GetTransactionHistory/
│   │   │       └── GetTransactionHistoryQuery.cs
│   │   └── EventHandlers/
│   │       ├── AccountCreatedEventHandler.cs
│   │       ├── MoneyTransferedEventHandler.cs
│   │       └── InsufficientFundsEventHandler.cs
│   └── Customers/
│       ├── Commands/
│       │   └── CreateCustomer/
│       │       ├── CreateCustomerCommand.cs
│       │       └── CreateCustomerCommandValidator.cs
│       └── Queries/
│           ├── GetCustomers/
│           │   └── GetCustomersQuery.cs
│           └── GetCustomersDetails/
│               └── GetCustomersDetailsQuery.cs
│
└── CoreBanking.Infrastructure/        ← Infrastructure Layer (Data Access)
    ├── Data/
    │   ├── BankingDbContext.cs        (EF Core DbContext)
    │   └── UnitOfWork.cs
    ├── Persistence/
    │   ├── Configurations/
    │   │   └── OutBoxMessageConfiguration.cs
    │   └── Outbox/
    │       └── OutBoxMessage.cs
    ├── Repositories/
    │   ├── AccountRepository.cs
    │   ├── CustomerRepository.cs
    │   └── TransactionRepository.cs
    ├── Services/
    │   ├── DomainEventDispatcher.cs
    │   ├── OutBoxBackgroundService.cs
    │   └── OutboxMessageProcessor.cs
    └── Migrations/
        ├── [Multiple migration files]
        └── BankingDbContextModelSnapshot.cs
```

---

# 📋 File-by-File Explanation

## DOMAIN LAYER (CoreBankingTest.CORE)

### 1. **Common/AggregateRoot.cs**
**Purpose**: Base class for aggregate roots in DDD pattern

```csharp
public abstract class AggregateRoot<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

**What it does**:
- Provides base functionality for entities that emit domain events
- `_domainEvents`: Stores events created during entity operations
- `DomainEvents`: Public read-only access to events
- `AddDomainEvent()`: Allows subclasses to emit events
- `ClearDomainEvents()`: Removes events after they're processed

**Used by**: Account (aggregate root)

---

### 2. **Common/DomainEvent.cs**
**Purpose**: Base class for all domain events

```csharp
public abstract record DomainEvent : IDomainEvent, INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
}
```

**What it does**:
- Defines the structure of all events
- `EventId`: Unique identifier for each event
- `OccurredOn`: Timestamp when event occurred
- `EventType`: Name of the event class
- `INotification`: Integrates with MediatR for event publishing

**Used by**: AccountCreatedEvent, MoneyTransferredEvent, InsufficientFundsEvent

---

### 3. **Common/Result.cs**
**Purpose**: Provides result pattern for operation outcomes

**What it does**:
- `Result<T>`: Generic result with success/failure
- `Result`: Non-generic result for void operations
- Contains data or error message

---

### 4. **Entities/Customer.cs**
**Purpose**: Represents a customer entity

**Key Properties**:
```csharp
- CustomerId: Unique identifier (Value Object)
- FirstName, LastName: Customer name
- Email: Unique email address
- PhoneNumber, Address
- DateOfBirth, DateCreated
- IsActive, IsDeleted: Status flags
- Accounts: Collection of customer's accounts (1-to-Many)
```

**Key Methods**:
```csharp
- Create(): Factory method to create new customer
- UpdateContactInfo(email, phone): Update contact details
- Deactivate(): Deactivate account (only if no balance)
- SoftDelete(deletedBy): Soft-delete customer
- AddAccount(account): Add account to customer
```

**Implements**: `ISoftDelete` (supports logical deletion)

**Usage**: Represents a person who owns bank accounts

---

### 5. **Entities/Account.cs**
**Purpose**: Core banking aggregate root - represents a bank account

**Key Properties**:
```csharp
- AccountId: Unique identifier (Value Object)
- AccountNumber: Human-readable account number (Value Object)
- Balance: Amount of money (Money Value Object)
- CustomerId: Reference to owner
- AccountType: Checking/Savings (Enum)
- RowVersion: For optimistic concurrency control
- _domainEvents: Events emitted by account operations
- _transactions: Account's transaction history
- IsActive, IsDeleted: Status flags
```

**Key Methods**:

**Create()** - Factory method:
```csharp
var account = Account.Create(
    customerId,
    accountNumber,
    AccountType.Checking,
    new Money(1000)
);
// Emits AccountCreatedEvent
```

**Deposit()** - Add money:
```csharp
var transaction = account.Deposit(
    new Money(500),
    account,
    "Salary deposit"
);
// Returns transaction object
```

**Withdraw()** - Remove money:
```csharp
var transaction = account.Withdraw(
    new Money(200),
    account,
    "Cash withdrawal"
);
// Throws InsufficientFundsException if not enough funds
// Checks Savings account withdrawal limit (max 6/month)
```

**Transfer()** - Money transfer between accounts:
```csharp
var result = sourceAccount.Transfer(
    amount: new Money(300),
    destination: destinationAccount,
    reference: "REF123",
    description: "Payment"
);
// Emits MoneyTransferredEvent
// Checks source & destination active
// Validates sufficient funds
// Atomic operation (both debit & credit)
```

**Debit()** - Internal debit operation:
- Removes money without creating transaction
- Used by Transfer()

**Credit()** - Internal credit operation:
- Adds money without creating transaction
- Used by Transfer()

**CloseAccount()** - Close account:
- Only if balance is 0

**UpdateBalance()** - Update balance:
- Direct balance update for reconciliation

---

### 6. **Entities/Transaction.cs**
**Purpose**: Records a banking transaction

**Key Properties**:
```csharp
- TransactionId: Unique identifier (Value Object)
- AccountId: Reference to account
- Type: Deposit/Withdrawal (Enum)
- Amount: Transaction amount (Money Value Object)
- Description: What the transaction is for
- Timestamp: When it occurred
- Reference: Optional reference number
- IsDeleted: Soft delete flag
```

**Constructor**:
```csharp
public Transaction(
    AccountId accountId,
    TransactionType type,
    Money amount,
    string description,
    Account account,
    string reference = ""
)
```

**Methods**:
- `GenerateReference()`: Auto-generates reference if not provided

---

### 7. **ValueObjects/CustomerId.cs**
**Purpose**: Strongly-typed customer ID

```csharp
public record CustomerId
{
    public Guid Value { get; init; }
    
    public static CustomerId Create() => new(Guid.NewGuid());
    public static CustomerId Create(Guid value) => new(value);
}
```

**Why Value Objects**:
- Type safety: Can't mix up CustomerId with AccountId
- Encapsulates ID generation logic
- Immutable (record)

---

### 8. **ValueObjects/AccountId.cs**
**Purpose**: Strongly-typed account ID

```csharp
public record AccountId
{
    public Guid Value { get; init; }
    
    public static AccountId Create() => new(Guid.NewGuid());
    public static AccountId Create(Guid value) => new(value);
}
```

---

### 9. **ValueObjects/TransactionId.cs**
**Purpose**: Strongly-typed transaction ID

```csharp
public record TransactionId
{
    public Guid Value { get; init; }
    
    public static TransactionId Create() => new(Guid.NewGuid());
}
```

---

### 10. **ValueObjects/AccountNumber.cs**
**Purpose**: Enforces account number format (10 digits)

```csharp
public record AccountNumber
{
    public string Value { get; }
    
    public AccountNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 10)
            throw new ArgumentException("Account number must be 10 digits");
        if (!value.All(char.IsDigit))
            throw new ArgumentException("Account number must contain only digits");
        
        Value = value;
    }
    
    public static AccountNumber Create(string value) => new(value);
}
```

**Validation**: Ensures account number is exactly 10 digits

---

### 11. **ValueObjects/Money.cs**
**Purpose**: Represents money with currency and amount

```csharp
public record Money
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "NGN";
    
    public Money(decimal amount, string currency = "NGN")
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative");
        
        Amount = amount;
        Currency = currency;
    }
    
    // Operator overloads
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Cannot add different currencies");
        return new Money(a.Amount + b.Amount, a.Currency);
    }
    
    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Cannot subtract different currencies");
        return new Money(a.Amount - b.Amount, a.Currency);
    }
}
```

**What it does**:
- Enforces amount ≥ 0
- Prevents currency mixing
- Operator overloads for math operations

---

### 12. **Enums/AccountType.cs**
```csharp
public enum AccountType
{
    Checking,      // Can withdraw unlimited times
    Savings        // Limited to 6 withdrawals per month
}
```

---

### 13. **Enums/TransactionType.cs**
```csharp
public enum TransactionType
{
    Deposit,       // Money in
    Withdrawal,    // Money out
    Transfer       // Money between accounts
}
```

---

### 14. **Events/AccountCreatedEvent.cs**
**Purpose**: Emitted when account is created

```csharp
public record AccountCreatedEvent : DomainEvent
{
    public AccountId AccountId { get; }
    public AccountNumber AccountNumber { get; }
    public CustomerId CustomerId { get; }
    public AccountType AccountType { get; }
    public Money InitialDeposit { get; }
}
```

**When it fires**: In `Account.Create()` after account is initialized

---

### 15. **Events/MoneyTransferredEvent.cs**
**Purpose**: Emitted when money is transferred between accounts

```csharp
public record MoneyTransferredEvent : DomainEvent
{
    public TransactionId TransactionId { get; }
    public AccountNumber SourceAccountNumber { get; }
    public AccountNumber DestinationAccountNumber { get; }
    public Money Amount { get; }
    public string Reference { get; }
    public DateTime TransferDate { get; }
}
```

**When it fires**: In `Account.Transfer()` after successful transfer

---

### 16. **Events/InsufficientFundsEvent.cs**
**Purpose**: Emitted when transfer fails due to insufficient funds

```csharp
public record InsufficientFundsEvent: DomainEvent
{
    public AccountNumber AccountNumber { get; }
    public Money RequestedAmount { get; }
    public Money CurrentBalance { get; }
    public string Operation { get; }
}
```

**When it fires**: In `Account.Transfer()` when balance < requested amount

---

### 17. **Exceptions/InsufficientFundsException.cs**
```csharp
public class InsufficientFundsException : Exception
{
    public decimal RequiredAmount { get; }
    public decimal AvailableBalance { get; }
}
```

**Thrown by**: `Account.Withdraw()`, `Account.Transfer()`

---

### 18. **Exceptions/ConcurrencyException.cs**
```csharp
public class ConcurrencyException : Exception
{
    // Thrown when RowVersion check fails in AccountRepository
}
```

---

### 19. **Interfaces/IAccountRepository.cs**
**Purpose**: Contract for account data access

```csharp
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(AccountId accountId);
    Task<List<Account>> GetAllAsync();
    Task<Account?> GetByAccountNumberAsync(AccountNumber accountNumber);
    Task<IEnumerable<Account>> GetByCustomerIdAsync(CustomerId customerId);
    Task AddAsync(Account account);
    Task UpdateAsync(Account account);
    Task UpdateAccountBalanceAsync(AccountId accountId, Money newBalance);
    Task<bool> AccountNumberExistsAsync(AccountNumber accountNumber);
    Task SaveChangesAsync();
}
```

---

### 20. **Interfaces/ICustomerRepository.cs**
**Purpose**: Contract for customer data access

```csharp
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId customerId);
    Task<IEnumerable<Customer>> GetAllAsync();
    Task AddAsync(Customer customer);
    Task UpdateAsync(Customer customer);
    Task<bool> ExistsAsync(CustomerId customerId);
    Task<bool> EmailExistsAsync(string email);
    Task SaveChangesAsync();
}
```

---

### 21. **Interfaces/ITransactionRepository.cs**
**Purpose**: Contract for transaction data access

```csharp
public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(TransactionId transactionId, CancellationToken ct);
    Task<IEnumerable<Transaction>> GetByAccountIdAsync(AccountId accountId, CancellationToken ct);
    Task<IEnumerable<Transaction>> GetByAccountIdAndDateRangeAsync(
        AccountId accountId, DateTime start, DateTime end, CancellationToken ct);
    Task AddAsync(Transaction transaction, CancellationToken ct);
    Task UpdateAsync(Transaction transaction, CancellationToken ct);
}
```

---

### 22. **Interfaces/IUnitOfWork.cs**
**Purpose**: Transaction management

```csharp
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}
```

---

### 23. **Interfaces/ISoftDelete.cs**
**Purpose**: Marks entities that support soft deletion

```csharp
public interface ISoftDelete
{
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
    string? DeletedBy { get; }
}
```

---

### 24. **Interfaces/IAggregateRoot.cs**
**Purpose**: Marks aggregate roots

```csharp
public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
}
```

---

## APPLICATION LAYER (CoreBankingTest.APP)

### 1. **Common/Interfaces/ICommand.cs**
**Purpose**: Marker interface for commands

```csharp
public interface ICommand : IRequest<Result> { }
public interface ICommand<out TResponse> : IRequest<Result<TResponse>> { }
```

**Usage**: All command classes inherit this to indicate they're CQRS commands

---

### 2. **Common/Behaviors/ValidationBehavior.cs**
**Purpose**: MediatR pipeline behavior that validates requests

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Get all validators for TRequest
        var validationResults = await Task.WhenAll(
            _validators.Select(x => x.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken))
        );
        
        var failures = validationResults.SelectMany(x => x.Errors).Where(f => f != null).ToList();
        
        if (failures.Any())
        {
            // Return failure result with error messages
            return (TResponse)Result<>.Failure(failures.Select(f => f.ErrorMessage).ToArray());
        }
        
        return await next(); // Continue to handler
    }
}
```

**How it works**:
1. Before handler executes, validates the request
2. Uses FluentValidation validators
3. If validation fails, returns error immediately (short-circuit)
4. If validation passes, continues to handler

---

### 3. **Common/Behaviors/DomainEventsBehavior.cs**
**Purpose**: MediatR pipeline behavior that dispatches domain events

```csharp
public class DomainEventsBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(); // Execute handler
        
        // After handler completes, dispatch any domain events
        await _dispatcher.DispatchDomainEventsAsync(cancellationToken);
        
        return response;
    }
}
```

**Pipeline Order**:
1. Validation runs first
2. Handler executes (business logic)
3. Domain events dispatched
4. Response returned

---

### 4. **Common/Behaviors/LoggingBehavior.cs**
**Purpose**: Logs all request/response activity

**What it does**:
- Logs request start
- Logs handler execution
- Logs response or errors
- Useful for debugging and audit trail

---

### 5. **Common/Models/Result.cs**
**Purpose**: Standard result pattern for operations

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public string[] Errors { get; }
    
    public static Result Success() => new(true);
    public static Result Failure(params string[] errors) => new(false, errors);
}

public class Result<T> : Result
{
    public T? Data { get; }
    
    public static Result<T> Success(T data) => new(true, data);
    public static Result<T> Failure(params string[] errors) => new(false, errors);
}
```

**Usage**: Handlers return Result to indicate success/failure

---

### 6. **Common/Mappings/AccountProfile.cs**
**Purpose**: AutoMapper profile for Account entity ↔ DTO mapping

```csharp
public class AccountProfile : Profile
{
    public AccountProfile()
    {
        CreateMap<Account, AccountSummaryDto>()
            .ForMember(dest => dest.Balance, opt => opt.MapFrom(src => src.Balance.Amount))
            .ForMember(dest => dest.Currency, opt => opt.MapFrom(src => src.Balance.Currency));
    }
}
```

---

### 7. **Common/Mappings/CustomerProfile.cs**
**Purpose**: AutoMapper profile for Customer entity ↔ DTO mapping

---

### 8. **Common/Mappings/TransactionProfile.cs**
**Purpose**: AutoMapper profile for Transaction entity ↔ DTO mapping

---

### 9. **Accounts/Commands/CreateAccount/CreateAccountCommand.cs**
**Purpose**: CQRS command to create a new account

```csharp
public record CreateAccountCommand : ICommand<Guid>
{
    public CustomerId CustomerId { get; init; }
    public string AccountType { get; init; }
    public decimal InitialDeposit { get; init; }
    public string Currency { get; init; } = "NGN";
}

public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate customer exists
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
        if (customer == null)
            return Result<Guid>.Failure("Customer not found");
        
        // 2. Generate unique account number
        var accountNumber = await GenerateUniqueAccountNumberAsync();
        
        // 3. Create account (triggers AccountCreatedEvent)
        var account = Account.Create(
            customerId: request.CustomerId,
            accountNumber: accountNumber,
            accountType: Enum.Parse<AccountType>(request.AccountType),
            initialDeposit: new Money(request.InitialDeposit, request.Currency)
        );
        
        // 4. Add to repository & save
        await _accountRepository.AddAsync(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result<Guid>.Success(account.AccountId.Value);
    }
}
```

**Flow**:
- Request comes in → ValidationBehavior checks inputs
- → CreateAccountCommandHandler executes → Creates Account aggregate
- → DomainEventsBehavior dispatches AccountCreatedEvent
- → Event handlers execute (send notification, log, etc.)

---

### 10. **Accounts/Commands/CreateAccount/CreateAccountCommandValidator.cs**
**Purpose**: FluentValidation rules for CreateAccountCommand

```csharp
public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required");
        
        RuleFor(x => x.AccountType)
            .Must(x => x == "Checking" || x == "Savings")
            .WithMessage("Account type must be Checking or Savings");
        
        RuleFor(x => x.InitialDeposit)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1000000)
            .WithMessage("Initial deposit must be between 0 and 1,000,000");
    }
}
```

---

### 11. **Accounts/Commands/TransferMoney/TransferMoneyCommand.cs**
**Purpose**: CQRS command to transfer money between accounts

```csharp
public record TransferMoneyCommand : ICommand
{
    public AccountNumber SourceAccountNumber { get; init; }
    public AccountNumber DestinationAccountNumber { get; init; }
    public Money Amount { get; init; }
    public string Reference { get; init; }
    public string Description { get; init; }
}

public class TransferMoneyCommandHandler : IRequestHandler<TransferMoneyCommand, Result>
{
    public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Get accounts
            var sourceAccount = await _accountRepository.GetByAccountNumberAsync(
                new AccountNumber(request.SourceAccountNumber));
            var destAccount = await _accountRepository.GetByAccountNumberAsync(
                new AccountNumber(request.DestinationAccountNumber));
            
            if (sourceAccount == null) return Result.Failure("Source account not found");
            if (destAccount == null) return Result.Failure("Destination account not found");
            
            // 2. Execute transfer (raises MoneyTransferredEvent)
            sourceAccount.Transfer(
                amount: request.Amount,
                destination: destAccount,
                reference: request.Reference,
                description: request.Description
            );
            
            // 3. Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (InsufficientFundsException ex)
        {
            return Result.Failure($"Insufficient funds: Required {ex.RequiredAmount}, Available {ex.AvailableBalance}");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
```

---

### 12. **Accounts/Commands/TransferMoney/TransferMoneyCommandValidator.cs**
**Purpose**: Validates TransferMoneyCommand

---

### 13. **Accounts/Queries/GetAccountDetails/GetAccountDetailsQuery.cs**
**Purpose**: CQRS query to fetch account details

```csharp
public record GetAccountDetailsQuery : IRequest<Result<AccountDetailsDto>>
{
    public AccountId AccountId { get; init; }
}
```

---

### 14. **Accounts/Queries/GetAccountSummary/AccountSummaryDto.cs**
**Purpose**: Data Transfer Object for account summary

```csharp
public class AccountSummaryDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; }
    public string AccountType { get; set; }
    public bool IsActive { get; set; }
}
```

---

### 15. **Accounts/Queries/GetTransactionHistory/GetTransactionHistoryQuery.cs**
**Purpose**: Query to fetch transaction history for an account

---

### 16. **Accounts/Queries/GetTransactionHistory/TransactionDto.cs**
**Purpose**: Data Transfer Object for transaction

```csharp
public class TransactionDto
{
    public Guid TransactionId { get; set; }
    public string Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public string Reference { get; set; }
}
```

---

### 17. **Accounts/EventHandlers/AccountCreatedEventHandler.cs**
**Purpose**: Handles AccountCreatedEvent

```csharp
public class AccountCreatedEventHandler : INotificationHandler<AccountCreatedEvent>
{
    public async Task Handle(AccountCreatedEvent @event, CancellationToken cancellationToken)
    {
        // Send welcome email
        // Log account creation
        // Update analytics
        // etc.
    }
}
```

**When it fires**: After Account.Create() completes

---

### 18. **Accounts/EventHandlers/MoneyTransferedEventHandler.cs**
**Purpose**: Handles MoneyTransferredEvent

---

### 19. **Accounts/EventHandlers/InsufficientFundsEventHandler.cs**
**Purpose**: Handles InsufficientFundsEvent

```csharp
public class InsufficientFundsEventHandler : INotificationHandler<InsufficientFundsEvent>
{
    public async Task Handle(InsufficientFundsEvent @event, CancellationToken cancellationToken)
    {
        // Log failed transfer attempt
        // Alert customer
        // Trigger fraud detection
        // etc.
    }
}
```

---

### 20. **Customers/Commands/CreateCustomer/CreateCustomerCommand.cs**
**Purpose**: CQRS command to create a new customer

```csharp
public record CreateCustomerCommand : ICommand<Guid>
{
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public string Email { get; init; }
    public string PhoneNumber { get; init; }
    public string Address { get; init; }
    public DateTime DateOfBirth { get; init; }
}
```

---

### 21. **Customers/Commands/CreateCustomer/CreateCustomerCommandValidator.cs**
**Purpose**: Validates CreateCustomerCommand

---

### 22. **Customers/Queries/GetCustomers/GetCustomersQuery.cs**
**Purpose**: Query to fetch all customers

---

### 23. **Customers/Queries/GetCustomers/CustomerDto.cs**
**Purpose**: Data Transfer Object for customer

---

### 24. **Customers/Queries/GetCustomersDetails/GetCustomersDetailsQuery.cs**
**Purpose**: Query to fetch detailed customer information with accounts

---

### 25. **Customers/Queries/GetCustomersDetails/CustomerDetailsDto.cs**
**Purpose**: Detailed customer DTO with accounts

---

## INFRASTRUCTURE LAYER (CoreBanking.Infrastructure)

### 1. **Data/BankingDbContext.cs**
**Purpose**: EF Core DbContext - database mapping and Outbox pattern implementation

**Key Features**:

```csharp
public class BankingDbContext : DbContext
{
    // DbSets - map to database tables
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<OutBoxMessage> OutboxMessages { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. Configure Outbox table
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        
        // 2. Ignore DomainEvent in-memory collections
        modelBuilder.Ignore<DomainEvent>();
        modelBuilder.Ignore<IDomainEvent>();
        
        // 3. Configure Customer entity
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.CustomerId);
            entity.Property(c => c.Email).IsUnique();
            
            // 1-to-Many: Customer has many Accounts
            entity.HasMany(c => c.Accounts)
                .WithOne(a => a.Customer)
                .HasForeignKey(a => a.CustomerId);
            
            // Global query filter - exclude deleted
            entity.HasQueryFilter(c => !c.IsDeleted);
        });
        
        // 4. Configure Account entity
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(a => a.AccountId);
            
            // Value Object conversions
            entity.Property(a => a.AccountNumber)
                .HasConversion(an => an.Value, value => AccountNumber.Create(value));
            
            // Owned type: Money value object
            entity.OwnsOne(a => a.Balance, money =>
            {
                money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
            });
            
            // Concurrency control
            entity.Property(a => a.RowVersion).IsRowVersion().IsConcurrencyToken();
            
            // 1-to-Many: Account has many Transactions
            entity.HasMany(a => a.Transactions)
                .WithOne(t => t.Account)
                .HasForeignKey(t => t.AccountId);
            
            // Query filter
            entity.HasQueryFilter(a => !a.IsDeleted);
        });
        
        // 5. Configure Transaction entity
        modelBuilder.Entity<Transaction>(entity =>
        {
            // Similar configuration as Account
        });
        
        // 6. Seed initial data
        modelBuilder.Entity<Customer>().HasData(new { ... });
        modelBuilder.Entity<Account>().HasData(new { ... });
    }
    
    // Outbox Pattern Implementation
    public async Task SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default)
    {
        // 1. Extract domain events from aggregates
        var events = ChangeTracker.Entries<AggregateRoot<AccountId>>()
            .SelectMany(x => x.Entity.DomainEvents)
            .Select(domainEvent => new OutBoxMessage
            {
                Id = Guid.NewGuid(),
                Type = domainEvent.GetType().Name,
                Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                OccurredOn = domainEvent.OccurredOn
            })
            .ToList();
        
        // 2. Clear domain events from aggregates
        ChangeTracker.Entries<AggregateRoot<AccountId>>()
            .ToList()
            .ForEach(entry => entry.Entity.ClearDomainEvents());
        
        // 3. Save changes (aggregates + outbox messages in single transaction)
        await base.SaveChangesAsync(cancellationToken);
        
        // 4. Add outbox messages
        if (events.Any())
        {
            await OutboxMessages.AddRangeAsync(events, cancellationToken);
            await base.SaveChangesAsync(cancellationToken);
        }
    }
}
```

**Key Concepts**:

**Value Object Conversion**:
```csharp
entity.Property(a => a.AccountNumber)
    .HasConversion(
        an => an.Value,           // To DB: AccountNumber → string
        value => AccountNumber.Create(value)  // From DB: string → AccountNumber
    );
```

**Owned Types** (Money):
```csharp
entity.OwnsOne(a => a.Balance, money =>
{
    money.Property(m => m.Amount).HasColumnName("Amount");
    money.Property(m => m.Currency).HasColumnName("Currency");
});
```

**Global Query Filters** (Soft Delete):
```csharp
entity.HasQueryFilter(c => !c.IsDeleted);
// All queries automatically add: WHERE IsDeleted = false
```

**Concurrency Control**:
```csharp
entity.Property(a => a.RowVersion)
    .IsRowVersion()
    .IsConcurrencyToken();
// Database maintains version, throws error if value changed
```

---

### 2. **Data/UnitOfWork.cs**
**Purpose**: Provides transaction management

```csharp
public class UnitOfWork : IUnitOfWork
{
    private readonly BankingDbContext _context;
    
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
    
    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
```

**Usage**: Single point to save all changes (abstraction over DbContext)

---

### 3. **Repositories/AccountRepository.cs**
**Purpose**: Data access for Account entity

```csharp
public class AccountRepository : IAccountRepository
{
    private readonly BankingDbContext _context;
    
    public async Task<Account?> GetByIdAsync(AccountId accountId)
    {
        // Includes: load related data automatically
        return await _context.Accounts
            .Include(a => a.Customer)      // Load customer
            .Include(a => a.Transactions)  // Load transactions
            .FirstOrDefaultAsync(a => a.AccountId == accountId);
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
    
    public async Task UpdateAccountBalanceAsync(AccountId accountId, Money newBalance)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId);
        
        account.UpdateBalance(newBalance);
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Account was modified by another user");
        }
    }
    
    public async Task<bool> AccountNumberExistsAsync(AccountNumber accountNumber)
    {
        return await _context.Accounts
            .AnyAsync(a => a.AccountNumber == accountNumber);
    }
}
```

**Key Methods**:
- **GetByIdAsync**: Fetch account with related data
- **GetByAccountNumberAsync**: Fetch by human-readable number
- **GetByCustomerIdAsync**: Get all accounts for a customer
- **UpdateAccountBalanceAsync**: Update balance with concurrency checking
- **AccountNumberExistsAsync**: Check if account number exists

---

### 4. **Repositories/CustomerRepository.cs**
**Purpose**: Data access for Customer entity

---

### 5. **Repositories/TransactionRepository.cs**
**Purpose**: Data access for Transaction entity

```csharp
public class TransactionRepository : ITransactionRepository
{
    public async Task<IEnumerable<Transaction>> GetByAccountIdAndDateRangeAsync(
        AccountId accountId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        return await _context.Transactions
            .Where(t => t.AccountId == accountId &&
                       t.Timestamp >= startDate &&
                       t.Timestamp <= endDate)
            .OrderBy(t => t.Timestamp)
            .ToListAsync(cancellationToken);
    }
}
```

---

### 6. **Services/DomainEventDispatcher.cs**
**Purpose**: Dispatches domain events to MediatR handlers

```csharp
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly BankingDbContext _context;
    private readonly IPublisher _publisher;  // MediatR publisher
    
    public async Task DispatchDomainEventsAsync(CancellationToken cancellationToken = default)
    {
        // 1. Get all aggregates with domain events
        var domainEntities = _context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(x => x.Entity.DomainEvents.Any())
            .ToList();
        
        // 2. Extract all events
        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();
        
        // 3. Publish each event to MediatR handlers
        foreach (var domainEvent in domainEvents)
        {
            _logger.LogInformation("Dispatching domain event: {EventType}", domainEvent.GetType().Name);
            await _publisher.Publish(domainEvent, cancellationToken);
        }
        
        // 4. Clear events after publishing
        domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());
    }
}
```

**Flow**:
1. Called by DomainEventsBehavior after handler completes
2. Finds all aggregates with events
3. Publishes each event via MediatR
4. MediatR routes to corresponding event handlers
5. Clears events to prevent re-publishing

---

### 7. **Services/OutboxBackgroundService.cs**
**Purpose**: Background worker that processes outbox messages every 30 seconds

```csharp
public class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create scope for dependency injection
                using var scope = _serviceProvider.CreateScope();
                
                // Get processor from DI
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();
                
                // Process unprocessed messages
                await processor.ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }
            
            // Wait 30 seconds before next run
            await Task.Delay(_interval, stoppingToken);
        }
    }
}
```

**Purpose**: Ensures events are eventually published even if immediate dispatch fails

---

### 8. **Services/OutboxMessageProcessor.cs**
**Purpose**: Deserializes and publishes outbox messages

```csharp
public class OutboxMessageProcessor : IOutboxMessageProcessor
{
    private readonly BankingDbContext _context;
    
    public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Get unprocessed messages (up to 20 at a time)
        var messages = await _context.OutboxMessages
            .Where(x => x.ProcessedOn == null && x.RetryCount < 3)
            .OrderBy(x => x.OccurredOn)
            .Take(20)
            .ToListAsync(cancellationToken);
        
        // 2. Process each message
        foreach (var message in messages)
        {
            try
            {
                // 3. Deserialize JSON back to DomainEvent
                var domainEvent = DeserializeMessage(message);
                
                // 4. Publish event (currently commented - would publish to event bus)
                // await _eventBus.PublishAsync(domainEvent, cancellationToken);
                
                // 5. Mark as processed
                message.ProcessedOn = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                
                // 6. Increment retry count
                message.RetryCount++;
                message.Error = ex.Message;
            }
        }
        
        // 7. Save changes
        await _context.SaveChangesAsync(cancellationToken);
    }
    
    private static IDomainEvent? DeserializeMessage(OutBoxMessage message)
    {
        // Reconstruct event type from message
        var eventType = Type.GetType($"CoreBanking.Core.Accounts.Events.{message.Type}, CoreBanking.Core");
        
        if (eventType == null)
            return null;
        
        // Deserialize JSON to event instance
        return JsonSerializer.Deserialize(message.Content, eventType) as IDomainEvent;
    }
}
```

**Error Handling**:
- Retries failed messages up to 3 times
- Logs errors for debugging
- Marks message with error details

---

### 9. **Persistence/Outbox/OutBoxMessage.cs**
**Purpose**: Entity representing a message in the outbox table

```csharp
public class OutBoxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; }                   // Event class name
    public string Content { get; set; }                // Serialized JSON
    public DateTime OccurredOn { get; set; }           // When event occurred
    public DateTime? ProcessedOn { get; set; }         // When event was published
    public int RetryCount { get; set; }                // Failed attempts
    public string? Error { get; set; }                 // Last error message
}
```

**Database columns**:
- `Id`: Primary key
- `Type`: "MoneyTransferredEvent", etc.
- `Content`: Full JSON of event
- `OccurredOn`: Timestamp from event
- `ProcessedOn`: Null until processed
- `RetryCount`: Incremented on each failure
- `Error`: Exception message

---

### 10. **Persistence/Configurations/OutBoxMessageConfiguration.cs**
**Purpose**: EF Core configuration for OutBoxMessage

```csharp
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutBoxMessage>
{
    public void Configure(EntityTypeBuilder<OutBoxMessage> builder)
    {
        builder.HasKey(om => om.Id);
        
        builder.Property(om => om.Type)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(om => om.Content)
            .IsRequired();
        
        builder.Property(om => om.ProcessedOn)
            .IsRequired(false);
    }
}
```

---

### 11. **Migrations/[DateName]_[Name].cs**
**Purpose**: Database schema version control

Example migration (auto-generated by EF):
```csharp
public partial class initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create Customers table
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                // ... more columns
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.CustomerId);
                table.UniqueConstraint("UX_Customers_Email", x => x.Email);
            });
        
        // Create Accounts table
        migrationBuilder.CreateTable(
            name: "Accounts",
            // ...
        );
        
        // Create Transactions table
        migrationBuilder.CreateTable(
            name: "Transactions",
            // ...
        );
        
        // Create OutboxMessages table
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            // ...
        );
    }
    
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Revert changes
    }
}
```

**What migrations do**:
- Track database schema changes
- Create tables with constraints
- Add indexes
- Seed initial data
- Allow rollback of changes

---

### 12. **Migrations/BankingDbContextModelSnapshot.cs**
**Purpose**: Snapshot of current database schema

Auto-generated, used by EF Core to track schema state

---

## HOW EVERYTHING WORKS TOGETHER

### Scenario: User Transfers Money

```
1. API Endpoint receives TransferMoneyCommand
   └─ POST /api/accounts/transfer
   └─ { sourceAccount, destAccount, amount, reference }

2. ValidationBehavior (Pipeline Step 1)
   └─ Validates: account numbers exist, amount > 0, etc.
   └─ If invalid → Return error immediately
   └─ If valid → Continue to handler

3. TransferMoneyCommandHandler (Business Logic)
   └─ Fetch sourceAccount from database
   └─ Fetch destAccount from database
   └─ Call sourceAccount.Transfer(destAccount, amount)
      └─ In Transfer() method:
         ├─ Check source active, dest active
         ├─ Check sufficient funds
         ├─ Debit source account (Balance -= amount)
         ├─ Credit dest account (Balance += amount)
         ├─ Create MoneyTransferredEvent
         └─ Add event to _domainEvents list
   └─ Call unitOfWork.SaveChangesAsync()
      └─ In BankingDbContext.SaveChangesAsync():
         ├─ Extract MoneyTransferredEvent from Account
         ├─ Create OutBoxMessage row
         ├─ Save Account changes + OutBoxMessage in single transaction
         └─ Events cleared from aggregate

4. DomainEventsBehavior (Pipeline Step 2)
   └─ Handler completed successfully
   └─ Call DomainEventDispatcher.DispatchDomainEventsAsync()
      └─ Get all aggregates with events (empty now, already saved to outbox)
      └─ Publish to MediatR handlers
         └─ MoneyTransferedEventHandler.Handle()
            ├─ Send email to both customers
            ├─ Log transaction
            ├─ Update analytics
            └─ etc.

5. Response returned to client
   └─ { "success": true, "message": "Transfer successful" }

6. OutboxBackgroundService (Every 30 seconds)
   └─ Query: SELECT * FROM OutboxMessages WHERE ProcessedOn IS NULL
   └─ For each message:
      └─ Deserialize JSON to MoneyTransferredEvent
      └─ Publish to event bus (if configured)
      └─ Mark ProcessedOn = now
      └─ Save changes

7. Event Handlers Execute
   └─ AccountCreatedEventHandler
   └─ MoneyTransferedEventHandler
   └─ InsufficientFundsEventHandler
```

---

## ARCHITECTURE PATTERNS USED

| Pattern | File | Purpose |
|---------|------|---------|
| **Aggregate Root** | Account.cs, AggregateRoot.cs | Encapsulate business logic, emit events |
| **Value Objects** | Money.cs, AccountNumber.cs | Type-safe immutable domain values |
| **Domain Events** | AccountCreatedEvent.cs | Communicate state changes |
| **Repository** | AccountRepository.cs | Abstract data access |
| **Unit of Work** | UnitOfWork.cs | Manage transactions |
| **CQRS** | Commands/, Queries/ | Separate read/write operations |
| **Pipeline Behaviors** | ValidationBehavior.cs | Cross-cutting concerns |
| **Outbox Pattern** | OutBoxMessage.cs | Guaranteed event delivery |
| **Soft Delete** | ISoftDelete.cs | Logical deletion with query filter |
| **Concurrency Control** | RowVersion | Optimistic locking |


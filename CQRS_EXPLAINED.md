# CQRS Pattern - Simple Explanation

## What is CQRS?

**CQRS** = **Command Query Responsibility Segregation**

It means: **Separate the code that changes data (Commands) from the code that reads data (Queries)**

---

## Real-World Analogy: Restaurant

### ❌ **WITHOUT CQRS (Mixed)**

```
Customer: "I want to order pizza"
Waiter: 
  - Write order to notebook (Change data)
  - Get menu list (Read data)
  - Calculate price (Read data)
  - All done by same person

Problem: Waiter is doing everything
- Sometimes writes wrong info
- Sometimes forgets to read menu
- Confusing responsibility
```

### ✅ **WITH CQRS (Separated)**

```
Customer: "I want to order pizza"
  ↓
Order Taker (COMMAND):
  - Just takes the order
  - Writes it down
  - That's it!

Menu Reader (QUERY):
  - Just reads menus
  - Calculates prices
  - Shows available items
  - That's it!

Clear separation of responsibilities!
```

---

## CQRS in Your Banking System

### **COMMANDS: Change Data**

**What:** Operations that MODIFY the database

**Examples:**
- Transfer money ← Creates new transactions, updates balances
- Create account ← Creates new account record
- Create customer ← Creates new customer record
- Withdraw money ← Modifies balance

**Characteristics:**
- Return `Result` (success/failure)
- Have side effects (data changes)
- One command = one action
- Can fail with specific errors

### **QUERIES: Read Data**

**What:** Operations that RETRIEVE data without changing it

**Examples:**
- Get account details ← Reads account info
- Get transaction history ← Reads transactions
- Get customer list ← Reads all customers
- Get account balance ← Reads balance

**Characteristics:**
- Return data (DTO - Data Transfer Object)
- No side effects (nothing changes)
- Can be cached
- Always succeed (or not found)

---

## File Structure in Your Project

### **Commands** (Things that CHANGE data)

```
CoreBankingTest.APP/Accounts/Commands/
├── CreateAccount/
│   ├── CreateAccountCommand.cs          ← The command definition
│   └── CreateAccountCommandValidator.cs ← Validation rules
└── TransferMoney/
    ├── TransferMoneyCommand.cs
    └── TransferMoneyCommandValidator.cs

CoreBankingTest.APP/Customers/Commands/
└── CreateCustomer/
    ├── CreateCustomerCommand.cs
    └── CreateCustomerCommandValidator.cs
```

### **Queries** (Things that READ data)

```
CoreBankingTest.APP/Accounts/Queries/
├── GetAccountDetails/
│   └── GetAccountDetailsQuery.cs
├── GetAccountSummary/
│   └── AccountSummaryDto.cs
└── GetTransactionHistory/
    ├── GetTransactionHistoryQuery.cs
    └── TransactionDto.cs

CoreBankingTest.APP/Customers/Queries/
├── GetCustomers/
│   ├── GetCustomersQuery.cs
│   └── CustomerDto.cs
└── GetCustomersDetails/
    ├── GetCustomersDetailsQuery.cs
    └── CustomerDetailsDto.cs
```

---

## How Commands Work

### **Step 1: Define the Command**

```csharp
// File: CreateAccountCommand.cs

public record CreateAccountCommand : ICommand<Guid>
{
    public CustomerId CustomerId { get; init; }
    public string AccountType { get; init; }
    public decimal InitialDeposit { get; init; }
    public string Currency { get; init; } = "NGN";
}
```

**What it is:**
- A request to create an account
- Contains all data needed to create account
- `ICommand<Guid>` = "This command returns a Guid"

### **Step 2: Validate the Command**

```csharp
// File: CreateAccountCommandValidator.cs

public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID required");
        
        RuleFor(x => x.AccountType)
            .Must(x => x == "Checking" || x == "Savings")
            .WithMessage("Invalid account type");
        
        RuleFor(x => x.InitialDeposit)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1000000)
            .WithMessage("Invalid deposit amount");
    }
}
```

### **Step 3: Handle the Command**

```csharp
// File: CreateAccountCommand.cs (Handler inside)

public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<Guid>>
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<Result<Guid>> Handle(
        CreateAccountCommand request, 
        CancellationToken cancellationToken)
    {
        // Step 1: Validate customer exists
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
        if (customer == null)
            return Result<Guid>.Failure("Customer not found");
        
        // Step 2: Generate unique account number
        var accountNumber = await GenerateUniqueAccountNumberAsync();
        
        // Step 3: Create account (triggers AccountCreatedEvent)
        var account = Account.Create(
            customerId: request.CustomerId,
            accountNumber: accountNumber,
            accountType: Enum.Parse<AccountType>(request.AccountType),
            initialDeposit: new Money(request.InitialDeposit, request.Currency)
        );
        
        // Step 4: Persist
        await _accountRepository.AddAsync(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        // Step 5: Return the account ID
        return Result<Guid>.Success(account.AccountId.Value);
    }
}
```

### **Step 4: Send the Command (From API)**

```csharp
// In some controller or API endpoint

[HttpPost("accounts/create")]
public async Task<ActionResult> CreateAccount([FromBody] CreateAccountCommand command)
{
    // MediatR sends command to handler
    var result = await _mediator.Send(command);
    
    if (!result.IsSuccess)
        return BadRequest(result.Errors);
    
    return Ok(new { accountId = result.Data });
}
```

---

## How Queries Work

### **Step 1: Define the Query**

```csharp
// File: GetAccountDetailsQuery.cs

public record GetAccountDetailsQuery : IRequest<Result<AccountDetailsDto>>
{
    public AccountId AccountId { get; init; }
}
```

**What it is:**
- A request to get account details
- Contains the account ID we want
- Returns `AccountDetailsDto`

### **Step 2: Create DTO (Data Transfer Object)**

```csharp
// File: AccountDetailsDto.cs

public class AccountDetailsDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; }
    public string AccountType { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateOpened { get; set; }
}
```

**Why DTO?**
- Don't expose internal entity structure
- Client only gets what they need
- Can change entity without breaking API

### **Step 3: Handle the Query**

```csharp
// File: GetAccountDetailsQuery.cs (Handler inside)

public class GetAccountDetailsQueryHandler : 
    IRequestHandler<GetAccountDetailsQuery, Result<AccountDetailsDto>>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IMapper _mapper;
    
    public async Task<Result<AccountDetailsDto>> Handle(
        GetAccountDetailsQuery request, 
        CancellationToken cancellationToken)
    {
        // Fetch account from database
        var account = await _accountRepository.GetByIdAsync(request.AccountId);
        
        if (account == null)
            return Result<AccountDetailsDto>.Failure("Account not found");
        
        // Map entity to DTO
        var dto = _mapper.Map<AccountDetailsDto>(account);
        
        // Return data
        return Result<AccountDetailsDto>.Success(dto);
    }
}
```

### **Step 4: Send the Query (From API)**

```csharp
// In some controller or API endpoint

[HttpGet("accounts/{id}")]
public async Task<ActionResult> GetAccountDetails(Guid id)
{
    var query = new GetAccountDetailsQuery 
    { 
        AccountId = AccountId.Create(id) 
    };
    
    // MediatR sends query to handler
    var result = await _mediator.Send(query);
    
    if (!result.IsSuccess)
        return NotFound();
    
    return Ok(result.Data);
}
```

---

## Complete Example: Money Transfer

### **The Command (Changes data)**

```csharp
public record TransferMoneyCommand : ICommand
{
    public AccountNumber SourceAccountNumber { get; init; }
    public AccountNumber DestinationAccountNumber { get; init; }
    public Money Amount { get; init; }
    public string Reference { get; init; }
}

public class TransferMoneyCommandHandler : IRequestHandler<TransferMoneyCommand, Result>
{
    public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
    {
        var sourceAccount = await _accountRepository.GetByAccountNumberAsync(
            request.SourceAccountNumber);
        var destAccount = await _accountRepository.GetByAccountNumberAsync(
            request.DestinationAccountNumber);
        
        // CHANGES DATA
        sourceAccount.Transfer(request.Amount, destAccount, ...);
        
        // PERSISTS CHANGES
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
}
```

**API Call:**
```
POST /api/accounts/transfer
{
    "sourceAccountNumber": "1234567890",
    "destinationAccountNumber": "0987654321",
    "amount": 500,
    "reference": "REF123"
}

Response: 
{
    "success": true,
    "message": "Transfer successful"
}
```

---

### **The Query (Reads data)**

```csharp
public record GetTransactionHistoryQuery : IRequest<Result<List<TransactionDto>>>
{
    public AccountId AccountId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}

public class GetTransactionHistoryQueryHandler : 
    IRequestHandler<GetTransactionHistoryQuery, Result<List<TransactionDto>>>
{
    public async Task<Result<List<TransactionDto>>> Handle(
        GetTransactionHistoryQuery request, 
        CancellationToken cancellationToken)
    {
        // READS DATA (doesn't change anything)
        var transactions = await _transactionRepository.GetByAccountIdAndDateRangeAsync(
            request.AccountId, 
            request.StartDate, 
            request.EndDate,
            cancellationToken);
        
        // MAP to DTOs
        var dtos = _mapper.Map<List<TransactionDto>>(transactions);
        
        return Result<List<TransactionDto>>.Success(dtos);
    }
}
```

**API Call:**
```
GET /api/transactions?accountId=123&startDate=2025-11-01&endDate=2025-11-06

Response:
[
    {
        "transactionId": "abc123",
        "type": "Transfer",
        "amount": 500,
        "currency": "NGN",
        "timestamp": "2025-11-06T04:00:00",
        "description": "Transfer to Alice"
    }
]
```

---

## Command vs Query: Side-by-Side

| Aspect | Command | Query |
|--------|---------|-------|
| **Purpose** | Change data | Read data |
| **Example** | TransferMoneyCommand | GetAccountDetailsQuery |
| **What It Does** | Modifies database | Retrieves from database |
| **Return Type** | Result (success/failure) | Result<T> with data |
| **Can Fail** | Yes (validation errors) | Usually no (or not found) |
| **Can Be Cached** | No | Yes |
| **Side Effects** | Yes (changes data) | No |
| **Event Emitted** | Yes | No |
| **Retry Logic** | Sometimes needed | Yes, safe to retry |

---

## MediatR: The Dispatcher

CQRS relies on **MediatR** to route commands/queries to handlers.

```csharp
// Send a command
var result = await _mediator.Send(
    new TransferMoneyCommand 
    { 
        SourceAccountNumber = "1234567890",
        DestinationAccountNumber = "0987654321",
        Amount = new Money(500)
    }
);

// Send a query
var result = await _mediator.Send(
    new GetAccountDetailsQuery 
    { 
        AccountId = AccountId.Create(Guid.Parse("abc123"))
    }
);
```

**MediatR does:**
1. Receives the command/query
2. Finds the matching handler
3. Runs validation (for commands)
4. Executes the handler
5. Runs domain events dispatch (for commands)
6. Returns the result

---

## Pipeline: How It All Works Together

```
User Request (API)
    ↓
MediatR receives command/query
    ↓
ValidationBehavior (for commands only)
    ├─ Validate input
    ├─ If invalid: Return error immediately
    └─ If valid: Continue
    ↓
Handler executes
    ├─ For Command: Load → Modify → Save
    └─ For Query: Load → Map → Return
    ↓
DomainEventsBehavior (for commands only)
    └─ Dispatch any domain events
    ↓
Response sent to user
```

---

## Your Project Structure (Organized by CQRS)

```
CoreBankingTest.APP/
├── Accounts/
│   ├── Commands/          ← Changes account data
│   │   ├── CreateAccount/
│   │   └── TransferMoney/
│   ├── Queries/           ← Reads account data
│   │   ├── GetAccountDetails/
│   │   ├── GetAccountSummary/
│   │   └── GetTransactionHistory/
│   └── EventHandlers/     ← Reacts to domain events
│       ├── AccountCreatedEventHandler
│       └── MoneyTransferedEventHandler
│
├── Customers/
│   ├── Commands/          ← Changes customer data
│   │   └── CreateCustomer/
│   └── Queries/           ← Reads customer data
│       ├── GetCustomers/
│       └── GetCustomersDetails/
│
└── Common/
    ├── Behaviors/         ← MediatR pipeline behaviors
    │   ├── ValidationBehavior
    │   ├── DomainEventsBehavior
    │   └── LoggingBehavior
    └── Mappings/          ← Entity ↔ DTO mappings
```

---

## Benefits of CQRS

| Benefit | Why |
|---------|-----|
| **Clear Responsibility** | Commands change, Queries read - separate concerns |
| **Easy to Understand** | Look at structure, immediately understand what it does |
| **Optimization** | Query handlers optimized for reading, command handlers for writing |
| **Scalability** | Can scale read path differently from write path |
| **Testing** | Easy to test - mock either path independently |
| **Event Sourcing** | Natural fit with domain events (commands emit events) |
| **Caching** | Can aggressively cache queries |

---

## Real-World Usage Pattern

### **In Your API Controller**

```csharp
[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    
    // COMMAND: POST (Create/Modify)
    [HttpPost]
    public async Task<ActionResult> CreateAccount([FromBody] CreateAccountCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(result.Errors);
        return Ok(result.Data);
    }
    
    // COMMAND: PUT/POST (Modify)
    [HttpPost("transfer")]
    public async Task<ActionResult> Transfer([FromBody] TransferMoneyCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(result.Errors);
        return Ok();
    }
    
    // QUERY: GET (Read)
    [HttpGet("{id}")]
    public async Task<ActionResult> GetAccountDetails(Guid id)
    {
        var result = await _mediator.Send(
            new GetAccountDetailsQuery { AccountId = AccountId.Create(id) }
        );
        if (!result.IsSuccess)
            return NotFound();
        return Ok(result.Data);
    }
    
    // QUERY: GET (Read)
    [HttpGet("{id}/transactions")]
    public async Task<ActionResult> GetTransactionHistory(
        Guid id, 
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate)
    {
        var result = await _mediator.Send(
            new GetTransactionHistoryQuery 
            { 
                AccountId = AccountId.Create(id),
                StartDate = startDate,
                EndDate = endDate
            }
        );
        if (!result.IsSuccess)
            return NotFound();
        return Ok(result.Data);
    }
}
```

---

## Key Differences Illustrated

### **Command Flow (Changes Data)**

```
1. Receive: TransferMoneyCommand
2. Validate: Check account numbers, amount, etc.
3. Execute: Load accounts → Transfer → Create event
4. Persist: Save to database
5. Dispatch: Send event to handlers
6. Return: Success or error message

Result: Database changed, event sent, handlers executed
```

### **Query Flow (Reads Data)**

```
1. Receive: GetAccountDetailsQuery
2. No validation needed (or basic)
3. Execute: Load account from database
4. Map: Convert entity to DTO
5. Return: Data to user

Result: Database unchanged, no events, data returned
```

---

## Summary

**CQRS** = Separate Commands (write/change) from Queries (read)

| | Command | Query |
|---|---------|-------|
| **Verb** | POST, PUT, DELETE | GET |
| **Does** | Changes database | Reads database |
| **Example** | TransferMoney | GetAccountDetails |
| **Returns** | Result (success/fail) | Result<Data> |
| **Validated** | Yes, strict | No, flexible |
| **Events** | Yes | No |
| **Cached** | No | Yes |

Your project uses CQRS with MediatR, organized by feature (Accounts, Customers) with clear separation of Commands and Queries!


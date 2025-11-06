# MediatR with CQRS - How They Work Together

## What is MediatR?

**MediatR** is a **service bus** or **message dispatcher** that:
- Receives requests (Commands and Queries)
- Routes them to the correct handler
- Executes the handler
- Returns the result

Think of it as a **postman** who:
1. Receives mail (request)
2. Reads the address (handler type)
3. Delivers to the right house (handler)
4. Brings back response

---

## How MediatR Connects CQRS

```
┌──────────────────────────────────────┐
│         API Endpoint                 │
│  POST /api/accounts/transfer         │
└────────────────┬─────────────────────┘
                 │
                 │ Creates TransferMoneyCommand
                 ↓
        ┌─────────────────────┐
        │    MediatR.Send()   │
        │  (The Dispatcher)   │
        └────────┬────────────┘
                 │
    ┌────────────┴───────────┐
    │                        │
    ▼                        ▼
┌──────────────┐      ┌────────────────┐
│ Is it a      │      │ Is it a        │
│ Command? ✓   │      │ Query? ❌      │
└──────┬───────┘      └────────────────┘
       │
       ▼
┌────────────────────────────┐
│ Route to Handler:          │
│ TransferMoneyCommandHandler│
└─────────┬──────────────────┘
          │
          ▼
┌────────────────────────────┐
│ Execute Handler:           │
│ 1. Validate                │
│ 2. Load accounts           │
│ 3. Transfer money          │
│ 4. Save to database        │
│ 5. Emit events             │
└─────────┬──────────────────┘
          │
          ▼
    ┌──────────────┐
    │ Return Result│
    └──────────────┘
```

---

## The 4 Key MediatR Components

### **1. Request (Command/Query)**

```csharp
// This is a REQUEST
// Implements IRequest or IRequest<T>

public record TransferMoneyCommand : ICommand
//                                    ↑
//                          This says "I'm a command"
//                          MediatR will route me to a handler
{
    public AccountNumber SourceAccountNumber { get; init; }
    public AccountNumber DestinationAccountNumber { get; init; }
    public Money Amount { get; init; }
}

// ICommand is defined as:
public interface ICommand : IRequest<Result> { }

// So TransferMoneyCommand : ICommand
// = TransferMoneyCommand : IRequest<Result>
```

### **2. Handler**

```csharp
// This is a HANDLER
// Implements IRequestHandler<TRequest, TResponse>

public class TransferMoneyCommandHandler : 
    IRequestHandler<TransferMoneyCommand, Result>
//  ↑                  ↑                        ↑
//  "I handle this"    "Command type"    "Response type"
{
    public async Task<Result> Handle(
        TransferMoneyCommand request,
        CancellationToken cancellationToken)
    {
        // YOUR CODE HERE
        // Do the work
        return Result.Success();
    }
}

// MediatR will:
// 1. See TransferMoneyCommand
// 2. Find TransferMoneyCommandHandler
// 3. Call Handle() method
// 4. Return Result
```

### **3. Pipeline Behavior (Optional but powerful)**

```csharp
// Middleware that runs BEFORE and AFTER handler

public class ValidationBehavior<TRequest, TResponse> : 
    IPipelineBehavior<TRequest, TResponse>
//  ↑
//  "I intercept all requests passing through MediatR"
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // BEFORE handler
        // Validate request
        var validationErrors = Validate(request);
        if (validationErrors.Any())
            return ValidationFailed(validationErrors);  // Short-circuit!
        
        // CALL the actual handler
        var response = await next();
        
        // AFTER handler
        // Could do logging, etc.
        
        return response;
    }
}
```

### **4. Mediator (The Bus)**

```csharp
// This is the actual service

public interface IMediator
{
    // Send a command or query
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}

// You inject it:
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    
    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;  // Injected by DI
    }
}
```

---

## Complete Flow: Step by Step

### **Example: Transfer Money**

### **Step 1: Controller Sends Command**

```csharp
[HttpPost("transfer")]
public async Task<ActionResult> Transfer([FromBody] TransferMoneyCommand command)
{
    // Create command
    var cmd = new TransferMoneyCommand
    {
        SourceAccountNumber = AccountNumber.Create("1234567890"),
        DestinationAccountNumber = AccountNumber.Create("0987654321"),
        Amount = new Money(500, "NGN")
    };
    
    // Send to MediatR
    var result = await _mediator.Send(cmd);
    
    if (!result.IsSuccess)
        return BadRequest(result.Errors);
    
    return Ok();
}
```

---

### **Step 2: MediatR Receives Command**

```
MediatR sees: TransferMoneyCommand

Internal logic:
- Check: What type is this?
  Answer: It's an IRequest<Result>
- Check: Who handles IRequest<TransferMoneyCommand, Result>?
  Answer: TransferMoneyCommandHandler
- Check: Are there any pipeline behaviors?
  Answer: Yes - ValidationBehavior, LoggingBehavior, DomainEventsBehavior
```

---

### **Step 3: Pipeline Behaviors Execute (Before Handler)**

```csharp
// BEHAVIOR #1: ValidationBehavior
public async Task<TResponse> Handle(
    TRequest request,  // TransferMoneyCommand
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    // Validate the command
    var validator = GetValidator(typeof(TransferMoneyCommand));
    var result = await validator.ValidateAsync(request);
    
    if (result.IsValid)
    {
        // Valid - continue to next behavior
        return await next();
    }
    else
    {
        // Invalid - return errors without calling handler
        return ErrorResult(result.Errors);
    }
}
```

**Output:** ✅ Valid → Continue

---

### **Step 4: Handler Executes**

```csharp
// TransferMoneyCommandHandler

public async Task<Result> Handle(
    TransferMoneyCommand request,
    CancellationToken cancellationToken)
{
    try
    {
        // STEP 1: Get accounts
        var sourceAccount = await _accountRepository.GetByAccountNumberAsync(
            request.SourceAccountNumber);
        var destAccount = await _accountRepository.GetByAccountNumberAsync(
            request.DestinationAccountNumber);
        
        if (sourceAccount == null || destAccount == null)
            return Result.Failure("Account not found");
        
        // STEP 2: Call domain logic (creates events)
        var result = sourceAccount.Transfer(
            amount: request.Amount,
            destination: destAccount,
            reference: request.Reference,
            description: request.Description
        );
        
        if (!result.IsSuccess)
            return result;
        
        // STEP 3: Save to database
        // This saves BOTH account changes AND events to outbox
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
    catch (Exception ex)
    {
        return Result.Failure(ex.Message);
    }
}
```

**Output:** ✅ Result.Success()

---

### **Step 5: Pipeline Behaviors Execute (After Handler)**

```csharp
// BEHAVIOR #2: DomainEventsBehavior
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    // Execute handler (from step above)
    var response = await next();
    
    // Handler completed - now dispatch domain events
    await _dispatcher.DispatchDomainEventsAsync(cancellationToken);
    
    // Events are now published to handlers:
    // - MoneyTransferedEventHandler
    // - AccountCreatedEventHandler
    // - InsufficientFundsEventHandler
    
    return response;
}

// BEHAVIOR #3: LoggingBehavior
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    _logger.LogInformation("Executing: {CommandType}", typeof(TRequest).Name);
    
    var response = await next();
    
    _logger.LogInformation("Completed: {CommandType}", typeof(TRequest).Name);
    
    return response;
}
```

**Output:** Events dispatched, logs written

---

### **Step 6: Response Returned**

```csharp
// Back in controller

var result = await _mediator.Send(cmd);
//                    ↑
//     Returns after all behaviors complete

if (!result.IsSuccess)
    return BadRequest(result.Errors);

return Ok();
```

---

## How Queries Work With MediatR

### **Same Process, But Simpler**

```csharp
// QUERY
public record GetAccountDetailsQuery : IRequest<Result<AccountDetailsDto>>
{
    public AccountId AccountId { get; init; }
}

// HANDLER
public class GetAccountDetailsQueryHandler : 
    IRequestHandler<GetAccountDetailsQuery, Result<AccountDetailsDto>>
{
    public async Task<Result<AccountDetailsDto>> Handle(
        GetAccountDetailsQuery request,
        CancellationToken cancellationToken)
    {
        // Just read and return
        var account = await _accountRepository.GetByIdAsync(request.AccountId);
        if (account == null)
            return Result<AccountDetailsDto>.Failure("Not found");
        
        var dto = _mapper.Map<AccountDetailsDto>(account);
        return Result<AccountDetailsDto>.Success(dto);
    }
}

// USAGE
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
```

**MediatR Flow for Queries:**
```
1. Receive GetAccountDetailsQuery
2. Find GetAccountDetailsQueryHandler
3. Call Handle()
4. Load account
5. Map to DTO
6. Return data
```

(No validation needed, no events dispatched)

---

## MediatR Registration (Dependency Injection)

### **In Program.cs or Startup.cs**

```csharp
// Register MediatR
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// This tells MediatR to:
// 1. Scan all types in current assembly
// 2. Find all IRequestHandler implementations
// 3. Find all IPipelineBehavior implementations
// 4. Register them in DI container

// Now you can inject:
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    
    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;  // ← Automatically injected
    }
}
```

---

## Complete Request/Response Cycle

```
┌─────────────────────────────────────────────────────────────┐
│                    USER REQUEST                              │
│  POST /api/accounts/transfer { data }                       │
└──────────────────────┬──────────────────────────────────────┘
                       │
          ┌────────────▼────────────┐
          │    Controller           │
          │  Creates Command        │
          │  Calls mediator.Send()  │
          └────────────┬────────────┘
                       │
          ┌────────────▼────────────────────┐
          │    MediatR Bus                  │
          │  Receives TransferMoneyCommand  │
          │  Finds Handler                  │
          │  Gets Behaviors                 │
          └────────────┬────────────────────┘
                       │
     ┌─────────────────┼─────────────────┐
     │                 │                 │
     ▼                 ▼                 ▼
 ┌─────────┐    ┌──────────────┐   ┌────────────┐
 │Validation│    │ TransferMoney│   │ DomainEvents
 │Behavior  │    │   Handler    │   │  Behavior
 │          │    │              │   │
 │Validate  │    │Load Accounts │   │Dispatch
 │Input ✓   │    │Transfer ✓    │   │Events ✓
 │          │    │Save ✓        │   │
 └────┬─────┘    └──────┬───────┘   └────┬──────┘
      │                 │                │
      └─────────┬───────┴────────┬───────┘
                │                │
         ✅ VALID            ✅ SUCCESS
                │                │
                └────────┬────────┘
                         │
                ┌────────▼────────┐
                │ Response        │
                │ Result.Success()│
                └────────┬────────┘
                         │
                         ▼
          ┌──────────────────────────┐
          │  Back to Controller      │
          │  Return Ok()             │
          └──────────────────────────┘
                         │
                         ▼
        ┌────────────────────────────┐
        │    HTTP Response           │
        │    200 OK                  │
        └────────────────────────────┘
```

---

## Side-by-Side: Command vs Query with MediatR

### **COMMAND (Transfer Money)**

```csharp
// Step 1: Create command
var cmd = new TransferMoneyCommand { ... };

// Step 2: Send via MediatR
var result = await _mediator.Send(cmd);

// Step 3: MediatR routing
MediatR → ValidationBehavior → TransferMoneyCommandHandler
                                    ↓
                          (Load accounts, transfer, save)
                                    ↓
                           → DomainEventsBehavior
                                    ↓
                          (Dispatch MoneyTransferredEvent)
                                    ↓
                              Return Result

// Result: Database changed, events emitted
```

### **QUERY (Get Account Details)**

```csharp
// Step 1: Create query
var qry = new GetAccountDetailsQuery { AccountId = id };

// Step 2: Send via MediatR
var result = await _mediator.Send(qry);

// Step 3: MediatR routing
MediatR → GetAccountDetailsQueryHandler
              ↓
        (Load account, map to DTO)
              ↓
          Return Result<DTO>

// Result: Database unchanged, data returned
```

---

## Key Behaviors in Your Project

### **1. ValidationBehavior**

```csharp
// Runs FIRST

public class ValidationBehavior<TRequest, TResponse> : 
    IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();
        
        // Validate all rules
        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(x => x.ValidateAsync(context, cancellationToken))
        );
        
        var failures = validationResults
            .SelectMany(x => x.Errors)
            .Where(f => f != null)
            .ToList();
        
        if (failures.Any())
        {
            // Return error immediately - handler never runs!
            return (TResponse)Result.Failure(failures.Select(f => f.ErrorMessage).ToArray());
        }
        
        // Valid - continue to handler
        return await next();
    }
}
```

### **2. DomainEventsBehavior**

```csharp
// Runs AFTER handler

public class DomainEventsBehavior<TRequest, TResponse> : 
    IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IDomainEventDispatcher _dispatcher;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Handler executes first
        var response = await next();
        
        // Then dispatch domain events
        await _dispatcher.DispatchDomainEventsAsync(cancellationToken);
        
        return response;
    }
}
```

### **3. LoggingBehavior**

```csharp
// Logs all requests

public class LoggingBehavior<TRequest, TResponse> : 
    IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling request: {RequestType}", typeof(TRequest).Name);
        
        var response = await next();
        
        _logger.LogInformation("Request completed: {RequestType}", typeof(TRequest).Name);
        
        return response;
    }
}
```

---

## The Pipeline Order (Onion Model)

```
                    ▼ Incoming Request
                    │
        ┌───────────┴──────────────┐
        │   ValidationBehavior     │
        │  (Validate input)        │
        │  OR return error         │
        └───────────┬──────────────┘
                    │
        ┌───────────▼──────────────┐
        │   LoggingBehavior        │
        │  (Log request start)     │
        └───────────┬──────────────┘
                    │
        ┌───────────▼──────────────┐
        │  DomainEventsBehavior    │
        │  (Setup)                 │
        └───────────┬──────────────┘
                    │
        ┌───────────▼──────────────┐
        │   HANDLER                │
        │  (Do the work)           │
        └───────────┬──────────────┘
                    │
        ┌───────────▼──────────────┐
        │  DomainEventsBehavior    │
        │  (Dispatch events)       │
        └───────────┬──────────────┘
                    │
        ┌───────────▼──────────────┐
        │   LoggingBehavior        │
        │  (Log request end)       │
        └───────────┬──────────────┘
                    │
                    ▼ Response
```

---

## Simple Example: User Creates Account

```csharp
// Step 1: API receives request
[HttpPost("accounts")]
public async Task<ActionResult> CreateAccount(
    [FromBody] CreateAccountCommand command)
{
    // Step 2: Send to MediatR
    var result = await _mediator.Send(command);
    
    // Returns after ALL behaviors complete
    if (!result.IsSuccess)
        return BadRequest(result.Errors);
    
    return Ok(result.Data);
}

// BEHIND THE SCENES:
// ─────────────────

// MediatR receives: CreateAccountCommand
// ↓
// ValidationBehavior checks:
//   - CustomerId not empty? ✓
//   - AccountType valid? ✓
//   - InitialDeposit valid? ✓
// ↓
// CreateAccountCommandHandler executes:
//   - Check customer exists ✓
//   - Generate account number ✓
//   - Create account ✓
//   - Save to DB ✓
//   - Emit AccountCreatedEvent ✓
// ↓
// DomainEventsBehavior:
//   - Dispatch AccountCreatedEvent ✓
//   - Handler receives event ✓
//   - Send welcome email ✓
// ↓
// Return: Result<Guid>
// ↓
// Back to controller: return Ok(result.Data)
```

---

## Summary

| Component | Purpose | Used For |
|-----------|---------|----------|
| **IRequest<T>** | Defines request | Command/Query |
| **IRequestHandler<T,R>** | Handles request | Processing logic |
| **IPipelineBehavior** | Intercepts | Validation, logging, events |
| **IMediator** | Dispatches | Routing to handler |

**MediatR with CQRS:**
- Commands → Change database → Emit events
- Queries → Read database → Return data
- Behaviors → Validate before, dispatch events after
- Everything coordinated by MediatR bus

Your project: **Clean, organized, testable!**


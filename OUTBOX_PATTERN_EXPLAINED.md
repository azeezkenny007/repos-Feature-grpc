# Outbox Pattern - Deep Dive with Examples

## The Problem Outbox Solves

Imagine you're processing a money transfer:

```
❌ WITHOUT OUTBOX PATTERN (2-Phase Problem):
┌─────────────────────────────────────────┐
│ 1. Save account changes to database     │ ✅ Success
└─────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────┐
│ 2. Publish event to event bus           │ ❌ CRASHES! Network error
└─────────────────────────────────────────┘

Result: Account saved but event never published
        → Customers don't get notifications
        → No audit trail
        → System is inconsistent
```

## The Outbox Pattern Solution

```
✅ WITH OUTBOX PATTERN (Atomic Operation):
┌─────────────────────────────────────────┐
│ 1. Save account changes                 │
│ 2. Save event as message in Outbox      │ ✅ Both in ONE transaction
│    (both in same database transaction)  │
└─────────────────────────────────────────┘
         ↓
    Data persisted safely
         ↓
┌─────────────────────────────────────────┐
│ Background Service polls Outbox         │ Runs periodically (every 30s)
│ (Every 30 seconds)                      │
└─────────────────────────────────────────┘
         ↓
┌─────────────────────────────────────────┐
│ Found unprocessed message?              │
│ - Deserialize JSON to Event             │
│ - Publish to event bus                  │
│ - If success: Mark as ProcessedOn       │
│ - If failure: Retry (max 3 times)       │
└─────────────────────────────────────────┘
```

---

## Components Explained

### 1. **OutboxBackgroundService** (The Timer)

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
                // Create scope for each iteration
                using var scope = _serviceProvider.CreateScope();
                
                // Get the processor
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();
                
                // Tell processor to handle unprocessed messages
                await processor.ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }
            
            // Wait 30 seconds before next check
            await Task.Delay(_interval, stoppingToken);
        }
    }
}
```

**What it does:**
- Runs as a background service (started when application starts)
- Every 30 seconds: checks if there are unprocessed messages
- If messages exist: tells OutboxMessageProcessor to handle them
- If error occurs: logs it and continues (doesn't crash)
- Stops when application stops

**Why every 30 seconds?**
- Trade-off between latency (how fast events are published) and CPU usage
- 30s = events published within half a minute (usually fast enough)
- Could be 5s (faster) or 5m (slower, saves CPU)

---

### 2. **OutboxMessageProcessor** (The Worker)

```csharp
public class OutboxMessageProcessor : IOutboxMessageProcessor
{
    private readonly BankingDbContext _context;
    private readonly ILogger<OutboxMessageProcessor> _logger;
    
    public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        // STEP 1: Get unprocessed messages from database
        var messages = await _context.OutboxMessages
            .Where(x => x.ProcessedOn == null && x.RetryCount < 3)  // Conditions
            .OrderBy(x => x.OccurredOn)                              // Oldest first
            .Take(20)                                                // Max 20 at a time
            .ToListAsync(cancellationToken);
        
        // STEP 2: Process each message
        foreach (var message in messages)
        {
            try
            {
                // STEP 3a: Deserialize JSON string back to DomainEvent object
                var domainEvent = DeserializeMessage(message);
                
                // STEP 3b: Publish to event bus (currently commented)
                // This is where you'd send to RabbitMQ, Azure Service Bus, etc.
                // await _eventBus.PublishAsync(domainEvent, cancellationToken);
                
                // STEP 3c: Mark as successfully processed
                message.ProcessedOn = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                // STEP 4: Handle errors - increment retry count
                _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                
                message.RetryCount++;
                message.Error = ex.Message;
            }
        }
        
        // STEP 5: Save all changes (both successful and failed)
        await _context.SaveChangesAsync(cancellationToken);
    }
    
    private static IDomainEvent? DeserializeMessage(OutBoxMessage message)
    {
        // Get the event type name from message
        // Example: message.Type = "MoneyTransferredEvent"
        var eventType = Type.GetType($"CoreBanking.Core.Accounts.Events.{message.Type}, CoreBanking.Core");
        
        if (eventType == null)
        {
            _logger.LogWarning("Could not find event type: {EventType}", message.Type);
            return null;
        }
        
        // Deserialize JSON string to event object
        // Example: message.Content = '{"TransactionId":"123","Amount":100}'
        //          → MoneyTransferredEvent instance
        return JsonSerializer.Deserialize(message.Content, eventType) as IDomainEvent;
    }
}
```

---

## Complete Example: Money Transfer Flow

### **SCENARIO: User transfers ₦500 from Account A to Account B**

---

### **STEP 1: API Receives Request**

```csharp
POST /api/accounts/transfer
{
    "sourceAccountNumber": "1234567890",
    "destinationAccountNumber": "0987654321",
    "amount": 500,
    "reference": "REF123"
}
```

---

### **STEP 2: Command Handler Executes**

```csharp
public class TransferMoneyCommandHandler : IRequestHandler<TransferMoneyCommand, Result>
{
    public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
    {
        // Get accounts from database
        var sourceAccount = await _accountRepository.GetByAccountNumberAsync("1234567890");
        var destAccount = await _accountRepository.GetByAccountNumberAsync("0987654321");
        
        // Call Transfer method (this is where domain logic happens)
        sourceAccount.Transfer(
            amount: new Money(500),
            destination: destAccount,
            reference: "REF123",
            description: "Transfer"
        );
        
        // Save changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
}
```

---

### **STEP 3: Account.Transfer() Does Business Logic**

```csharp
public class Account : AggregateRoot<AccountId>
{
    public Result Transfer(Money amount, Account destination, string reference, string description)
    {
        // Validation
        if (Balance.Amount < amount.Amount)
        {
            throw new InsufficientFundsException(amount.Amount, Balance.Amount);
        }
        
        // Execute transfer
        this.Balance -= amount;           // Debit source
        destination.Balance += amount;    // Credit destination
        
        // ✅ CREATE DOMAIN EVENT
        this.AddDomainEvent(new MoneyTransferredEvent(
            transactionId: TransactionId.Create(),
            sourceAccount: this.AccountNumber,
            destinationAccount: destination.AccountNumber,
            amount: amount,
            reference: reference
        ));
        
        return Result.Success();
    }
}
```

**At this point:**
- Balance updated in memory
- Event created in `Account._domainEvents` collection
- Database NOT updated yet

---

### **STEP 4: SaveChangesAsync() - The Magic**

```csharp
// In BankingDbContext
public async Task SaveChangesWithOutboxAsync(CancellationToken cancellationToken = default)
{
    // EXTRACT: Get all events from all aggregates
    var events = ChangeTracker
        .Entries<AggregateRoot<AccountId>>()
        .SelectMany(x => x.Entity.DomainEvents)
        .Select(domainEvent => new OutBoxMessage
        {
            Id = Guid.NewGuid(),                                    // New ID
            Type = domainEvent.GetType().Name,                      // "MoneyTransferredEvent"
            Content = JsonSerializer.Serialize(domainEvent),        // Full JSON
            OccurredOn = domainEvent.OccurredOn                     // When it happened
        })
        .ToList();
    
    // CLEAR: Remove events from aggregates
    ChangeTracker.Entries<AggregateRoot<AccountId>>()
        .ForEach(entry => entry.Entity.ClearDomainEvents());
    
    // SAVE: Single transaction - both account changes AND outbox message
    await base.SaveChangesAsync(cancellationToken);
    
    // ADD: Outbox messages to database
    if (events.Any())
    {
        await OutboxMessages.AddRangeAsync(events, cancellationToken);
        await base.SaveChangesAsync(cancellationToken);
    }
}
```

**Database State After This:**

| Table | Change |
|-------|--------|
| **Accounts** | ✅ Source balance: 10,000 - 500 = 9,500 |
| **Accounts** | ✅ Dest balance: 5,000 + 500 = 5,500 |
| **OutboxMessages** | ✅ New row inserted |

**OutboxMessages Table:**

```
| Id   | Type                    | Content (JSON)                              | OccurredOn          | ProcessedOn | RetryCount | Error |
|------|-------------------------|---------------------------------------------|---------------------|-------------|-----------|-------|
| UUID | MoneyTransferredEvent   | {"TransactionId":"...", "Amount":500, ...}  | 2025-11-06 04:00:00 | NULL        | 0         | NULL  |
```

---

### **STEP 5: Response Sent to User**

```json
{
    "success": true,
    "message": "Transfer completed successfully"
}
```

**User is happy! But event hasn't been published yet.**

---

### **STEP 6: Background Service Runs (First Time - 30 seconds later)**

```
⏰ Time: 04:00:30 (30 seconds after transfer)
```

**OutboxBackgroundService wakes up:**

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Every iteration:
    // 1. Create new scope for dependency injection
    using var scope = _serviceProvider.CreateScope();
    
    // 2. Get the processor
    var processor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();
    
    // 3. Tell it to process messages
    await processor.ProcessOutboxMessagesAsync(stoppingToken);
    
    // 4. Sleep for 30 seconds
    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
}
```

---

### **STEP 7: OutboxMessageProcessor Queries Database**

```csharp
public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
{
    // Query: Get all messages that haven't been processed AND haven't failed 3 times
    var messages = await _context.OutboxMessages
        .Where(x => x.ProcessedOn == null &&      // Not yet processed
                    x.RetryCount < 3)             // Less than 3 failures
        .OrderBy(x => x.OccurredOn)               // Oldest first
        .Take(20)                                 // Max 20 at a time
        .ToListAsync(cancellationToken);
    
    // Found: 1 message (our MoneyTransferredEvent)
    foreach (var message in messages)  // Loop through
    {
        try
        {
            // Message data:
            // - Id: "abc-123"
            // - Type: "MoneyTransferredEvent"
            // - Content: '{"TransactionId":"xyz","Amount":500,...}'
            // - ProcessedOn: NULL
            // - RetryCount: 0
            
            // DESERIALIZE: Convert JSON back to C# object
            var domainEvent = DeserializeMessage(message);
            // Result: MoneyTransferredEvent instance with all properties populated
            
            // PUBLISH: Send to event bus (if configured)
            // await _eventBus.PublishAsync(domainEvent, cancellationToken);
            
            // MARK SUCCESS
            message.ProcessedOn = DateTime.UtcNow;  // Set to current time
            message.Error = null;
            
            Console.WriteLine($"✅ Message processed successfully at {DateTime.UtcNow}");
        }
        catch (Exception ex)
        {
            // If any error occurs...
            Console.WriteLine($"❌ Error: {ex.Message}");
            
            message.RetryCount++;      // Increment retry
            message.Error = ex.Message; // Store error
        }
    }
    
    // SAVE: Update message status in database
    await _context.SaveChangesAsync(cancellationToken);
}
```

**OutboxMessages Table After Processing:**

```
| Id   | Type                    | Content (JSON)  | OccurredOn          | ProcessedOn         | RetryCount | Error |
|------|-------------------------|-----------------|---------------------|---------------------|-----------|-------|
| UUID | MoneyTransferredEvent   | {...}           | 2025-11-06 04:00:00 | 2025-11-06 04:00:30 | 0         | NULL  |
```

✅ **ProcessedOn now has a timestamp!**

---

## Retry Mechanism Example

### **Scenario: Event Bus is Down Initially**

**Iteration 1** (04:00:30):
```csharp
try
{
    // ❌ Event bus is unreachable - throws NetworkException
    await _eventBus.PublishAsync(domainEvent, cancellationToken);
}
catch (Exception ex)
{
    message.RetryCount++;       // Now = 1
    message.Error = "Connection refused: Event bus unavailable";
    // ProcessedOn stays NULL (not marked as processed)
}
```

**OutboxMessages after Iteration 1:**
```
| RetryCount | ProcessedOn | Error                              |
|------------|-------------|-----------------------------------|
| 1          | NULL        | Connection refused: Event bus...  |
```

---

**Iteration 2** (04:01:00 - 30 seconds later):
```csharp
// Query still finds this message because:
// - ProcessedOn == NULL ✓
// - RetryCount (1) < 3 ✓

var messages = await _context.OutboxMessages
    .Where(x => x.ProcessedOn == null && x.RetryCount < 3)
    // ✓ This message is included again!
```

```csharp
try
{
    // ✅ Event bus is back online!
    await _eventBus.PublishAsync(domainEvent, cancellationToken);
    
    message.ProcessedOn = DateTime.UtcNow;  // ✅ Mark as processed
    message.Error = null;
}
catch (Exception ex)
{
    message.RetryCount++;       // Now = 2
    message.Error = "New error...";
}
```

**OutboxMessages after Iteration 2:**
```
| RetryCount | ProcessedOn         | Error |
|------------|---------------------|-------|
| 1          | 2025-11-06 04:01:00 | NULL  |
```

✅ **Message successfully processed after retry!**

---

## What Happens After 3 Failed Retries?

```
Iteration 1: RetryCount = 1 (included in query)
Iteration 2: RetryCount = 2 (included in query)
Iteration 3: RetryCount = 3 (EXCLUDED from query ❌)
Iteration 4: RetryCount = 3 (EXCLUDED from query ❌)
...
```

**Dead Letter Queue:**
```
SELECT * FROM OutboxMessages 
WHERE ProcessedOn IS NULL AND RetryCount >= 3
```

These messages are stuck - they need manual intervention:
- Check the Error column to see what went wrong
- Fix the issue
- Reset RetryCount = 0
- Manual retry

---

## Complete Lifecycle Timeline

```
TIME         EVENT                           DATABASE STATE
──────────────────────────────────────────────────────────────
04:00:00     Transfer initiated
04:00:05     Account balances updated        ✅ Accounts table updated
04:00:05     Event created                   
04:00:05     SaveChangesAsync called         ✅ OutboxMessage inserted
             Response sent to client

04:00:30     BackgroundService wakes up      🔍 Query OutboxMessages
04:00:30     Finds unprocessed message       📋 RetryCount=0, ProcessedOn=NULL
04:00:31     Deserialize JSON → Event        🔄 MoneyTransferredEvent created
04:00:31     Publish to event bus            📤 Sent to RabbitMQ/Service Bus
04:00:31     Mark ProcessedOn                ✅ Set timestamp
04:00:31     Save changes                    ✅ Database updated

04:00:32     Event Handlers Execute          
             - AccountCreatedEventHandler
             - MoneyTransferedEventHandler
             - Notifications sent to customer ✉️ Emails, SMS, etc.
```

---

## Benefits of Outbox Pattern

| Benefit | Explanation |
|---------|-------------|
| **Atomicity** | Database AND event are saved together (no partial failures) |
| **Reliability** | Events don't get lost even if event bus crashes |
| **Eventual Consistency** | Events published when system recovers |
| **No Double Publishing** | ProcessedOn flag prevents re-publishing |
| **Retry Logic** | Automatic retries up to 3 times |
| **Audit Trail** | Full history of what events were published |
| **Decoupling** | Business logic doesn't depend on event bus availability |

---

## Visual Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    USER REQUEST                              │
│              Transfer ₦500 A → B                            │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
         ┌───────────────────────────────┐
         │  TransferMoneyCommandHandler  │
         │  - Get accounts               │
         │  - Call Transfer()            │
         │  - Call SaveAsync()           │
         └───────────┬───────────────────┘
                     │
        ┌────────────▼────────────┐
        │ Account.Transfer()      │
        │ - Check funds ✓         │
        │ - Debit ✓               │
        │ - Credit ✓              │
        │ - Emit Event ✓          │
        └────────────┬────────────┘
                     │
      ┌──────────────▼─────────────┐
      │   BankingDbContext         │
      │   SaveChangesAsync()       │
      │                            │
      │  ✅ Save Account changes   │
      │  ✅ Create OutboxMessage   │
      │  ✅ Single transaction     │
      └──────────────┬─────────────┘
                     │
         ┌───────────▼────────────┐
         │   Response to Client   │
         │   "Transfer Complete"  │
         └───────────┬────────────┘
                     │
                     │ (30 seconds pass)
                     │
         ┌───────────▼──────────────────┐
         │ OutboxBackgroundService      │
         │ - Wakes up every 30 seconds  │
         └───────────┬──────────────────┘
                     │
         ┌───────────▼──────────────────────┐
         │ OutboxMessageProcessor           │
         │ - Query unprocessed messages     │
         │ - Deserialize JSON → Event       │
         │ - Publish to event bus           │
         │ - Mark ProcessedOn               │
         │ - Handle retries (max 3)         │
         └───────────┬──────────────────────┘
                     │
         ┌───────────▼──────────────────────┐
         │ Event Handlers                   │
         │ - AccountCreatedEventHandler     │
         │ - MoneyTransferedEventHandler    │
         │ - Send notifications ✉️          │
         └──────────────────────────────────┘
```

---

## Key Takeaways

1. **Outbox = Insurance Policy**
   - Guarantees events are eventually published
   - No data loss if event bus crashes

2. **30-Second Polling**
   - Background service checks every 30 seconds
   - Events typically published within 30-60 seconds

3. **Automatic Retries**
   - Up to 3 automatic retry attempts
   - If still failing after 3 retries, manual intervention needed

4. **JSON Serialization**
   - Event stored as JSON string in database
   - Later deserialized back to C# object

5. **Two-Step Process**
   - Immediate: Saved to database (guaranteed)
   - Later: Published to event bus (guaranteed on retry)


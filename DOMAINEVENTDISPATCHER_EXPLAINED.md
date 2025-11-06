# DomainEventDispatcher - Simple Explanation

## What Does It Do?

**DomainEventDispatcher** is like a **postman** who delivers messages (events) to the right people (handlers).

---

## Real-World Analogy

### You Get Mail at Home

```
❌ PROBLEM: Mail arrives at your house
   - Letters scattered in your mailbox
   - You don't know who's responsible for opening them
   - No one reads the important notices

✅ SOLUTION: Postal worker (DomainEventDispatcher)
   - Takes mail from your mailbox
   - Reads each letter to see what it's about
   - "This is a bill → Send to accountant"
   - "This is a pizza coupon → Give to food lover"
   - "This is a job offer → Give to job seeker"
   - Everyone gets their relevant mail
```

---

## In Your Banking System

### What Happens When You Create an Account?

```
1. User creates account
   ↓
2. Account.Create() is called
   ↓
3. Event is created: "AccountCreatedEvent"
   ↓
4. Event is stored in: Account._domainEvents (in-memory list)
   ↓
5. SaveAsync() called
   ↓
6. ✅ Event saved to OutboxMessages table
   Event cleared from Account._domainEvents
   ✓ Now what?
   
   Someone needs to DO SOMETHING with this event!
   - Send welcome email
   - Log to audit trail
   - Update analytics
   - Send SMS notification
   - etc.
```

---

## DomainEventDispatcher to the Rescue

```csharp
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly BankingDbContext _context;
    private readonly IPublisher _publisher;  // MediatR
    
    public async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        // STEP 1: Find all aggregates (like Account) that have events
        var domainEntities = _context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(x => x.Entity.DomainEvents.Any())  // Only if has events
            .ToList();
        
        // STEP 2: Extract all events
        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();
        
        // STEP 3: Publish each event (deliver to handlers)
        foreach (var domainEvent in domainEvents)
        {
            _logger.LogInformation("Dispatching: {EventType}", domainEvent.GetType().Name);
            await _publisher.Publish(domainEvent, cancellationToken);
        }
        
        // STEP 4: Clean up - remove events after delivery
        domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());
    }
}
```

---

## Step-by-Step Breakdown

### **STEP 1: Find Events in Memory**

```
Account after Transfer:
┌──────────────────────────────────────────┐
│ Balance: ₦9,500                          │
│ _domainEvents: [                         │
│   MoneyTransferredEvent(...)            │
│ ]                                        │
└──────────────────────────────────────────┘

DomainEventDispatcher looks at this and says:
"I see an Account with events! Let me get them."
```

### **STEP 2: Extract All Events**

```
Found events:
1. MoneyTransferredEvent
   - From: Account 1234567890
   - To: Account 0987654321
   - Amount: ₦500
```

### **STEP 3: Publish Each Event (Deliver to Handlers)**

```
DomainEventDispatcher tells MediatR:
"Here's a MoneyTransferredEvent, 
 send it to everyone who cares about it"

MediatR finds all handlers for this event:

1. AccountCreatedEventHandler
   ↓
   Logs: "Account was created"
   Sends email: "Welcome!"

2. MoneyTransferedEventHandler
   ↓
   Logs: "Money transferred"
   Sends SMS: "₦500 transferred to Alice"
   Updates dashboard

3. InsufficientFundsEventHandler
   ↓
   (This handler doesn't care about MoneyTransferredEvent)
   (It only cares about InsufficientFundsEvent)
   So it ignores this one
```

### **STEP 4: Clean Up**

```
After all handlers have been called:

Account._domainEvents: [MoneyTransferredEvent]
                ↓
         Clear it (remove)
                ↓
Account._domainEvents: []  (empty)

Why? So the same event doesn't get delivered twice!
```

---

## When Does DomainEventDispatcher Get Called?

Look at **DomainEventsBehavior** (in MediatR pipeline):

```csharp
public class DomainEventsBehavior<TRequest, TResponse>: 
    IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // 1. Execute the actual handler (business logic)
        var response = await next();
        
        // 2. After handler completes successfully
        //    Dispatch any events that were created
        await _dispatcher.DispatchDomainEventsAsync(cancellationToken);
        
        // 3. Return response to user
        return response;
    }
}
```

**Timeline:**

```
04:00:00  API Request: Transfer money
           ↓
04:00:01  ValidationBehavior runs → Validates input ✓
           ↓
04:00:02  TransferMoneyCommandHandler runs → Business logic
           - Load accounts
           - Transfer money
           - Create MoneyTransferredEvent
           - Save to database
           ✓ Handler complete
           ↓
04:00:03  DomainEventDispatcher runs → Deliver events!
           - Find MoneyTransferredEvent
           - Call MoneyTransferedEventHandler
           - Call other handlers
           ✓ Events delivered
           ↓
04:00:04  Response sent to user
           "Transfer successful!"
```

---

## Complete Example: Money Transfer

### **Code in TransferMoneyCommandHandler**

```csharp
public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
{
    var sourceAccount = await _accountRepository.GetByAccountNumberAsync("1234567890");
    var destAccount = await _accountRepository.GetByAccountNumberAsync("0987654321");
    
    // This is where events are created
    sourceAccount.Transfer(
        amount: new Money(500),
        destination: destAccount,
        reference: "REF123",
        description: "Payment"
    );
    
    // Events stored in sourceAccount._domainEvents (in memory)
    
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    
    // Events COPIED to OutboxMessages table
    // Events CLEARED from sourceAccount._domainEvents
    
    return Result.Success();
    
    // At this point, handler is done
    // But events haven't been delivered yet!
}
```

### **DomainEventDispatcher Takes Over**

```csharp
await _dispatcher.DispatchDomainEventsAsync(cancellationToken);

// It runs automatically because DomainEventsBehavior calls it
```

### **What Happens in DomainEventDispatcher**

```
1. Check ChangeTracker for entities with events
   "Are there any aggregates (Accounts, Customers) with events?"
   
   Hmm, the events were already cleared from sourceAccount!
   So there's nothing to dispatch here.
   
   ✓ No events to dispatch (they're in Outbox now)

2. Later (30 seconds):
   OutboxBackgroundService wakes up
   Finds event in OutboxMessages table
   Deserializes it
   Publishes it
   Event handlers execute
```

---

## Key Difference

| What | Where | When |
|-----|-------|------|
| **Event Created** | In Account aggregate | When you call Transfer() |
| **Event Saved** | In OutboxMessages table | When SaveAsync() runs |
| **Event Dispatched** | To handlers | When DomainEventDispatcher runs |

---

## Simple Summary

```
DomainEventDispatcher:
1. ✉️ Finds events waiting to be delivered
2. 📬 Publishes them to MediatR
3. 🎯 Routes them to the right handlers
4. 🧹 Cleans up (removes events after delivery)

Like a postman:
- Gets mail from mailbox
- Reads each letter
- Sends to the right person
- Marks as delivered
```

---

## Timeline (Complete Picture)

```
04:00:00  Transfer Request
            ↓
04:00:01  Account.Transfer() called
            - Balance updated
            - MoneyTransferredEvent created ✅
            
04:00:02  SaveAsync() called
            - Account balance saved ✅
            - Event serialized to JSON ✅
            - OutboxMessage inserted ✅
            - Event cleared from Account ✅
            
04:00:03  DomainEventDispatcher.DispatchDomainEventsAsync()
            - Looks for events in Account
            - Event already cleared! ❌
            - Nothing to dispatch from immediate memory
            
            But wait... OutboxMessages table has it!
            
04:00:04  Response sent to user: "Transfer complete" ✅
            User happy!
            
04:00:30  OutboxBackgroundService wakes up
            - Finds OutboxMessage in database ✅
            - Deserializes → MoneyTransferredEvent
            - Publishes to MediatR
            - MediatR routes to handlers:
              ✓ AccountCreatedEventHandler
              ✓ MoneyTransferedEventHandler
              ✓ InsufficientFundsEventHandler (ignores)
              
04:00:31  Handlers execute:
            - Send emails ✅
            - Send SMS ✅
            - Log to audit ✅
            - Update dashboard ✅
            
04:00:32  OutboxMessage marked as ProcessedOn ✅
```

---

## Two Event Publishing Paths

### **Path 1: Immediate (in same request)**
```
DomainEventDispatcher runs after handler
→ Events in memory are dispatched
→ Handlers execute immediately
→ User waits for response
```

### **Path 2: Delayed (via Outbox)**
```
Events saved to OutboxMessages table
User gets response immediately
30 seconds later:
OutboxBackgroundService finds events
Deserializes and publishes
Handlers execute
(User never notices the delay)
```

**Your system uses Path 2 (Outbox pattern)** for reliability!

---

## What If Event Handler Crashes?

```
Scenario: Email service crashes during handler execution

04:00:30  OutboxMessageProcessor deserializes event
04:00:31  Calls MediatR to publish
04:00:32  MoneyTransferedEventHandler.Handle() called
04:00:33  try { Send email } 
          ❌ EMAIL SERVICE CRASHED!
          catch { Retry count++ }

Result:
- Event stays in OutboxMessages
- Retry count: 0 → 1
- Next check (04:01:00): Try again
- If still broken: Retry again (04:01:30)
- Max 3 attempts, then manual intervention
```

---

## The Role of DomainEventDispatcher in Your Code

It's called here (DomainEventsBehavior.cs):

```csharp
public async Task<TResponse> Handle(
    TRequest request, 
    RequestHandlerDelegate<TResponse> next, 
    CancellationToken cancellationToken)
{
    var response = await next();
    
    // ← YOUR CODE HERE (DomainEventDispatcher runs)
    await _dispatcher.DispatchDomainEventsAsync(cancellationToken);
    
    return response;
}
```

Located at:
```
CoreBankingTest.APP/Common/Behaviors/DomainEventsBehavior.cs
CoreBankingTest.DAL/Services/DomainEventDispatcher.cs
```

---

## Summary

**DomainEventDispatcher** is the **event delivery service**:
- Takes events from aggregates
- Publishes them to handlers
- Makes sure handlers execute
- Cleans up after delivery

It's like having a **secretary** who:
- Gets your message
- Finds the right department
- Delivers it
- Confirms delivery
- Files the message


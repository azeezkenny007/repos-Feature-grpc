# Action Map - CoreBanking System (Simple Flow)

## What's Happening in This Code: Simple Version

### 1. USER SENDS REQUEST
```
User Action
    │
    ├─ REST API: POST /api/accounts/transfer
    │  Body: { sourceAccountId, destinationAccountId, amount }
    │
    ├─ gRPC: Call AccountService.Transfer()
    │  Message: TransferRequest { source, dest, amount }
    │
    └─ Both routes → API Controller/gRPC Service
```

---

### 2. API RECEIVES & VALIDATES
```
API Controller / gRPC Service
    │
    ├─ Parse incoming request
    ├─ Create Command: TransferMoneyCommand
    ├─ Pass to MediatR
    │
    └─→ MediatR Pipeline starts
```

---

### 3. VALIDATION LAYER
```
ValidationBehavior (MediatR)
    │
    ├─ Run FluentValidation rules
    │  ├─ amount > 0? ✓
    │  ├─ sourceId != destId? ✓
    │  └─ both accounts exist? ✓
    │
    ├─ If FAIL → Return 400 Bad Request
    └─ If PASS → Continue to next step
```

---

### 4. LOGGING
```
LoggingBehavior (MediatR)
    │
    ├─ Log: "TransferMoneyCommand received"
    ├─ Log: Command details (source, dest, amount)
    │
    └─→ Pass to handler
```

---

### 5. BUSINESS LOGIC (DOMAIN LAYER)
```
TransferMoneyCommandHandler
    │
    ├─ Load source account from database
    │  └─ SELECT * FROM Accounts WHERE AccountId = @source
    │
    ├─ Load destination account from database
    │  └─ SELECT * FROM Accounts WHERE AccountId = @dest
    │
    └─→ Call Account.Transfer(amount, destination)
         │
         ├─ Check: Balance >= amount? 
         │  └─ If NO → Throw InsufficientFundsException
         │
         ├─ Check: Both accounts active?
         │  └─ If NO → Throw InvalidOperationException
         │
         ├─ Debit source account
         │  └─ source.Balance -= amount
         │
         ├─ Credit destination account
         │  └─ destination.Balance += amount
         │
         ├─ Create MoneyTransferredEvent
         │  ├─ sourceAccount, destAccount
         │  ├─ amount, reference
         │  └─ timestamp
         │
         ├─ Add event to account._domainEvents list
         │
         └─ Return Result.Success()
```

---

### 6. SAVE TO DATABASE (ATOMIC TRANSACTION)
```
UnitOfWork.SaveEntitiesAsync()
    │
    └─→ Context.SaveChangesWithOutboxAsync()
         │
         ├─ STEP 1: Extract all domain events
         │  └─ Get MoneyTransferredEvent from account._domainEvents
         │
         ├─ STEP 2: Serialize events to JSON
         │  └─ { type: "MoneyTransferredEvent", data: {...} }
         │
         ├─ STEP 3: Create OutBoxMessage entity
         │  ├─ Id: new Guid
         │  ├─ Type: "MoneyTransferredEvent"
         │  ├─ Content: "{serialized JSON}"
         │  ├─ OccurredOn: DateTime.Now
         │  └─ ProcessedOn: null (not yet processed)
         │
         ├─ STEP 4: Clear events from account
         │  └─ account._domainEvents.Clear()
         │
         ├─ STEP 5: SaveChangesAsync() [ATOMIC]
         │  ├─ UPDATE Accounts SET Balance = 300 WHERE RowVersion = 0x0A
         │  ├─ UPDATE Accounts SET Balance = 1100 WHERE RowVersion = 0x0B
         │  ├─ INSERT OutBoxMessages (event storage)
         │  └─ COMMIT all or ROLLBACK all
         │
         └─ STEP 6: Both entities now in database
              ├─ Accounts table: Updated balances
              └─ OutboxMessages table: Event persisted
```

---

### 7. IMMEDIATE EVENT HANDLERS (Synchronous)
```
DomainEventsBehavior (MediatR)
    │
    ├─ After handler completes
    ├─ Get MoneyTransferredEvent from aggregates
    │
    ├─ Publish to MediatR handlers
    │  ├─ MoneyTransferedEventHandler
    │  │  └─ Log: "Transfer completed"
    │  │
    │  ├─ RealTimeNotificationEventHandler
    │  │  ├─ Get SignalR Hub context
    │  │  ├─ Broadcast to TransactionHub
    │  │  ├─ Broadcast to NotificationHub
    │  │  └─ Send to connected clients: { balance: 300, status: "success" }
    │  │
    │  └─ [Other handlers execute...]
    │
    └─→ Client receives live update via WebSocket
```

---

### 8. RETURN RESPONSE TO CLIENT
```
Response Builder
    │
    ├─ Map Account to AccountDto
    ├─ Include new balance, timestamp, status
    │
    └─ Return 200 OK + JSON:
       {
         sourceAccountId: "...",
         destinationAccountId: "...",
         amount: 100,
         sourceNewBalance: 300,
         destNewBalance: 1100,
         status: "SUCCESS",
         timestamp: "2025-11-07T01:47:36Z"
       }
```

---

### 9. CLIENT SEES RESULT (Real-time)
```
Web Browser / Mobile App
    │
    ├─ HTTP Response: 200 OK
    │  └─ Display confirmation message
    │
    ├─ SignalR WebSocket notification
    │  ├─ Balance updated on screen
    │  ├─ Show "Transfer completed"
    │  └─ Refresh account view
    │
    └─ User sees: "Transfer successful! New balance: $300"
```

---

### 10. BACKGROUND PROCESSING (Every 30 Seconds)
```
OutboxBackgroundService (Hosted Service)
    │
    ├─ Timer triggers every 30 seconds
    │
    ├─ Query database:
    │  └─ SELECT * FROM OutboxMessages 
    │     WHERE ProcessedOn IS NULL
    │     LIMIT 20
    │
    ├─ Found our MoneyTransferredEvent
    │
    └─→ OutboxMessageProcessor.ProcessAsync()
         │
         ├─ STEP 1: Deserialize JSON → MoneyTransferredEvent
         │  └─ Parse Content field back to object
         │
         ├─ STEP 2: Publish event via MediatR
         │  ├─ Execute all registered handlers again
         │  └─ (External systems can subscribe)
         │
         ├─ STEP 3: Mark as processed
         │  └─ UPDATE OutboxMessages 
         │     SET ProcessedOn = NOW()
         │     WHERE Id = @id
         │
         ├─ STEP 4: SaveChangesAsync()
         │  └─ Update persisted in database
         │
         └─ On error:
            ├─ Increment RetryCount
            ├─ Store error message
            └─ Next cycle will retry (max 3 times)
```

---

## Quick Summary: The Journey of $100 Transfer

```
TIMELINE:
═════════════════════════════════════════════════════════════════════

T+0s
USER: "Transfer $100 from Account A to Account B"
  ↓
API receives → Validates ✓ → Loads accounts → Checks balance ✓
  ↓
Debit A: 500 → 400
Credit B: 1000 → 1100
  ↓
Event created: MoneyTransferredEvent
  ↓
ATOMIC SAVE:
  • Accounts updated ✓
  • Event stored in OutBox ✓
  ↓
Event handlers execute → SignalR broadcasts → UI updates live ✓
  ↓
API returns 200 OK to user
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

T+0-30s
Background service sleeping...

T+30s
Background service wakes up
  ↓
Query: "Any unprocessed events?"
  ↓
Found: MoneyTransferredEvent in OutBox
  ↓
Deserialize JSON → Publish to handlers
  ↓
Mark as processed
  ↓
Save to database
  ↓
Go back to sleep for 30 more seconds...

RESULT: Event fully processed and persisted
═════════════════════════════════════════════════════════════════════
```

---

## What Each Layer Does

```
┌────────────────────────────────────────────────────────┐
│ API LAYER (Controller/gRPC)                            │
│ • Receives HTTP/gRPC requests                         │
│ • Parses JSON/Protobuf                                │
│ • Creates Command objects                             │
└────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────┐
│ APPLICATION LAYER (MediatR Pipeline)                   │
│ • Validates input                                      │
│ • Logs operations                                      │
│ • Routes to command handlers                          │
└────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────┐
│ DOMAIN LAYER (Business Logic)                          │
│ • Account.Transfer() executes                         │
│ • Checks business rules (balance, active, etc)        │
│ • Creates domain events                               │
│ • Modifies account state                              │
└────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────┐
│ INFRASTRUCTURE LAYER (Persistence)                     │
│ • SaveChangesWithOutboxAsync()                        │
│ • Extracts and serializes events                      │
│ • Saves to database atomically                        │
│ • Manages outbox table                                │
└────────────────────────────────────────────────────────┘
                          ↓
┌────────────────────────────────────────────────────────┐
│ DATABASE LAYER (SQL Server)                            │
│ • Accounts table (updated balance)                    │
│ • OutboxMessages table (event stored)                 │
│ • Transactions table (transfer record)                │
└────────────────────────────────────────────────────────┘
```

---

## Key Concepts in Plain English

### Domain Events
- **What**: Records of things that happened ("Money Transferred")
- **When**: Created when business action occurs
- **Where**: Stored in memory on aggregate, then in OutBox table
- **Why**: Track history + trigger actions elsewhere

### Outbox Pattern
- **What**: A database table that stores events
- **Why**: Ensures events don't get lost if app crashes
- **How**: 
  - Save entity + event to DB in one transaction
  - Background service polls DB for unprocessed events
  - Publishes them asynchronously with retries

### RowVersion (Concurrency Token)
- **What**: A version number on each account
- **Why**: Prevents two users from overwriting each other's changes
- **How**: If someone else changes account, RowVersion changes too
  - UPDATE fails with "concurrency error"
  - User gets 409 response: "Please reload and try again"

### Soft Delete
- **What**: Instead of deleting from DB, just mark as deleted
- **Why**: Preserves audit trail + allows recovery
- **How**: Set IsDeleted = true, add DeletedAt timestamp
  - Query filter hides deleted records automatically
  - Data not truly gone, just hidden

### SignalR
- **What**: Real-time communication library
- **Why**: Send live updates to connected clients
- **How**: WebSocket connection → Server pushes updates → UI changes instantly

---

## Error Scenarios

```
❌ INSUFFICIENT FUNDS
  Validation fails → Throw exception → 
  Return 400 Bad Request with error message → 
  User sees: "You don't have enough balance"

❌ CONCURRENT MODIFICATION
  User A and B modify same account → 
  User B's RowVersion doesn't match → 
  Update fails → Return 409 Conflict → 
  User sees: "Account was modified elsewhere. Please reload"

❌ NETWORK ERROR DURING SAVE
  Event in OutBox but not processed yet → 
  Background service retries up to 3x → 
  If still fails: Error logged, user informed later

✅ SUCCESS
  Event saved + processed → Response sent → 
  User updated → Background service completes → 
  Full audit trail created
```


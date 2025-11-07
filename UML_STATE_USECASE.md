# UML State & Use Case Diagrams - CoreBanking System

## 1. State Diagram: Account Lifecycle

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                          STATE DIAGRAM: ACCOUNT LIFECYCLE                             │
└────────────────────────────────────────────────────────────────────────────────────────┘

                                   INITIAL STATE
                                        │
                                        ▼
                    ┌──────────────────────────────────┐
                    │   [*] Creating Account           │
                    │  ──────────────────────────────  │
                    │  Account.Create() called         │
                    │  - Initialize with balance       │
                    │  - Set AccountId, AccountNumber  │
                    │  - Emit AccountCreatedEvent      │
                    └─────────┬────────────────────────┘
                              │
                    ──────────┼──────────►  AccountCreatedEvent.ProcessedOn = NOW()
                    (event persisted in OutBox)
                              │
                              ▼
                    ┌──────────────────────────────────┐
                    │   ACTIVE STATE                   │
                    │  ──────────────────────────────  │
                    │  account.IsActive = true         │
                    │  account.IsDeleted = false       │
                    │                                  │
                    │  Valid Operations:               │
                    │  • Deposit(Money)                │
                    │  • Withdraw(Money)               │
                    │  • Transfer(dest, amount)        │
                    │  • UpdateBalance(Money)          │
                    │                                  │
                    └─────────┬────────────┬──────────┘
                              │            │
                    ┌─────────┴─────┐  ┌──┴─────────────────────┐
                    │               │  │                        │
                    ▼               ▼  ▼                        │
            ┌────────────────┐ ┌──────────────────┐             │
            │  CLOSING       │ │ ERROR STATES     │             │
            │  ──────────    │ ├──────────────────┤             │
            │ close()        │ │ InsufficientFunds│             │
            │ Balance != 0   │ │ TransferFailed   │             │
            │ BLOCKED ✗      │ │ ValidationFailed │             │
            │                │ │                  │             │
            │ close()        │ │ Auto-recovery    │             │
            │ Balance == 0   │ │ Exception caught │             │
            │ SUCCESS ✓      │ │ → Logged         │             │
            │                │ │ → User notified  │             │
            └────┬───────────┘ │ (no state change)│             │
                 │             └──────────────────┘             │
                 │                                               │
                 ▼                                               │
            ┌──────────────────────────────────┐                │
            │   CLOSED STATE                   │                │
            │  ──────────────────────────────  │                │
            │  account.IsActive = false        │                │
            │  account.IsDeleted = false       │                │
            │                                  │                │
            │  Valid Operations: NONE          │                │
            │  • Deposit: BLOCKED              │                │
            │  • Withdraw: BLOCKED             │                │
            │  • Transfer: BLOCKED             │                │
            │                                  │                │
            └────────────┬────────────────────┘                │
                         │                                      │
        ┌────────────────┴──────────────────────┐               │
        │                                       │               │
        ▼                                       ▼               │
┌──────────────────────────┐           ┌─────────────────────────┐
│  PERMANENT DELETION      │           │  SOFT DELETED STATE     │
│  ──────────────────────  │           │  ──────────────────────│
│  Hard delete from DB     │           │ account.IsDeleted = true
│  (rare operation)        │           │ account.DeletedAt = NOW()
│  Audit trail preserved   │           │ account.DeletedBy = 'admin'
│  (events still in OutBox)│           │                        │
│                          │           │  Valid Operations:     │
│  Path: Admin action      │           │  • Restore (undo)      │
│                          │           │  • Purge (hard delete) │
│                          │           │                        │
└──────────────────────────┘           └─────────────────────────┘
         (End state)

TRANSITIONS & EVENTS:

1. ACTIVE → CLOSED
   Event: CloseAccount()
   Condition: Balance == 0
   Action: IsActive = false

2. ACTIVE → ERROR (transient)
   Event: InsufficientFundsException
   Condition: Balance < WithdrawAmount
   Action: Event logged, user retries
   Recovery: Automatic (state unchanged)

3. ACTIVE → SOFT DELETED
   Event: SoftDelete()
   Condition: Admin action or business logic
   Action: IsDeleted = true, DeletedAt = NOW()
   Side Effect: Global query filter hides account

4. SOFT DELETED → ACTIVE
   Event: Restore()
   Condition: Admin restore action
   Action: IsDeleted = false, DeletedAt = NULL
   Side Effect: Account reappears in queries

5. ANY → AUDIT LOG
   All transitions recorded with:
   - timestamp
   - user/service
   - old state
   - new state
   - reason


BUSINESS RULES BY STATE:

┌──────────────┬─────────────┬──────────────┬──────────┬──────────────┐
│ State        │ Deposit     │ Withdraw     │ Transfer │ Close        │
├──────────────┼─────────────┼──────────────┼──────────┼──────────────┤
│ ACTIVE       │ ✓ Allowed   │ ✓ Allowed    │ ✓ Allowed│ ✓ If Bal=0   │
│ CLOSED       │ ✗ Blocked   │ ✗ Blocked    │ ✗ Blocked│ ✗ Already    │
│ SOFT DELETE  │ ✗ Blocked   │ ✗ Blocked    │ ✗ Blocked│ ✗ N/A        │
│ PENDING      │ ~ Queued    │ ~ Queued     │ ~ Queued │ ~ Hold       │
└──────────────┴─────────────┴──────────────┴──────────┴──────────────┘

ACCOUNT STATUS SCENARIOS:

Scenario 1: Happy Path (Full Account Lifecycle)
┌──────────────┐
│ [*] CREATE   │
└────────┬─────┘
         │
         ├─→ Deposit(1000) ──→ Balance: 1000, Status: ACTIVE
         ├─→ Transfer(200)  ──→ Balance: 800,  Status: ACTIVE
         ├─→ Withdraw(300)  ──→ Balance: 500,  Status: ACTIVE
         └─→ Withdraw(500)  ──→ Balance: 0,    Status: ACTIVE
                ├─→ CloseAccount() ──→ Status: CLOSED
                └─→ [End]

Scenario 2: Insufficient Funds
┌──────────────┐
│ [*] CREATE   │
└────────┬─────┘
         │
         ├─→ Deposit(100)       ──→ Balance: 100
         └─→ Withdraw(200)      ──→ ✗ InsufficientFundsException
              └─→ Error logged, Balance: 100 (unchanged)
              └─→ User notified
              └─→ Status: ACTIVE (stays active for retry)

Scenario 3: Soft Delete & Restore
┌──────────────┐
│ [*] CREATE   │
└────────┬─────┘
         │
         ├─→ Deposit(500)       ──→ Status: ACTIVE
         ├─→ AdminDelete()       ──→ IsDeleted: true, Status: SOFT DELETED
         │                          Query filters hide account
         ├─→ AdminRestore()      ──→ IsDeleted: false, Status: ACTIVE
         └─→ Withdraw(100)       ──→ Balance: 400, Status: ACTIVE
```

---

## 2. Use Case Diagram: Banking Operations

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                           USE CASE DIAGRAM: BANKING SYSTEM                            │
└────────────────────────────────────────────────────────────────────────────────────────┘

                    ┌─────────────────────────────────────────────────┐
                    │       COREBANKING SYSTEM                         │
                    │                                                  │
                    │  ┌──────────────────────────────────────────┐  │
                    │  │  CUSTOMER ACCOUNT MANAGEMENT             │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-1: Create Customer Account    │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Input: Name, Email, Phone         │  │
                    │  │     - Process: Validate, store to DB    │  │
                    │  │     - Output: CustomerId, confirmation  │  │
                    │  │     - Error: Email exists, validation  │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-2: Open New Account            │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Precondition: Customer exists     │  │
                    │  │     - Input: AccountType, initial amt   │  │
                    │  │     - Process: Create account, emit evt │  │
                    │  │     - Output: AccountId, confirmation   │  │
                    │  │     - Error: Invalid initial amount    │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-3: View Account Details        │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Input: AccountId                  │  │
                    │  │     - Process: Query repo, map to DTO  │  │
                    │  │     - Output: AccountDto (Balance, etc) │  │
                    │  │     - Error: Account not found          │  │
                    │  └──────────────────────────────────────────┘  │
                    │                                                  │
                    │  ┌──────────────────────────────────────────┐  │
                    │  │  TRANSACTION OPERATIONS                  │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-4: Deposit Money              │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Precondition: Account active      │  │
                    │  │     - Input: Amount, description        │  │
                    │  │     - Process: Validate, update balance │  │
                    │  │     - Output: Transaction record        │  │
                    │  │     - Error: Account inactive           │  │
                    │  │     - Event: TransactionCreated         │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-5: Withdraw Money              │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Precondition: Account active      │  │
                    │  │     - Input: Amount, description        │  │
                    │  │     - Process: Check balance, update    │  │
                    │  │     - Output: Transaction record        │  │
                    │  │     - Error: Insufficient funds         │  │
                    │  │     - Event: TransactionCreated         │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-6: Transfer Money              │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Precondition: Both accounts active│  │
                    │  │     - Input: DestAcctId, Amount, Ref   │  │
                    │  │     - Process: Debit source,            │  │
                    │  │               Credit destination,       │  │
                    │  │               Emit event                │  │
                    │  │     - Output: Transaction record        │  │
                    │  │     - Error: Insufficient, not same acc │  │
                    │  │     - Event: MoneyTransferred           │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-7: View Transaction History    │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Input: AccountId, daterange       │  │
                    │  │     - Process: Query transactions       │  │
                    │  │     - Output: List<TransactionDto>      │  │
                    │  │     - Error: Account not found          │  │
                    │  └──────────────────────────────────────────┘  │
                    │                                                  │
                    │  ┌──────────────────────────────────────────┐  │
                    │  │  ACCOUNT MANAGEMENT                      │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-8: Close Account               │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Precondition: Balance == 0        │  │
                    │  │     - Input: AccountId, reason          │  │
                    │  │     - Process: Set IsActive=false       │  │
                    │  │     - Output: Confirmation              │  │
                    │  │     - Error: Balance > 0                │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-9: Delete Account (Soft)       │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Precondition: Admin role          │  │
                    │  │     - Input: AccountId, reason          │  │
                    │  │     - Process: Set IsDeleted=true       │  │
                    │  │     - Output: Confirmation              │  │
                    │  │     - Side Effect: Filtered from queries│  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-10: Restore Account            │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Precondition: Admin role, deleted │  │
                    │  │     - Input: AccountId                  │  │
                    │  │     - Process: Set IsDeleted=false      │  │
                    │  │     - Output: Confirmation              │  │
                    │  │     - Side Effect: Reappears in queries │  │
                    │  └──────────────────────────────────────────┘  │
                    │                                                  │
                    │  ┌──────────────────────────────────────────┐  │
                    │  │  NOTIFICATIONS & ANALYTICS              │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-11: Receive Real-time Updates  │ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Trigger: Transaction completed    │  │
                    │  │     - Channel: SignalR WebSocket        │  │
                    │  │     - Output: Update notification       │  │
                    │  │     - Broadcast: To all connected users │  │
                    │  │                                          │  │
                    │  │  ┌────────────────────────────────────┐ │  │
                    │  │  │  UC-12: Generate Transaction Report│ │  │
                    │  │  └────────────────────────────────────┘ │  │
                    │  │     - Input: Date range, filters        │  │
                    │  │     - Process: Aggregate data, format   │  │
                    │  │     - Output: Report (PDF/CSV/JSON)    │  │
                    │  │     - Error: Date range invalid         │  │
                    │  └──────────────────────────────────────────┘  │
                    │                                                  │
                    └─────────────────────────────────────────────────┘

                              ▲                    ▲
                              │                    │
                              │ uses               │ uses
                              │                    │
                    ┌─────────────────┐   ┌──────────────────┐
                    │  Customer       │   │  Administrator   │
                    │  (End User)     │   │  (Operator)      │
                    │                 │   │                  │
                    │ • Browse account│   │ • Manage accounts│
                    │ • Make transfers│   │ • Delete/restore │
                    │ • View history  │   │ • Generate report│
                    │ • Set alerts    │   │ • Audit logs     │
                    └─────────────────┘   └──────────────────┘


ACTOR-USE CASE MATRIX:

┌─────────────────┬──────────────┬──────────────────┐
│ Use Case        │ Customer     │ Administrator    │
├─────────────────┼──────────────┼──────────────────┤
│ UC-1 (Create)   │ ✓ (self)     │ ✓ (on behalf)    │
│ UC-2 (Open)     │ ✓            │ ✓                │
│ UC-3 (View)     │ ✓ (own)      │ ✓ (all)          │
│ UC-4 (Deposit)  │ ✓            │ ✓                │
│ UC-5 (Withdraw) │ ✓            │ ✓                │
│ UC-6 (Transfer) │ ✓ (own to *)  │ ✓ (any to any)   │
│ UC-7 (History)  │ ✓ (own)      │ ✓ (all)          │
│ UC-8 (Close)    │ ✓ (own)      │ ✓ (any)          │
│ UC-9 (Delete)   │ ✗            │ ✓                │
│ UC-10 (Restore) │ ✗            │ ✓                │
│ UC-11 (Notify)  │ ✓ (auto)     │ ✓ (auto)         │
│ UC-12 (Report)  │ ✗            │ ✓                │
└─────────────────┴──────────────┴──────────────────┘
```

---

## 3. Activity Diagram: Money Transfer Process

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                    ACTIVITY DIAGRAM: MONEY TRANSFER PROCESS                           │
└────────────────────────────────────────────────────────────────────────────────────────┘

                            ┌─────────────────────┐
                            │   [Start]           │
                            │  Transfer Request   │
                            └──────────┬──────────┘
                                       │
                                       ▼
                            ┌─────────────────────┐
                            │ Receive Request     │
                            │ (Source, Dest, Amt) │
                            └──────────┬──────────┘
                                       │
                                       ▼
                    ┌──────────────────────────────────────┐
                    │  VALIDATE REQUEST                    │
                    │  ├─ Amount > 0?                     │
                    │  ├─ Source ≠ Destination?           │
                    │  ├─ Both accounts exist?            │
                    │  └─ FluentValidation rules          │
                    └──┬──────────────────┬───────────────┘
                       │                  │
                       │ Valid            │ Invalid
                       ▼                  ▼
                    [Valid]         ┌─────────────────┐
                       │            │ Return 400      │
                       │            │ Bad Request     │
                       │            └────────┬────────┘
                       │                     │
                       │                     ▼
                       │            ┌─────────────────┐
                       │            │   [End - Error] │
                       │            └─────────────────┘
                       │
                       ▼
            ┌──────────────────────────┐
            │ Load Source Account      │
            │ From Repository          │
            └──────────┬───────────────┘
                       │
                       ▼
            ┌──────────────────────────┐
            │ Load Dest Account        │
            │ From Repository          │
            └──────────┬───────────────┘
                       │
                       ▼
            ┌──────────────────────────────────────┐
            │ CHECK BUSINESS RULES                 │
            │ ├─ Source balance >= amount?         │
            │ ├─ Source account active?            │
            │ ├─ Dest account active?              │
            │ ├─ Not locked by concurrency?        │
            │ └─ Savings withdrawal limit?         │
            └──┬────────────────────────┬──────────┘
               │                        │
               │ Pass                   │ Fail
               ▼                        ▼
           [Pass]                  ┌─────────────────────────┐
               │                   │ Emit InsufficientFunds  │
               │                   │ or ValidationException  │
               │                   │                         │
               │                   │ Log error               │
               │                   │ Return error response   │
               │                   └────────┬────────────────┘
               │                            │
               ▼                            ▼
    ┌──────────────────────┐        ┌──────────────────┐
    │ DEBIT SOURCE         │        │   [End - Error]  │
    │ source.Debit(amt)    │        └──────────────────┘
    │ Balance -= amount    │
    │ Add to transactions  │
    └──────────┬───────────┘
               │
               ▼
    ┌──────────────────────┐
    │ CREDIT DESTINATION   │
    │ dest.Credit(amt)     │
    │ Balance += amount    │
    │ Add to transactions  │
    └──────────┬───────────┘
               │
               ▼
    ┌──────────────────────────────┐
    │ EMIT DOMAIN EVENT            │
    │ MoneyTransferredEvent:       │
    │ ├─ SourceAcct                │
    │ ├─ DestAcct                  │
    │ ├─ Amount                    │
    │ ├─ Reference                 │
    │ └─ OccurredOn: DateTime.Now  │
    └──────────┬───────────────────┘
               │
               ▼
    ┌──────────────────────────────────┐
    │ SAVE WITH OUTBOX PATTERN         │
    │ SaveChangesWithOutboxAsync():    │
    │                                  │
    │ ├─ Extract events from accounts │
    │ ├─ Create OutBoxMessage entity  │
    │ │   Type: "MoneyTransferredEvent"
    │ │   Content: JSON serialized    │
    │ │   OccurredOn: timestamp       │
    │ │   ProcessedOn: null           │
    │ │                               │
    │ ├─ Clear domain events          │
    │ │   _domainEvents.Clear()       │
    │ │                               │
    │ ├─ SaveChangesAsync()           │
    │ │   [ATOMIC TRANSACTION]        │
    │ │   ├─ INSERT INTO Accounts ... │
    │ │   ├─ INSERT INTO OutBoxMsg ..│
    │ │   └─ COMMIT or ROLLBACK       │
    │ │                               │
    │ └─ Return updated accounts      │
    └──────────┬────────────────────┬─┘
               │                    │
               │ Success            │ Concurrency Error
               │ RowVersion match   │ (RowVersion mismatch)
               ▼                    ▼
           [Success]            ┌──────────────────────┐
               │                │ DbUpdateConcurrency  │
               │                │ Exception thrown     │
               │                │                      │
               │                │ RowVersion conflict  │
               │                │ detected             │
               │                │                      │
               │                │ Return 409 Conflict  │
               │                │ "Record modified"    │
               │                └────────┬─────────────┘
               │                         │
               ▼                         ▼
    ┌──────────────────────┐        ┌──────────────────┐
    │ INVOKE DOMAIN EVENT  │        │   [End - Error]  │
    │ HANDLERS             │        └──────────────────┘
    │                      │
    │ DomainEventsBehavior │
    │ ├─ Get events        │
    │ └─ Publish via       │
    │   MediatR            │
    └──────────┬───────────┘
               │
               ▼
    ┌──────────────────────────────┐
    │ EVENT HANDLERS EXECUTE       │
    │ (Parallel or Sequential)     │
    │                              │
    │ 1. MoneyTransferred...       │
    │    └─ Logs transfer          │
    │    └─ Updates audit          │
    │                              │
    │ 2. RealTimeNotification...   │
    │    └─ Gets SignalR context   │
    │    └─ Broadcasts to hub:     │
    │       TransactionHub         │
    │       NotificationHub        │
    │       EnhancedNotification   │
    │    └─ Sends update to client │
    │       (Balance change)       │
    │                              │
    │ 3. [Other handlers...]       │
    │                              │
    └──────────┬───────────────────┘
               │
               ▼
    ┌──────────────────────────┐
    │ BUILD RESPONSE           │
    │ ├─ Map Account to DTO    │
    │ ├─ Include new balance   │
    │ ├─ Include timestamp     │
    │ └─ Status: SUCCESS       │
    └──────────┬───────────────┘
               │
               ▼
    ┌──────────────────────────┐
    │ RETURN TO CLIENT         │
    │ HTTP 200 OK +            │
    │ TransferResponseDto      │
    └──────────┬───────────────┘
               │
               ▼
    ┌──────────────────────────┐
    │ BACKGROUND PROCESS       │
    │ (T+30s later)            │
    │                          │
    │ OutboxBackgroundService: │
    │ ├─ Query OutboxMessages  │
    │ │  WHERE ProcessedOn NULL │
    │ ├─ Find our message      │
    │ └─ ProcessAsync()        │
    │    ├─ Deserialize JSON   │
    │    ├─ Publish event      │
    │    ├─ Mark ProcessedOn   │
    │    └─ SaveChangesAsync() │
    │                          │
    └──────────┬───────────────┘
               │
               ▼
    ┌──────────────────────┐
    │   [End - Success]    │
    │ Transfer completed   │
    │ Event processed      │
    │ Notifications sent   │
    └──────────────────────┘

SWIMLANES (Parallel Activities):

MAIN THREAD                      BACKGROUND THREAD (30s later)
│                                │
├─ Validate request              ├─ Query OutboxMessages
├─ Load accounts                 ├─ Find unprocessed msgs
├─ Check business rules          ├─ OutboxMessageProcessor
├─ Debit/Credit                  │  ├─ Deserialize JSON
├─ Emit event                    │  ├─ Publish to MediatR
├─ Save (atomic transaction)     │  └─ Mark processed
├─ Invoke handlers               │
├─ SignalR broadcast             ├─ Retry logic
├─ Return 200 OK                 │  ├─ Max 3 retries
│                                │  ├─ Log errors
│                                │  └─ Store failed state
│                                │
└─ Client receives response      └─ Polling continues (every 30s)
```

---

## 4. Sequence Diagram: Concurrency Resolution

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│              SEQUENCE DIAGRAM: CONCURRENCY CONFLICT RESOLUTION                         │
└────────────────────────────────────────────────────────────────────────────────────────┘

USER A (Browser)         USER B (Mobile)      Database            EF Core
       │                      │                   │                  │
       │ GET /accounts/123    │                   │                  │
       ├──────────────────────────────────────────►│                  │
       │                      │                   │                  │
       │                      │ GET /accounts/123 │                  │
       │                      ├────────────────────────────────────────►
       │                      │                   │                  │
       │◄──────────────────────────────────────────────────────────────┤
       │ Account:             │                   │  SELECT with      │
       │ Balance: 500         │                   │  RowVersion: 0x0A │
       │ RowVersion: 0x0A    │                   │                  │
       │                      │◄────────────────────────────────────────┤
       │                      │ Account:          │  SELECT with      │
       │                      │ Balance: 500      │  RowVersion: 0x0A │
       │                      │ RowVersion: 0x0A │                  │
       │                      │                   │                  │
       │ Modify:              │                   │                  │
       │ Transfer $200        │ Modify:           │                  │
       │ → Balance: 300       │ Transfer $300     │                  │
       │ RowVersion: 0x0B    │ → Balance: 200    │                  │
       │                      │ RowVersion: 0x0B │                  │
       │                      │                   │                  │
       │ POST /transfer       │                   │                  │
       │ {Amount: 200, ...}   │                   │                  │
       ├──────────────────────────────────────────►│                  │
       │                      │                   │                  │
       │                      │                   │ UPDATE Accounts │
       │                      │                   │ SET Balance=300 │
       │                      │                   │ WHERE           │
       │                      │                   │ RowVersion=0x0A│
       │                      │                   │                 │
       │                      │                   │ [Row found]     │
       │                      │                   │ Success ✓       │
       │                      │                   │ RowVersion: 0x0B
       │                      │                   ├──────────────────►
       │◄──────────────────────────────────────────────────────────────┤
       │ 200 OK               │                   │  Transaction    │
       │ New Balance: 300     │                   │  committed      │
       │                      │                   │                  │
       │                      │ POST /transfer    │                  │
       │                      │ {Amount: 300, ...}│                  │
       │                      ├────────────────────────────────────────►
       │                      │                   │                  │
       │                      │                   │ UPDATE Accounts │
       │                      │                   │ SET Balance=200 │
       │                      │                   │ WHERE           │
       │                      │                   │ RowVersion=0x0A │
       │                      │                   │ (Already 0x0B!) │
       │                      │                   │                 │
       │                      │                   │ [Row NOT found] │
       │                      │                   │ 0 rows affected │
       │                      │                   │ DbUpdateConcurrencyException
       │                      │                   ├──────────────────►
       │                      │◄────────────────────────────────────────┤
       │                      │ 409 Conflict     │ EF Core throws   │
       │                      │ "The record was   │ exception caught │
       │                      │ modified by       │ by controller    │
       │                      │ another user.     │                  │
       │                      │ Please reload."   │                  │
       │                      │                   │                  │
       │                      │ GET /accounts/123 │                  │
       │                      │ (Reload)          │                  │
       │                      ├────────────────────────────────────────►
       │                      │                   │                  │
       │                      │◄────────────────────────────────────────┤
       │                      │ Account:          │ Fresh data with  │
       │                      │ Balance: 300      │ updated version  │
       │                      │ RowVersion: 0x0B │                  │
       │                      │                   │                  │
       │                      │ Retry with new    │                  │
       │                      │ balance state     │                  │
       │                      │                   │                  │
       │                      │ Transfer $50      │                  │
       │                      │ (300 - 50 = 250)  │                  │
       │                      │ RowVersion: 0x0C │                  │
       │                      │                   │                  │
       │                      │ POST /transfer    │                  │
       │                      │ {Amount: 50, ...} │                  │
       │                      ├────────────────────────────────────────►
       │                      │                   │                  │
       │                      │                   │ UPDATE Accounts │
       │                      │                   │ SET Balance=250 │
       │                      │                   │ WHERE           │
       │                      │                   │ RowVersion=0x0B │
       │                      │                   │                 │
       │                      │                   │ [Row found]     │
       │                      │                   │ Success ✓       │
       │                      │                   │ RowVersion: 0x0C
       │                      │                   ├──────────────────►
       │                      │◄────────────────────────────────────────┤
       │                      │ 200 OK            │ Transaction      │
       │                      │ New Balance: 250  │ committed        │
       │                      │                   │                  │

KEY POINTS:

1. OPTIMISTIC LOCKING
   - No database locks held
   - RowVersion acts as "version stamp"
   - Each update increments RowVersion

2. CONFLICT DETECTION
   - UPDATE checks WHERE RowVersion = @oldValue
   - If changed by another thread → 0 rows updated
   - EF Core detects this and throws exception

3. RESOLUTION STRATEGIES
   ┌─────────────────────────────────────────┐
   │ Strategy    │ Use When           │ Action  │
   ├─────────────┼────────────────────┼─────────┤
   │ Fail Fast   │ User action needed │ Throw   │
   │             │ (not auto)         │ 409     │
   │             │                    │         │
   │ Reload+     │ Determined by      │ Reload  │
   │ Retry       │ business logic     │ Retry   │
   │             │ (payments: NO)     │         │
   │             │ (UI state: YES)    │         │
   │             │                    │         │
   │ Last Write  │ Non-critical       │ Overwrite
   │ Wins        │ Updates            │ (rare)  │
   │             │                    │         │
   └─────────────┴────────────────────┴─────────┘

4. IN COREBANKING SYSTEM
   - Money transfers: FAIL FAST (no auto-retry)
   - UI state updates: RELOAD & RETRY
   - Audit: Log conflict for investigation
```


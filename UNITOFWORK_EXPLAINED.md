# UnitOfWork Pattern - Simple Explanation

## What Does UnitOfWork Do?

**UnitOfWork** is a **single button** that saves everything you changed to the database in one atomic operation.

Instead of calling `SaveAsync()` multiple times in different places, you have one central place to call it.

---

## Real-World Analogy: Restaurant Order

### ❌ **WITHOUT UnitOfWork (Messy)**

```
Chef: "I made the pasta"
Waiter: "Let me save pasta to database"  ← saves ✅

Chef: "I made the sauce"
Waiter: "Let me save sauce to database"  ← saves ✅

Chef: "I made the salad"
Waiter: "Let me save salad to database"  ← saves ✅

Problem:
- If salad save fails, pasta already saved
- Inconsistent state
- No single transaction
- Nightmare to debug
```

### ✅ **WITH UnitOfWork (Clean)**

```
Chef: "Here's the complete order"
(pasta, sauce, salad, bread, all done)

Manager (UnitOfWork): "Wait, let me save EVERYTHING at once"
                      ✅ SaveAsync() once
                      
All-or-nothing:
- Everything saved together
- If ANY part fails, NOTHING saves
- Consistent state guaranteed
```

---

## Your Banking System

### **Without UnitOfWork**

```csharp
public class TransferMoneyCommandHandler
{
    public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
    {
        var sourceAccount = await _accountRepository.GetByAccountNumberAsync("1234567890");
        var destAccount = await _accountRepository.GetByAccountNumberAsync("0987654321");
        
        // Transfer logic
        sourceAccount.Transfer(...);
        
        // Save source account
        await _accountRepository.SaveChangesAsync();  ← Save #1
        
        // Save destination account  
        await _accountRepository.SaveChangesAsync();  ← Save #2
        
        // Save transaction history
        await _transactionRepository.SaveChangesAsync();  ← Save #3
        
        // Problem: Multiple saves, no transaction guarantee!
    }
}
```

**Problems:**
- Save #1 succeeds, Save #2 fails → Inconsistent state
- Hard to debug
- Multiple database round-trips
- No guarantee all changes persist together

---

## **WITH UnitOfWork (Clean)**

```csharp
public class TransferMoneyCommandHandler
{
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
    {
        var sourceAccount = await _accountRepository.GetByAccountNumberAsync("1234567890");
        var destAccount = await _accountRepository.GetByAccountNumberAsync("0987654321");
        
        // Transfer logic
        sourceAccount.Transfer(...);
        
        // ONE central save for everything
        await _unitOfWork.SaveChangesAsync(cancellationToken);  ← ONE save
        
        // All changes persisted together, or nothing persists
        return Result.Success();
    }
}
```

---

## What Is UnitOfWork?

```csharp
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly BankingDbContext _context;
    
    public UnitOfWork(BankingDbContext context)
    {
        _context = context;
    }
    
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

**That's it!** It's just a wrapper around `DbContext.SaveChangesAsync()`.

---

## Why Have a Wrapper?

### **Reason 1: Single Responsibility**

```csharp
// Good: All save logic in one place
await _unitOfWork.SaveChangesAsync();

// vs Bad: Need to know about DbContext everywhere
await _context.SaveChangesAsync();
```

### **Reason 2: Easy to Replace**

If you want to change how saving works (add logging, add retry logic, etc.):

```csharp
// Before:
public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
{
    await _context.SaveChangesAsync(cancellationToken);
}

// After: Add logging
public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Starting database save...");
    
    try
    {
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Save successful!");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Save failed!");
        throw;
    }
}

// All places using it automatically get logging!
```

### **Reason 3: Testing**

```csharp
// Easy to mock in tests
var mockUnitOfWork = new Mock<IUnitOfWork>();
mockUnitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(true);

var handler = new TransferMoneyCommandHandler(mockUnitOfWork);
```

### **Reason 4: Transaction Management**

```csharp
// Future: Could add transaction handling
public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
{
    using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

---

## How It Works: Step by Step

### **Step 1: Make Changes (In Memory)**

```csharp
var sourceAccount = await _accountRepository.GetByAccountNumberAsync("1234567890");
sourceAccount.Transfer(...);  ← Changes only in memory
                              ← NOT in database yet
```

**Database State:** Unchanged ❌

---

### **Step 2: Call UnitOfWork.SaveChangesAsync()**

```csharp
await _unitOfWork.SaveChangesAsync(cancellationToken);
```

**What happens inside UnitOfWork:**

```csharp
public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // This is the magic! DbContext tracks all changes
    // and saves them all together in ONE transaction
    await _context.SaveChangesAsync(cancellationToken);
}
```

**Database State:** All changes persisted ✅

---

### **Step 3: All Changes Saved Together**

```
Transaction starts:
  ✓ Account 1234567890 balance updated: ₦10,000 → ₦9,500
  ✓ Account 0987654321 balance updated: ₦5,000 → ₦5,500
  ✓ Transaction record created
  ✓ OutboxMessage created
Transaction commits:
  All changes persisted to database
  OR
  All changes rolled back (if any fails)
```

---

## Real Example: Money Transfer

```csharp
public class TransferMoneyCommandHandler : IRequestHandler<TransferMoneyCommand, Result>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<Result> Handle(TransferMoneyCommand request, CancellationToken cancellationToken)
    {
        // Step 1: Get accounts
        var sourceAccount = await _accountRepository.GetByAccountNumberAsync("1234567890");
        var destAccount = await _accountRepository.GetByAccountNumberAsync("0987654321");
        
        // Step 2: Make changes (in memory only)
        sourceAccount.Transfer(
            amount: new Money(500),
            destination: destAccount,
            reference: "REF123",
            description: "Payment"
        );
        
        // Step 3: Save ALL changes together
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        //     ↑
        //     This is the critical moment!
        //     All changes committed to database atomically
        
        // Step 4: Return success
        return Result.Success();
    }
}
```

---

## What Changes Get Saved?

When you call `_unitOfWork.SaveChangesAsync()`, it saves:

```
1. Modified Accounts
   ✅ sourceAccount.Balance = ₦9,500 (changed)
   ✅ destAccount.Balance = ₦5,500 (changed)

2. New Transactions
   ✅ Transaction record (new)

3. New OutboxMessages
   ✅ MoneyTransferredEvent (new)

4. Any other changes tracked by DbContext
   ✅ Customer updates (if any)
   ✅ etc.

All in ONE transaction!
```

---

## Failure Scenario

### **What If Database Connection Fails?**

```
await _unitOfWork.SaveChangesAsync(cancellationToken);
                    ↓
          Connection fails!
                    ↓
           Exception thrown
                    ↓
    NOTHING gets saved to database
    
    Database state remains unchanged:
    - sourceAccount.Balance still ₦10,000
    - destAccount.Balance still ₦5,000
    - No transaction record created
    - No outbox message created
    
    ✅ Consistency guaranteed!
```

---

## UnitOfWork vs Repositories

| Component | Purpose | What It Does |
|-----------|---------|--------------|
| **Repository** | Get/Add individual entities | Queries like "Get account by ID" |
| **UnitOfWork** | Save all changes together | Commits all changes atomically |

```
Repositories: Read/Write individual things
UnitOfWork: Save everything at once
```

---

## Timeline: Complete Picture

```
04:00:00  Transfer request arrives
           ↓
04:00:01  Handler gets accounts from repositories
           ✅ Data loaded from DB
           ↓
04:00:02  Handler calls Transfer()
           ✅ Changes made in memory only
           ✗ NOT in database yet
           ↓
04:00:03  Handler calls UnitOfWork.SaveChangesAsync()
           Transaction begins:
           ✓ All changes gathered by DbContext
           ✓ Sent to database
           ✓ Database applies all changes
           ✓ Transaction committed
           ✅ All changes persisted atomically
           ↓
04:00:04  Handler returns Result.Success()
           ✅ User gets response
```

---

## Why "Unit of Work"?

**"Unit of Work"** = One batch of work that should be:
- Either **all completed** ✅
- Or **all rolled back** ❌
- Never partially done ⚠️

```
Think of it as:
"Save this unit of work" = "Save this batch of related changes"

Not:
"Save this item"
"Save that item"
(multiple separate saves)
```

---

## Simple Summary

| Aspect | Details |
|--------|---------|
| **What** | A single save operation for all changes |
| **How** | Wraps `DbContext.SaveChangesAsync()` |
| **When** | Called once at the end of business logic |
| **Why** | Ensures consistency (all-or-nothing) |
| **Located At** | `CoreBankingTest.DAL/Data/UnitOfWork.cs` |

---

## Code Location

```
Interface: CoreBankingTest.CORE/Interfaces/IUnitOfWork.cs
Implementation: CoreBankingTest.DAL/Data/UnitOfWork.cs

Usage: Every command handler
```

---

## Key Takeaways

✅ **One Save Call**: All your changes saved together  
✅ **All or Nothing**: Either everything persists or nothing does  
✅ **Consistency**: No partial updates, no corrupted state  
✅ **Easy to Test**: Mock `IUnitOfWork` in tests  
✅ **Easy to Extend**: Add logging, transactions, retry logic, etc. in one place  

---

## Comparison: Before and After UnitOfWork

### **Before UnitOfWork (Messy)**

```csharp
await _accountRepository.SaveChangesAsync();
await _customerRepository.SaveChangesAsync();
await _transactionRepository.SaveChangesAsync();
await _outboxRepository.SaveChangesAsync();

// 4 different saves
// What if #3 fails after #1-2 succeed?
// Inconsistent state!
```

### **After UnitOfWork (Clean)**

```csharp
await _unitOfWork.SaveChangesAsync();

// 1 save for everything
// All-or-nothing guarantee
// Much cleaner!
```


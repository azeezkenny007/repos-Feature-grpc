# Value Objects - Complete Behavior & How They Work

## What Are Value Objects?

**Value Objects** are small, immutable objects that represent concepts from your domain. They wrap basic types (like `string`, `decimal`, `Guid`) to add type safety, validation, and behavior.

Think of them as **strongly-typed wrappers** that prevent mistakes and ensure data quality.

---

## Value Object 1: CustomerId

### What It Represents
A unique identifier for a customer. Wraps a `Guid`.

### Source Code

```csharp
public record CustomerId
{
    public Guid Value { get; init; }

    // EF Core needs a parameterless constructor
    private CustomerId() { }

    private CustomerId(Guid value)
    {
        Value = value;
    }

    public static CustomerId Create() => new(Guid.NewGuid());
    public static CustomerId Create(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
```

### Behavior Explanation

#### 1. **Record Type** (`public record CustomerId`)
```csharp
// Records in C# are immutable by default
// They automatically get:
// - Equality comparison (two CustomerId with same Guid = equal)
// - Hash code (for use in collections)
// - ToString() override
// - Deconstruction support

// Example:
var id1 = CustomerId.Create(new Guid("a1b2c3d4-..."));
var id2 = CustomerId.Create(new Guid("a1b2c3d4-..."));

id1 == id2  // TRUE - Same Guid means equal records
```

#### 2. **Guid Value Property**
```csharp
public Guid Value { get; init; }
//         ↑           ↑     ↑
//    Type      Property  init only (set once during creation)
```

- `{ get; init; }` means:
  - `get`: Can read the value anytime
  - `init`: Can ONLY set the value during object creation
  - CANNOT change after created (IMMUTABLE)

```csharp
// ✅ Valid - Setting during creation
var id = new CustomerId(Guid.NewGuid());

// ❌ Invalid - Cannot change after creation
id.Value = Guid.NewGuid();  // COMPILER ERROR!
```

#### 3. **Private Parameterless Constructor**
```csharp
private CustomerId() { }
```

- `private`: Only internal use (EF Core uses reflection)
- Parameterless: EF Core needs this for database materialization
- When EF Core reads from database, it calls this constructor

```csharp
// EF Core Database Read:
// Database: CustomerId = "a1b2c3d4-..."
// ↓
// EF Core calls: new CustomerId()  ← parameterless
// ↓
// Sets: Value = "a1b2c3d4-..."
```

#### 4. **Private Constructor with Guid Parameter**
```csharp
private CustomerId(Guid value)
{
    Value = value;
}
```

- `private`: Only internal use
- Takes a `Guid` and stores it in `Value`
- Called by the `Create()` factory methods

```csharp
// When you do:
CustomerId.Create(someGuid)
// ↓
// It calls: new(someGuid)  ← private constructor
// ↓
// Creates: CustomerId { Value = someGuid }
```

#### 5. **Create() Factory Methods**
```csharp
public static CustomerId Create() => new(Guid.NewGuid());
//                       ↑
//                   Factory method

public static CustomerId Create(Guid value) => new(value);
```

These are the **only way** to create CustomerId:

```csharp
// ✅ Valid ways:
var id1 = CustomerId.Create();           // New random Guid
var id2 = CustomerId.Create(existingGuid); // Use existing Guid

// ❌ Invalid:
var id3 = new CustomerId(guid);  // COMPILER ERROR! Constructor is private
```

#### 6. **ToString() Override**
```csharp
public override string ToString() => Value.ToString();
```

Converts to string representation:

```csharp
var id = CustomerId.Create(new Guid("a1b2c3d4-1234-5678-9abc-123456789abc"));

Console.WriteLine(id);  // Output: "a1b2c3d4-1234-5678-9abc-123456789abc"
Console.WriteLine(id.Value);  // Output: "a1b2c3d4-1234-5678-9abc-123456789abc"
```

### Real-World Usage

```csharp
// Creating customers
var customerId = CustomerId.Create();  // New customer ID

// Storing in database
var customer = new Customer 
{ 
    CustomerId = customerId,
    FirstName = "Alice"
};

// Querying by ID
Customer found = await _repo.GetByIdAsync(customerId);

// Two customers with same ID are equal
var aliceId1 = CustomerId.Create(guid1);
var aliceId2 = CustomerId.Create(guid1);
Assert.Equal(aliceId1, aliceId2);  // TRUE
```

### Benefits

✅ **Type Safety**: Can't pass AccountId where CustomerId expected
✅ **Immutable**: Can't accidentally change ID after creation
✅ **Unique**: Each GUID is globally unique
✅ **Serializable**: Easy to convert to/from string

---

## Value Object 2: AccountId

### What It Represents
A unique identifier for an account. Wraps a `Guid`.

### Source Code

```csharp
public record AccountId
{
    public Guid Value { get; init; }

    // EF Core needs a parameterless constructor
    private AccountId() { }

    private AccountId(Guid value)
    {
        Value = value;
    }

    public static AccountId Create() => new(Guid.NewGuid());
    public static AccountId Create(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
```

### Behavior
**Identical to CustomerId** (see above) - same pattern, different domain concept.

### Real-World Usage

```csharp
// Creating account
var accountId = AccountId.Create();

// Using in account
var account = new Account
{
    AccountId = accountId,
    AccountNumber = "1000000001"
};

// Type safety prevents mixing
CustomerId customerId = CustomerId.Create();
AccountId accountId = accountId.Create();

DoSomething(customerId);   // ✅ Correct
DoSomething(accountId);    // ❌ COMPILER ERROR - Wrong type!
```

---

## Value Object 3: TransactionId

### What It Represents
A unique identifier for a transaction. Wraps a `Guid`.

### Source Code

```csharp
public record TransactionId
{
    public Guid Value { get; init; }

    // EF Core needs a parameterless constructor
    private TransactionId() { }

    private TransactionId(Guid value)
    {
        Value = value;
    }

    public static TransactionId Create() => new(Guid.NewGuid());
    public static TransactionId Create(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
```

### Behavior
**Identical to CustomerId and AccountId** - same immutable GUID-based ID pattern.

---

## Value Object 4: AccountNumber

### What It Represents
A human-readable, 10-digit account number. Wraps a `string`.

### Source Code

```csharp
public record AccountNumber
{
    public string Value { get; }
    //              ↑
    //         No 'init' - just get (read-only)

    public AccountNumber(string value)
    {
        // ✅ VALIDATION 1: Not null and exactly 10 characters
        if (string.IsNullOrWhiteSpace(value) || value.Length != 10)
            throw new ArgumentException("Account number must be 10 digits");

        // ✅ VALIDATION 2: All characters must be digits
        if (!value.All(char.IsDigit))
            throw new ArgumentException("Account number must contain only digits");

        Value = value;
    }

    private AccountNumber() : this(string.Empty) { }
    
    public static AccountNumber Create(string value) => new(value);

    // ✅ IMPLICIT OPERATOR: Can use as string automatically
    public static implicit operator string(AccountNumber number) => number.Value;
    
    // ✅ EXPLICIT OPERATOR: Must cast to convert back
    public static explicit operator AccountNumber(string value) => new(value);

    public override string ToString() => Value;
}
```

### Behavior Explanation

#### 1. **Validation in Constructor**

```csharp
public AccountNumber(string value)
{
    // VALIDATION 1: Check length
    if (string.IsNullOrWhiteSpace(value) || value.Length != 10)
        throw new ArgumentException("Account number must be 10 digits");
    
    // ✅ Examples:
    new AccountNumber("1000000001");  // OK - 10 digits
    new AccountNumber("100000000");   // ERROR - 9 digits
    new AccountNumber("10000000001"); // ERROR - 11 digits
    new AccountNumber("");            // ERROR - empty
    new AccountNumber(null);          // ERROR - null
    
    // VALIDATION 2: Check all characters are digits
    if (!value.All(char.IsDigit))
        throw new ArgumentException("Account number must contain only digits");
    
    // ✅ Examples:
    new AccountNumber("1000000001");  // OK - all digits
    new AccountNumber("100000000A");  // ERROR - contains letter
    new AccountNumber("1000000-001"); // ERROR - contains hyphen
}
```

**KEY POINT**: Validation happens at creation time. You CANNOT create an invalid AccountNumber.

```csharp
// ✅ Valid
var acc1 = new AccountNumber("1000000001");

// ❌ Invalid - throws ArgumentException
try {
    var acc2 = new AccountNumber("invalid!");
} catch (ArgumentException ex) {
    Console.WriteLine(ex.Message);  // "Account number must contain only digits"
}
```

#### 2. **Read-Only Value Property**

```csharp
public string Value { get; }
//                      ↑
//                   Only 'get' - no 'init'
```

Unlike ID value objects, AccountNumber only has `get`, not `init`:

```csharp
// Set during construction
var number = new AccountNumber("1000000001");
Console.WriteLine(number.Value);  // "1000000001" ✅

// ❌ Cannot change afterward
number.Value = "1000000002";  // COMPILER ERROR!
```

#### 3. **Private Parameterless Constructor**

```csharp
private AccountNumber() : this(string.Empty) { }
//      ↑ private                         ↑
//  EF Core only           calls constructor with empty string
```

When EF Core materializes from database, it uses reflection:

```csharp
// During EF Core database read:
// Database: "1000000001"
// ↓
// Calls: new AccountNumber()  ← parameterless
// ↓
// Which calls: this(string.Empty)  ← chained constructor
// ↓
// Which sets: Value = ""
// ↓
// Then EF Core sets Value via reflection to "1000000001"
```

#### 4. **Factory Method**

```csharp
public static AccountNumber Create(string value) => new(value);
```

Alternative way to create:

```csharp
// Two ways to create:
var acc1 = new AccountNumber("1000000001");
var acc2 = AccountNumber.Create("1000000001");

// Both do the same thing
```

#### 5. **Implicit Operator** (String Conversion)

```csharp
public static implicit operator string(AccountNumber number) => number.Value;
```

Automatically converts AccountNumber to string:

```csharp
var number = new AccountNumber("1000000001");

// ✅ Implicit - automatic conversion
string str = number;  
Console.WriteLine(str);  // "1000000001"

// This is equivalent to:
string str2 = number.Value;
```

**Why implicit?** You almost always want to use the account number as a string:

```csharp
// Friendly usage:
var number = new AccountNumber("1000000001");
Console.WriteLine($"Account: {number}");
// Instead of: Console.WriteLine($"Account: {number.Value}");

// In LINQ queries:
accounts.Where(a => a.AccountNumber == "1000000001")  // Works naturally
```

#### 6. **Explicit Operator** (Cast Back to AccountNumber)

```csharp
public static explicit operator AccountNumber(string value) => new(value);
```

Forces intentional conversion back to AccountNumber:

```csharp
string str = "1000000001";

// ✅ Explicit - must cast explicitly
AccountNumber number = (AccountNumber)str;
// Conversion is intentional - validates during construction

// This is equivalent to:
AccountNumber number2 = new AccountNumber(str);
```

**Why explicit?** It triggers validation. You must THINK about it.

```csharp
// Explicit reminds you validation happens:
AccountNumber number = (AccountNumber)"invalid!";
// ↑ If you see this cast, you know validation is running
// And might throw ArgumentException

// But implicit is natural:
string str = number;  // Obvious this won't fail
```

#### 7. **ToString() Override**

```csharp
public override string ToString() => Value;
```

```csharp
var number = new AccountNumber("1000000001");
Console.WriteLine(number);  // "1000000001"
```

### Comparison: AccountNumber vs. ID Value Objects

| Aspect | CustomerId/AccountId/TransactionId | AccountNumber |
|--------|----------------------------------|----------------|
| Wraps | `Guid` | `string` |
| Property | `{ get; init; }` | `{ get; }` |
| Validation | None | Length + digits only |
| Implicit Operator | No | Yes (to string) |
| Explicit Operator | No | Yes (from string) |
| Unique | Yes (GUID) | Yes (UNIQUE constraint) |
| Human-Readable | No (GUID) | Yes (10 digits) |

### Real-World Usage

```csharp
// Creating account with number
var accountNumber = new AccountNumber("1000000001");

var account = new Account
{
    AccountNumber = accountNumber,
    CustomerId = customerId
};

// Using as string naturally
Console.WriteLine($"Account: {accountNumber}");  // "Account: 1000000001"

// Validation prevents invalid accounts
try {
    var invalid = new AccountNumber("abc");  // ERROR - not 10 digits
} catch (ArgumentException ex) {
    Console.WriteLine(ex.Message);  // "Account number must be 10 digits"
}

// Type safety still applies
var str = (string)accountNumber;  // ✅ Explicit cast
var num = new AccountNumber(str);  // ✅ Creates validated copy
```

---

## Value Object 5: Money

### What It Represents
An amount of money in a specific currency. Wraps `decimal` (amount) and `string` (currency).

### Source Code

```csharp
public record Money
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "NGN";

    // EF Core needs this for materialization
    private Money() { }

    public Money(decimal amount, string currency = "NGN")
    {
        // ✅ VALIDATION: Amount cannot be negative
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative");

        Amount = amount;
        Currency = currency;
    }

    // ✅ OPERATOR: Add two Money values
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Cannot add different currencies");

        return new Money(a.Amount + b.Amount, a.Currency);
    }

    // ✅ OPERATOR: Subtract two Money values
    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Cannot subtract different currencies");

        return new Money(a.Amount - b.Amount, a.Currency);
    }
}
```

### Behavior Explanation

#### 1. **Properties with Validation**

```csharp
public decimal Amount { get; private set; }
//               ↑ Amount (e.g., 500)
// private set - only settable internally

public string Currency { get; private set; } = "NGN";
//                      ↑ Currency code
// Default: "NGN" (Nigerian Naira)
```

```csharp
// Creating Money
var money = new Money(500, "NGN");
Console.WriteLine(money.Amount);    // 500
Console.WriteLine(money.Currency);  // "NGN"

// Default currency
var money2 = new Money(100);  // Currency defaults to "NGN"
```

#### 2. **Validation: No Negative Amounts**

```csharp
public Money(decimal amount, string currency = "NGN")
{
    if (amount < 0)
        throw new ArgumentException("Money amount cannot be negative");
    
    Amount = amount;
    Currency = currency;
}
```

```csharp
// ✅ Valid
var money = new Money(500, "NGN");    // Positive amount
var money2 = new Money(0, "NGN");     // Zero is OK

// ❌ Invalid
var money3 = new Money(-100, "NGN");  // ERROR - negative not allowed
```

#### 3. **Addition Operator with Currency Check**

```csharp
public static Money operator +(Money a, Money b)
{
    if (a.Currency != b.Currency)
        throw new InvalidOperationException("Cannot add different currencies");

    return new Money(a.Amount + b.Amount, a.Currency);
}
```

This allows natural addition with validation:

```csharp
// ✅ Valid - same currency
var money1 = new Money(500, "NGN");
var money2 = new Money(300, "NGN");
var result = money1 + money2;
// Result: Money(800, "NGN")

// ❌ Invalid - different currencies
var usd = new Money(100, "USD");
var ngn = new Money(100, "NGN");
var bad = usd + ngn;  // ERROR: "Cannot add different currencies"
```

**KEY BEHAVIOR**: You CANNOT accidentally add different currencies!

```csharp
// Real world example: Account transfer
public Result Transfer(Money amount, Account destination, string reference, string description)
{
    // This won't compile if amounts have different currencies:
    Balance = Balance - amount;  // Must be same currency or error
    destination.Balance = destination.Balance + amount;  // Same validation
    
    // Try to add different currency → Exception thrown
}
```

#### 4. **Subtraction Operator with Currency Check**

```csharp
public static Money operator -(Money a, Money b)
{
    if (a.Currency != b.Currency)
        throw new InvalidOperationException("Cannot subtract different currencies");

    return new Money(a.Amount - b.Amount, a.Currency);
}
```

Same as addition - prevents currency mixing:

```csharp
// ✅ Valid
var money1 = new Money(500, "NGN");
var money2 = new Money(100, "NGN");
var result = money1 - money2;
// Result: Money(400, "NGN")

// ❌ Invalid
var usd = new Money(100, "USD");
var ngn = new Money(100, "NGN");
var bad = usd - ngn;  // ERROR: "Cannot subtract different currencies"
```

#### 5. **Private Parameterless Constructor**

```csharp
private Money() { }
```

EF Core uses this for database materialization.

#### 6. **Record Type**

```csharp
public record Money
{
    // Records automatically implement:
    // - Equality (two Money with same amount/currency = equal)
    // - Hash code (for collections)
    // - ToString()
}
```

```csharp
var money1 = new Money(500, "NGN");
var money2 = new Money(500, "NGN");

money1 == money2  // TRUE - same amount and currency
money1.Equals(money2)  // TRUE

// Hash code is consistent
var dict = new Dictionary<Money, string>();
dict[money1] = "First";
dict[money2] = "Second";  // Same key - overwrites
Assert.Single(dict);  // Only 1 entry because same money = same hash
```

### Real-World Usage

```csharp
// Creating account balance
var account = new Account
{
    Balance = new Money(500, "NGN")
};

// Deposit operation
var depositAmount = new Money(100, "NGN");
account.Balance = account.Balance + depositAmount;
// Result: Money(600, "NGN")

// Transfer operation
public Result Debit(Money amount, string description, string reference)
{
    if (Balance.Amount < amount.Amount)
        return Result.Failure("Insufficient funds");
    
    Balance = Balance - amount;  // Natural subtraction
    return Result.Success();
}

// Type-safe currency handling
try {
    var transfer = new Money(100, "USD");  // Want to transfer USD
    account.Balance = account.Balance - transfer;  // ERROR if account is NGN!
} catch (InvalidOperationException ex) {
    Console.WriteLine(ex.Message);  // "Cannot subtract different currencies"
}
```

---

## Summary: Value Object Behaviors

| Value Object | Type | Validation | Key Feature |
|--------------|------|-----------|-------------|
| **CustomerId** | GUID | None | Type-safe customer ID |
| **AccountId** | GUID | None | Type-safe account ID |
| **TransactionId** | GUID | None | Type-safe transaction ID |
| **AccountNumber** | String | 10 digits only | Human-readable + type-safe |
| **Money** | Decimal + String | No negatives, currency match | Prevents currency mixing |

## Why Value Objects Matter

### ❌ Without Value Objects
```csharp
public void Transfer(decimal amount, string sourceCurrency, 
                     decimal destAmount, string destCurrency)
{
    // Confusing parameters
    // No validation at compile time
    // Easy to mix up currencies
    // Transfer(-100, "NGN", ...)  // Oops! Negative allowed
}
```

### ✅ With Value Objects
```csharp
public Result Transfer(Money amount, Account destination, 
                       string reference, string description)
{
    // Clear intent - amount is Money (amount + currency)
    // Validation enforced - can't mix currencies
    // Transfer(new Money(-100), ...)  // ERROR at construction
    // Type safety - can't pass CustomerId as AccountId
}
```

---

## Key Takeaways

1. **Immutability**: Once created, value objects cannot change
2. **Validation**: Invalid values are rejected at construction
3. **Type Safety**: Can't mix CustomerId with AccountId
4. **Domain Logic**: Money operators enforce currency consistency
5. **Operators**: Natural syntax (+ and -) for Money operations
6. **Implicit/Explicit Conversion**: AccountNumber seamlessly converts to/from string
7. **Records**: Automatic equality and hashing for value objects


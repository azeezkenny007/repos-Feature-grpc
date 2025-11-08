# gRPC in Your Banking System - Complete Explanation

## What is gRPC?

**gRPC** stands for "**g**RPC **R**emote **P**rocedure **C**all". It's a modern, high-performance framework for communication between services.

### Traditional REST API vs gRPC

#### REST API
```
Client sends: HTTP POST /api/accounts/transfer
              JSON body: {"from": "123", "to": "456", "amount": 100}
              ↓
Server:       Parses JSON, processes, sends response
              ↓
Client:       Receives JSON response
              ↓
Result:       Human-readable but SLOW, lots of data overhead
```

#### gRPC
```
Client sends: protobuf (binary format): {from, to, amount}
              ↓
Server:       Fast binary parsing, processes
              ↓
Client:       Receives protobuf response
              ↓
Result:       MUCH FASTER, tiny payloads, HTTP/2 streaming
```

### Key Differences

| Feature | REST | gRPC |
|---------|------|------|
| **Protocol** | HTTP/1.1 | HTTP/2 |
| **Format** | JSON (text, big) | Protobuf (binary, small) |
| **Speed** | Slower | 7-10x faster |
| **Streaming** | Polling only | Native streaming |
| **Use Case** | Web browsers, public APIs | Service-to-service, real-time |

---

## How gRPC Works in Your Code

### Step 1: Protocol Buffer Definitions (.proto files)

Protocol Buffers are like "contracts" that define the messages your services can exchange.

#### File: `account.proto`

```protobuf
service AccountService {
    rpc GetAccount (GetAccountRequest) returns (AccountResponse);
    rpc CreateAccount (CreateAccountRequest) returns (CreateAccountResponse);
    rpc TransferMoney (TransferMoneyRequest) returns (TransferMoneyResponse);
    rpc GetTransactionHistory (TransactionHistoryRequest) returns (stream TransactionResponse);
}
```

**Translation:**
- `service AccountService` = Define a gRPC service
- `rpc GetAccount` = Define a method called GetAccount
- `GetAccountRequest` = Client sends this message
- `AccountResponse` = Server sends back this message
- `stream TransactionResponse` = Server streams multiple responses (see below)

#### Message Definitions

```protobuf
message GetAccountRequest {
    string account_number = 1;
}

message AccountResponse {
    string account_number = 1;
    string account_type = 2;
    double balance = 3;
    string currency = 4;
    string customer_name = 5;
    google.protobuf.Timestamp date_opened = 6;
    bool is_active = 7;
}
```

**Meaning:**
- `message` = Define a data structure
- `string account_number = 1` = Field with number (1,2,3... for binary encoding)
- `double balance = 3` = Floating point number
- `google.protobuf.Timestamp` = Special type for timestamps

### Step 2: Compile Proto Files to C# Code

When you build, the gRPC tools automatically generate C# classes:

```csharp
// Auto-generated from account.proto
public partial class AccountResponse
{
    public string AccountNumber { get; set; }
    public string AccountType { get; set; }
    public double Balance { get; set; }
    // ... etc
}

// Auto-generated service base class
public abstract class AccountService
{
    public virtual Task<AccountResponse> GetAccount(GetAccountRequest request, ServerCallContext context)
    {
        // You implement this
    }
}
```

### Step 3: Implement gRPC Services

In your code, you inherit from the generated base class and implement the methods:

#### AccountGrpcService.cs

```csharp
public class AccountGrpcService : AccountService.AccountServiceBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;
    private readonly ILogger<AccountGrpcService> _logger;

    public override async Task<AccountResponse> GetAccount(GetAccountRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC GetAccount called for {AccountNumber}", request.AccountNumber);

        // Validate input
        if (string.IsNullOrWhiteSpace(request.AccountNumber))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Account number is required"));

        try
        {
            // Create domain value object
            var accountNumber = AccountNumber.Create(request.AccountNumber);
            
            // Send query through MediatR pipeline
            var query = new GetAccountDetailsQuery { AccountNumber = accountNumber };
            var result = await _mediator.Send(query);

            if (result.IsSuccess)
            {
                // Map domain model to gRPC response
                return _mapper.Map<AccountResponse>(result.Data);
            }
            else
            {
                throw new RpcException(new Status(StatusCode.NotFound, string.Join("; ", result.Errors)));
            }
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            _logger.LogError(ex, "Error retrieving account {AccountNumber}", request.AccountNumber);
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
        }
    }
}
```

**Key Parts:**

1. **Inherits from generated base**: `AccountService.AccountServiceBase`
2. **Takes gRPC request**: `GetAccountRequest request`
3. **Uses DDD layer**: Converts to domain objects (`AccountNumber.Create(...)`)
4. **Uses CQRS**: Sends through MediatR (`_mediator.Send(query)`)
5. **Maps response**: Converts domain result to gRPC response (`_mapper.Map<AccountResponse>`)
6. **Error handling**: Throws `RpcException` for gRPC-specific errors

---

## gRPC Communication Types

Your system implements 4 types of gRPC communication:

### Type 1: Unary RPC (Request-Response)

```protobuf
rpc GetAccount (GetAccountRequest) returns (AccountResponse);
```

**Flow:**
```
Client                                    Server
  │                                         │
  ├─ Send GetAccountRequest ───────────────>│
  │                                         │
  │                          Process request│
  │                                         │
  │<──────────── Send AccountResponse ──────┤
  │                                         │
  Done                                     Done
```

**Example:**
```csharp
// Client code (pseudo)
var client = new AccountService.AccountServiceClient(channel);
var request = new GetAccountRequest { AccountNumber = "123456789" };
var response = await client.GetAccountAsync(request);
// Use response.Balance, response.CustomerName, etc.
```

### Type 2: Server Streaming RPC

```protobuf
rpc GetTransactionHistory (TransactionHistoryRequest) 
    returns (stream TransactionResponse);
```

**Flow:**
```
Client                                    Server
  │                                         │
  ├─ Send Request ────────────────────────>│
  │                                         │
  │<───── Send Transaction 1 ──────────────┤
  │<───── Send Transaction 2 ──────────────┤
  │<───── Send Transaction 3 ──────────────┤
  │<───── Send Transaction 4 ──────────────┤
  │<───── Send "Stream Complete" ──────────┤
  │                                         │
  Done receiving                           Done sending
```

**Implementation in EnhancedAccountGrpcService:**

```csharp
public override async Task StreamTransactions(StreamTransactionsRequest request,
    IServerStreamWriter<TransactionResponse> responseStream, ServerCallContext context)
{
    _logger.LogInformation("Starting transaction stream for account {AccountNumber}", 
        request.AccountNumber);

    var accountNumber = AccountNumber.Create(request.AccountNumber);

    try
    {
        // Get initial batch of recent transactions
        var historyQuery = new GetTransactionHistoryQuery
        {
            AccountNumber = accountNumber,
            PageSize = request.InitialBatchSize
        };

        var historyResult = await _mediator.Send(historyQuery, context.CancellationToken);

        if (historyResult.IsSuccess)
        {
            // Send each transaction one by one
            foreach (var transaction in historyResult.Data!.Transactions)
            {
                if (context.CancellationToken.IsCancellationRequested) 
                    break;

                // Stream response to client
                await responseStream.WriteAsync(
                    _mapper.Map<global::Corebanking.TransactionResponse>(transaction),
                    context.CancellationToken
                );
                
                // Stagger sends (don't flood client)
                await Task.Delay(50, context.CancellationToken);
            }
        }

        // Real-time updates simulation
        while (!context.CancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);

                // Send new transactions as they arrive
                if (DateTime.UtcNow.Second % 15 == 0)
                {
                    var simulatedTransaction = new global::Corebanking.TransactionResponse
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        Type = "Deposit",
                        Amount = new Random().Next(100, 500),
                        Currency = "USD",
                        Description = "Real-time transaction",
                        Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
                    };

                    await responseStream.WriteAsync(simulatedTransaction, context.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogError(ex, "Error in transaction stream for account {AccountNumber}", 
            request.AccountNumber);
        throw new RpcException(new Status(StatusCode.Internal, "Streaming error occurred"));
    }

    _logger.LogInformation("Transaction stream ended for account {AccountNumber}", 
        request.AccountNumber);
}
```

**Use Case:** Downloading large lists (transactions, history) efficiently without loading everything at once.

### Type 3: Client Streaming RPC

```protobuf
rpc BatchTransfer (stream TransferMoneyRequest) 
    returns (BatchTransferResponse);
```

**Flow:**
```
Client                                    Server
  │                                         │
  ├─ Send Transfer 1 ────────────────────>│
  ├─ Send Transfer 2 ────────────────────>│
  ├─ Send Transfer 3 ────────────────────>│
  ├─ Send "No More" ─────────────────────>│
  │                                         │
  │                          Process all    │
  │                          transfers      │
  │<──── Send BatchTransferResponse ───────┤
  │      (Total: 3, Successful: 2, Failed: 1)
  │                                         │
  Done sending                             Done
```

**Implementation:**

```csharp
public override async Task<BatchTransferResponse> BatchTransfer(
    IAsyncStreamReader<TransferMoneyRequest> requestStream, ServerCallContext context)
{
    _logger.LogInformation("Starting batch transfer processing");

    var results = new List<BatchTransferResult>();
    var successfulTransfers = 0;
    var failedTransfers = 0;

    try
    {
        // Read all incoming transfer requests from client
        await foreach (var transferRequest in requestStream.ReadAllAsync(context.CancellationToken))
        {
            try
            {
                var command = new TransferMoneyCommand
                {
                    SourceAccountNumber = AccountNumber.Create(transferRequest.SourceAccountNumber),
                    DestinationAccountNumber = AccountNumber.Create(transferRequest.DestinationAccountNumber),
                    Amount = new Money((decimal)transferRequest.Amount, transferRequest.Currency),
                    Reference = transferRequest.Reference,
                    Description = transferRequest.Description
                };

                // Execute each transfer through MediatR
                var result = await _mediator.Send(command, context.CancellationToken);

                results.Add(new BatchTransferResult
                {
                    Reference = transferRequest.Reference,
                    Success = result.IsSuccess,
                    Message = result.IsSuccess ? "Transfer completed" : string.Join("; ", result.Errors)
                });

                if (result.IsSuccess) 
                    successfulTransfers++;
                else 
                    failedTransfers++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch transfer");
                results.Add(new BatchTransferResult
                {
                    Reference = transferRequest.Reference,
                    Success = false,
                    Message = "Processing error"
                });
                failedTransfers++;
            }
        }

        // Send final summary
        return new BatchTransferResponse
        {
            TotalProcessed = results.Count,
            Successful = successfulTransfers,
            Failed = failedTransfers
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in batch transfer processing");
        throw new RpcException(new Status(StatusCode.Internal, "Batch processing failed"));
    }
}
```

**Use Case:** Upload multiple items efficiently (batch transfers, bulk operations).

### Type 4: Bidirectional Streaming RPC

```protobuf
rpc LiveTrading (stream TradingOrder) 
    returns (stream TradingExecution);
```

**Flow:**
```
Client                                    Server
  │                                         │
  ├─ Send Order 1 ─────────────────────────>│
  │                          Process Order 1 │
  │<─────── Send Execution 1 ───────────────┤
  ├─ Send Order 2 ─────────────────────────>│
  │                          Process Order 2 │
  │<─────── Send Market Data Update ────────┤
  ├─ Send Order 3 ─────────────────────────>│
  │<─────── Send Execution 2 ───────────────┤
  │<─────── Send Market Data Update ────────┤
  │                          Process Order 3 │
  │<─────── Send Execution 3 ───────────────┤
  │                                         │
  Done                                     Done
```

**Implementation:**

```csharp
public override async Task LiveTrading(
    IAsyncStreamReader<TradingOrder> requestStream,
    IServerStreamWriter<TradingExecution> responseStream, 
    ServerCallContext context)
{
    var sessionId = Guid.NewGuid().ToString();
    _logger.LogInformation("Starting trading session {SessionId}", sessionId);

    try
    {
        // Task 1: Read incoming orders from client
        var readTask = Task.Run(async () =>
        {
            await foreach (var order in requestStream.ReadAllAsync(context.CancellationToken))
            {
                _logger.LogInformation("Received trading order: {OrderId} for {Symbol}", 
                    order.OrderId, order.Symbol);

                // Process order
                await ProcessTradingOrder(order, responseStream, context.CancellationToken);
            }
        });

        // Task 2: Send market data updates (independent of incoming orders)
        var marketDataTask = Task.Run(async () =>
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    var marketUpdate = GenerateMarketDataUpdate();
                    
                    // Send market update
                    await responseStream.WriteAsync(marketUpdate, context.CancellationToken);
                    
                    // Update every 1 second
                    await Task.Delay(1000, context.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        // Wait for either task to complete
        await Task.WhenAny(readTask, marketDataTask);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogError(ex, "Error in trading session {SessionId}", sessionId);
        throw new RpcException(new Status(StatusCode.Internal, "Trading session error"));
    }
    finally
    {
        _logger.LogInformation("Ended trading session {SessionId}", sessionId);
    }
}
```

**Use Case:** Real-time trading (client sends orders, server sends fills + market data simultaneously).

---

## Enhanced Account Service - Advanced Streaming

Your `enhanced_account.proto` defines more sophisticated streaming patterns:

```protobuf
service EnhancedAccountService {
  // Server streaming
  rpc StreamTransactions (StreamTransactionsRequest) 
      returns (stream TransactionResponse);

  // Client streaming
  rpc BatchTransfer (stream TransferMoneyRequest) 
      returns (BatchTransferResponse);

  // Bidirectional streaming
  rpc LiveTrading (stream TradingOrder) 
      returns (stream TradingExecution);
}
```

This service provides:
1. **Real-time transaction monitoring** (server pushes transactions as they happen)
2. **Batch operations** (client sends multiple transfers)
3. **Live trading** (client and server exchange orders/executions simultaneously)

---

## Configuration: How gRPC is Set Up

### Program.cs Configuration

```csharp
// Step 1: Add gRPC service
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});
builder.Services.AddGrpcReflection();

// Step 2: Setup dual-port Kestrel server
builder.WebHost.ConfigureKestrel(options =>
{
    // Port 5037: HTTP/1.1 for REST API + Swagger
    options.ListenLocalhost(5037, o => o.Protocols = HttpProtocols.Http1);

    // Port 7288: HTTP/2 for gRPC
    options.ListenLocalhost(7288, o =>
    {
        o.UseHttps();
        o.Protocols = HttpProtocols.Http2;
    });
});

// Step 3: Map gRPC services to routes
app.MapGrpcService<AccountGrpcService>();
app.MapGrpcService<EnhancedAccountGrpcService>();

// Also map gRPC reflection (for debugging with grpcurl)
if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}
```

**What This Does:**

1. **Registers gRPC**: `AddGrpc()` - prepares framework
2. **Reflection enabled**: Clients can discover available methods
3. **Dual ports**: 
   - REST on port 5037 (HTTP/1.1)
   - gRPC on port 7288 (HTTP/2)
4. **Service mapping**: Registers `AccountGrpcService` and `EnhancedAccountGrpcService`

---

## Data Flow: gRPC Request Through Your System

```
┌──────────────────────────────┐
│  gRPC Client                 │
│  (External service/tool)     │
└──────────────┬───────────────┘
               │
               │ Binary protobuf message
               │ HTTP/2 over TLS
               │
               ▼
┌──────────────────────────────┐
│  Kestrel (Port 7288)         │
│  HTTP/2 Protocol Handler     │
└──────────────┬───────────────┘
               │
               │ Deserialize to C# object
               │
               ▼
┌──────────────────────────────┐
│  AccountGrpcService          │
│  (Your implementation)       │
└──────────────┬───────────────┘
               │
               │ Convert to domain objects
               │
               ▼
┌──────────────────────────────┐
│  MediatR Pipeline            │
│  - Validation                │
│  - Logging                   │
│  - Domain Events             │
└──────────────┬───────────────┘
               │
               │ Execute command/query
               │
               ▼
┌──────────────────────────────┐
│  Application Layer           │
│  (Commands/Queries)          │
└──────────────┬───────────────┘
               │
               ▼
┌──────────────────────────────┐
│  Domain Layer                │
│  (Business logic)            │
└──────────────┬───────────────┘
               │
               ▼
┌──────────────────────────────┐
│  Infrastructure/Database     │
│  (Data persistence)          │
└──────────────┬───────────────┘
               │
               │ Result object
               │
               ▼
┌──────────────────────────────┐
│  Auto Mapper                 │
│  Convert to gRPC response    │
└──────────────┬───────────────┘
               │
               │ Serialize to protobuf
               │ HTTP/2
               │
               ▼
┌──────────────────────────────┐
│  gRPC Client                 │
│  (Receives response)         │
└──────────────────────────────┘
```

---

## Comparison: REST vs gRPC in Your System

### REST Example (HTTP/1.1)

```http
POST /api/accounts/transfer HTTP/1.1
Host: localhost:5037
Content-Type: application/json
Content-Length: 150

{
  "sourceAccountNumber": "1000000001",
  "destinationAccountNumber": "1000000002",
  "amount": 100,
  "currency": "NGN",
  "reference": "TRANSFER-001",
  "description": "Account to account"
}

Response (HTTP/1.1, 200 OK):
{
  "success": true,
  "message": "Transfer successful",
  "transactionId": "guid-123",
  "transferDate": "2025-11-08T16:22:08Z"
}
```

**Size:** ~400 bytes (text)

### gRPC Example (HTTP/2)

```
Client sends binary protobuf to localhost:7288 over HTTP/2:
TransferMoneyRequest {
  source_account_number: "1000000001"
  destination_account_number: "1000000002"
  amount: 100
  currency: "NGN"
  reference: "TRANSFER-001"
  description: "Account to account"
}

Server sends binary protobuf response:
TransferMoneyResponse {
  success: true
  message: "Transfer successful"
  transaction_id: "guid-123"
  transfer_date: {timestamp}
}
```

**Size:** ~60 bytes (binary, compressed)

**Benefit:** 6.67x smaller payload!

---

## How to Use gRPC Client

### With grpcurl (debugging tool)

```bash
# List available services
grpcurl -plaintext localhost:7288 list

# Call GetAccount
grpcurl -plaintext -d '{"account_number": "1000000001"}' \
  localhost:7288 corebanking.AccountService/GetAccount

# Stream transactions
grpcurl -plaintext -d '{"account_number": "1000000001", "initial_batch_size": 10}' \
  localhost:7288 corebanking.EnhancedAccountService/StreamTransactions
```

### With .NET Client Code

```csharp
var channel = GrpcChannel.ForAddress("https://localhost:7288");
var client = new AccountService.AccountServiceClient(channel);

var request = new GetAccountRequest { AccountNumber = "1000000001" };
var response = await client.GetAccountAsync(request);

Console.WriteLine($"Balance: {response.Balance}");
Console.WriteLine($"Customer: {response.CustomerName}");
```

---

## Key Takeaways

1. **gRPC = High Performance**: Binary format (protobuf) is 5-10x faster than JSON
2. **Streaming**: Native support for streaming (server, client, bidirectional)
3. **HTTP/2**: Multiplexing - multiple requests over single connection
4. **Proto Contracts**: Define message structures in `.proto` files
5. **Auto-Generated Code**: `.proto` files compile to C# classes
6. **Service Implementation**: Inherit from generated base class and implement methods
7. **Dual Transport**: Your system runs REST (port 5037) and gRPC (port 7288) simultaneously
8. **MediatR Integration**: gRPC services use same CQRS pipeline as REST API
9. **Streaming Types**: Unary, Server Streaming, Client Streaming, Bidirectional
10. **Real-Time**: Ideal for real-time applications like trading, monitoring, notifications


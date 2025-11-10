using CoreBanking.API.gRPC.Mappings;
using CoreBankingTest.API.Extensions;
using CoreBankingTest.API.gRPC.Interceptors;
using CoreBankingTest.API.gRPC.Services;
using CoreBankingTest.API.Hubs;
using CoreBankingTest.API.Hubs.EventHandlers;
using CoreBankingTest.API.Hubs.Management;
using CoreBankingTest.API.Mappings;
using CoreBankingTest.API.Middleware;
using CoreBankingTest.API.Services;
using CoreBankingTest.APP.Accounts.Commands.CreateAccount;
using CoreBankingTest.APP.Accounts.Commands.TransferMoney;
using CoreBankingTest.APP.Accounts.EventHandlers;
using CoreBankingTest.APP.Common.Behaviors;
using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Mappings;
using CoreBankingTest.APP.Common.Models;
using CoreBankingTest.APP.Customers.Commands.CreateCustomer;
using CoreBankingTest.APP.External.HttpClients;
using CoreBankingTest.APP.External.Interfaces;
using CoreBankingTest.CORE.Events;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.DAL.Data;
using CoreBankingTest.DAL.External.Resilience;
using CoreBankingTest.DAL.Repositories;
using CoreBankingTest.DAL.ServiceBus;
using CoreBankingTest.DAL.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using System.Reflection;

//DELETE FROM dbo.Customers
//WHERE FirstName = 'string';

namespace CoreBanking.API
{
    
        public class Program
        {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ------------------- SERVICES -------------------

            builder.Services.AddDbContext<BankingDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Core dependencies
            builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
            builder.Services.AddScoped<IAccountRepository, AccountRepository>();
            builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

            // Event handlers
            builder.Services.AddTransient<INotificationHandler<AccountCreatedEvent>, AccountCreatedEventHandler>();
            builder.Services.AddTransient<INotificationHandler<MoneyTransferredEvent>, MoneyTransferedEventHandler>();
            builder.Services.AddTransient<INotificationHandler<InsufficientFundsEvent>, InsufficientFundsEventHandler>();
            builder.Services.AddTransient<INotificationHandler<MoneyTransferredEvent>, RealTimeNotificationEventHandler>();

            // Pipeline behaviors
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainEventsBehavior<,>));
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

            // gRPC + Reflection
            builder.Services.AddGrpc(options =>
            {
                options.EnableDetailedErrors = true;
            });
            builder.Services.AddGrpcReflection();

            // SignalR
            builder.Services.AddSignalR();

            builder.Services.Configure<ResilienceOptions>(builder.Configuration.GetSection("Resilience"));

            // Add advanced Polly policies
            builder.Services.AddSingleton<AdvancedPollyPolicies>();

            // Add simulated external services
            builder.Services.AddSingleton<ISimulatedCreditScoringService, SimulatedCreditScoringService>();

            // Add Azure Service Bus (simulated for now - will configure properly in subscequent class)


            //builder.Services.AddSingleton<IServiceBusSender>(provider =>
            //{
            //    var logger = provider.GetRequiredService<ILogger<ServiceBusSender>>();
            //    // For today, we'll use a mock. Tomorrow we'll add real Azure Service Bus connection
            //    return new MockServiceBusSender(logger);
            //});

            builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
            builder.Services.AddScoped<IDomainEventDispatcher, ServiceBusEventDispatcher>();

            // MediatR setup
            builder.Services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(CreateAccountCommand).Assembly);
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(DomainEventsBehavior<,>));
            });

            // Validation and mapping
            builder.Services.AddValidatorsFromAssembly(typeof(CreateAccountCommandValidator).Assembly);
            builder.Services.AddAutoMapper(cfg => { }, typeof(AccountProfile).Assembly);
            builder.Services.AddAutoMapper(cfg => { }, typeof(AccountGrpcProfile).Assembly);

            // Outbox
            builder.Services.AddScoped<IOutboxMessageProcessor, OutboxMessageProcessor>();
            builder.Services.AddHostedService<OutboxBackgroundService>();

            // Controllers + Swagger
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "CoreBanking API",
                    Version = "v1",
                    Description = "A modern banking API built with Clean Architecture, DDD, and CQRS"
                });
            });

            // Kestrel multi-protocol setup
            builder.WebHost.ConfigureKestrel(options =>
            {
                // HTTP/1.1 for REST, Swagger, etc.
                options.ListenLocalhost(5037, o => o.Protocols = HttpProtocols.Http1);

                // HTTP/2 for gRPC
                options.ListenLocalhost(7288, o =>
                {
                    o.UseHttps();
                    o.Protocols = HttpProtocols.Http2;
                });
            });
            // Add SignalR services
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                options.MaximumReceiveMessageSize = 64 * 1024; // 64KB
            })
            .AddMessagePackProtocol();

            // Add connection state management
            builder.Services.AddSingleton<ConnectionStateService>();

            // Add hosted services
            builder.Services.AddHostedService<TransactionBroadcastService>();

            // Add external HTTP clients with resilience
            builder.Services.AddExternalHttpClients(builder.Configuration);

            // Add resilience services
            builder.Services.AddSingleton<IResilientHttpClientService, ResilientHttpClientService>();

            // Register Polly policies
            builder.Services.AddSingleton(HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var logger = ContextExtensions.GetLogger(context);
                        logger?.LogWarning("Retry {RetryCount} after {Delay}ms",
                            retryCount, timespan.TotalMilliseconds);
                    }));

            builder.Services.AddHttpClient<ICreditScoringServiceClient, CreditScoringServiceClient>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["CreditScoringApi:BaseUrl"] ?? "https://api.example.com");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });


            var app = builder.Build();

            // ------------------- PIPELINE -------------------

            app.UseHttpsRedirection();

            app.UseStaticFiles(); // Enables wwwroot

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger(options => options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0);
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CoreBanking API v1");
                    c.RoutePrefix = "swagger";
                });

                app.MapGrpcReflectionService();
            }

            app.UseAuthorization();

            app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

            // ------------------- ROUTING -------------------

            // REST API
            app.MapControllers();

            // gRPC endpoints
            app.MapGrpcService<AccountGrpcService>();
            app.MapGrpcService<EnhancedAccountGrpcService>();

            // SignalR hub
            app.MapHub<EnhancedNotificationHub>("/hubs/enhanced-notifications");
            app.MapHub<NotificationHub>("/hubs/notifications");
            app.MapHub<TransactionHub>("/hubs/transactions");

            // Static file fallback (optional)
            app.MapFallbackToFile("index.html");

            // Root landing page
            app.MapGet("/", () => "CoreBanking API is running. Visit /swagger for REST or use gRPC client.");

            app.Run();
        }
    }
    }

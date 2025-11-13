using CoreBanking.API.gRPC.Mappings;
using CoreBanking.Infrastructure.ServiceBus;
using CoreBanking.Infrastructure.BackgroundJobs;
using CoreBanking.Infrastructure.Services;
using CoreBankingTest.API.Extensions;
using CoreBankingTest.APP.BackgroundJobs;
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
using CoreBankingTest.DAL.ServiceBus.Handlers;
using CoreBankingTest.DAL.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using System.Reflection;



namespace CoreBanking.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // =====================================================================
            // DATABASE
            // =====================================================================
            builder.Services.AddDbContext<BankingDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // =====================================================================
            // CORE & INFRASTRUCTURE
            // =====================================================================
            builder.Services.AddScoped<IAccountRepository, AccountRepository>();
            builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
            builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
            builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

            // =====================================================================
            // EXTERNAL SERVICES + RESILIENCE
            // =====================================================================
            builder.Services.AddHttpClient<ICreditScoringServiceClient, CreditScoringServiceClient>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["CreditScoringApi:BaseUrl"] ?? "https://api.example.com");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            builder.Services.AddSingleton<ISimulatedCreditScoringService, SimulatedCreditScoringService>();
            builder.Services.AddSingleton<IResilientHttpClientService, ResilientHttpClientService>();
            builder.Services.AddScoped<IResilienceService, ResilienceService>();
            builder.Services.Configure<ResilienceOptions>(builder.Configuration.GetSection("Resilience"));

            // =====================================================================
            // AZURE SERVICE BUS (MOCK-SAFE CONFIG)
            // =====================================================================
            builder.Services.Configure<ServiceBusConfiguration>(builder.Configuration.GetSection("ServiceBus"));
            builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ServiceBusConfiguration>>().Value);

            builder.Services.AddSingleton<IServiceBusClientFactory>(provider =>
            {
                var env = provider.GetRequiredService<IHostEnvironment>();
                var logger = provider.GetRequiredService<ILogger<ServiceBusClientFactory>>();
                var config = provider.GetRequiredService<IOptions<ServiceBusConfiguration>>().Value;

                var connectionString = env.IsDevelopment() || string.IsNullOrWhiteSpace(config.ConnectionString)
                    ? "Endpoint=sb://mock-servicebus/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mock"
                    : config.ConnectionString;

                return new ServiceBusClientFactory(connectionString, logger);
            });

            builder.Services.AddSingleton<ServiceBusAdministration>(provider =>
            {
                var env = provider.GetRequiredService<IHostEnvironment>();
                var logger = provider.GetRequiredService<ILogger<ServiceBusAdministration>>();
                var config = provider.GetRequiredService<IOptions<ServiceBusConfiguration>>().Value;

                var connectionString = env.IsDevelopment() || string.IsNullOrWhiteSpace(config.ConnectionString)
                    ? "Endpoint=sb://mock-servicebus/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mock"
                    : config.ConnectionString;

                return new ServiceBusAdministration(connectionString, config, logger);
            });

            builder.Services.AddSingleton<IBankingServiceBusSender>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<BankingServiceBusSender>>();
                var configuration = provider.GetRequiredService<IConfiguration>();

                var conn = configuration.GetConnectionString("ServiceBus")
                    ?? "Endpoint=sb://mock-servicebus/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=mock";

                return new BankingServiceBusSender(conn, logger);
            });

            builder.Services.AddSingleton<IDeadLetterQueueProcessor, DeadLetterQueueProcessor>();
            builder.Services.AddSingleton<CustomerEventServiceBusHandler>();
            builder.Services.AddSingleton<TransactionEventServiceBusHandler>();

            builder.Services.AddHostedService<MessageProcessingService>();
            builder.Services.AddHostedService<DeadLetterQueueMonitorService>();

            // Fraud detection (mock-safe)
            builder.Services.AddScoped<IFraudDetectionService, MockFraudDetectionService>();

            // =====================================================================
            // HANGFIRE BACKGROUND JOBS
            // =====================================================================
            builder.Services.AddHangfireServices(builder.Configuration);

            // Register background job services
            builder.Services.AddScoped<IDailyStatementService, DailyStatementService>();
            builder.Services.AddScoped<IInterestCalculationService, InterestCalculationService>();
            builder.Services.AddScoped<IAccountMaintenanceService, AccountMaintenanceService>();
            builder.Services.AddScoped<IJobInitializationService, JobInitializationService>();

            // Register helper services for background jobs
            builder.Services.AddScoped<CoreBankingTest.CORE.Interfaces.IEmailService, EmailService>();
            builder.Services.AddScoped<CoreBankingTest.CORE.Interfaces.IPdfGenerationService, PdfGenerationService>();

            // =====================================================================
            // DOMAIN EVENT HANDLERS
            // =====================================================================
            builder.Services.AddTransient<INotificationHandler<AccountCreatedEvent>, AccountCreatedEventHandler>();
            builder.Services.AddTransient<INotificationHandler<MoneyTransferredEvent>, MoneyTransferedEventHandler>();
            builder.Services.AddTransient<INotificationHandler<InsufficientFundsEvent>, InsufficientFundsEventHandler>();
            builder.Services.AddTransient<INotificationHandler<MoneyTransferredEvent>, RealTimeNotificationEventHandler>();

            // =====================================================================
            // PIPELINE BEHAVIORS (MEDIATR)
            // =====================================================================
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainEventsBehavior<,>));
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

            // =====================================================================
            // gRPC
            // =====================================================================
            builder.Services.AddGrpc(options => options.EnableDetailedErrors = true);
            builder.Services.AddGrpcReflection();

            // =====================================================================
            // SIGNALR
            // =====================================================================
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                options.MaximumReceiveMessageSize = 64 * 1024; // 64KB
            }).AddMessagePackProtocol();

            builder.Services.AddSingleton<ConnectionStateService>();
            builder.Services.AddHostedService<TransactionBroadcastService>();
            builder.Services.AddScoped<INotificationBroadcaster, NotificationBroadcaster>();

            // =====================================================================
            // POLLY
            // =====================================================================
            builder.Services.AddSingleton(HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogWarning("Retry {RetryCount} after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                    }));

            builder.Services.AddSingleton<AdvancedPollyPolicies>();

            // =====================================================================
            // VALIDATION, MAPPING & OUTBOX
            // =====================================================================
            builder.Services.AddValidatorsFromAssembly(typeof(CreateAccountCommandValidator).Assembly);
            builder.Services.AddAutoMapper(cfg => { }, typeof(AccountProfile).Assembly);
            builder.Services.AddAutoMapper(cfg => { }, typeof(AccountGrpcProfile).Assembly);

            builder.Services.AddScoped<IOutboxMessageProcessor, OutboxMessageProcessor>();
            builder.Services.AddHostedService<OutboxBackgroundService>();

            // =====================================================================
            // MEDIATR CONFIGURATION
            // =====================================================================
            builder.Services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(CreateAccountCommand).Assembly);
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(DomainEventsBehavior<,>));
            });

            // =====================================================================
            // CONTROLLERS + SWAGGER
            // =====================================================================
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

            // =====================================================================
            // KESTREL (HTTP/1.1 + HTTP/2)
            // =====================================================================
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(5037, o => o.Protocols = HttpProtocols.Http1);
                options.ListenLocalhost(7288, o =>
                {
                    o.UseHttps();
                    o.Protocols = HttpProtocols.Http2;
                });
            });

            // =====================================================================
            // APP PIPELINE
            // =====================================================================
            var app = builder.Build();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAuthorization();
            app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

            // Hangfire Dashboard (accessible at /hangfire)
            app.UseHangfireDashboardWithAuth();

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

            // Ensure mock-safe Service Bus setup
            using (var scope = app.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var admin = scope.ServiceProvider.GetRequiredService<ServiceBusAdministration>();

                try
                {
                    await admin.EnsureInfrastructureExistsAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Startup] Skipping Service Bus setup (mock or offline).");
                }

                // Initialize Hangfire recurring jobs
                try
                {
                    var jobInitializer = scope.ServiceProvider.GetRequiredService<IJobInitializationService>();
                    await jobInitializer.InitializeRecurringJobsAsync();
                    logger.LogInformation("[Startup] Hangfire recurring jobs initialized successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[Startup] Failed to initialize Hangfire recurring jobs");
                }
            }

            // =====================================================================
            // ROUTING
            // =====================================================================
            app.MapControllers();
            app.MapGrpcService<AccountGrpcService>();
            app.MapGrpcService<EnhancedAccountGrpcService>();

            app.MapHub<NotificationHub>("/hubs/notifications");
            app.MapHub<EnhancedNotificationHub>("/hubs/enhanced-notifications");
            app.MapHub<TransactionHub>("/hubs/transactions");

            app.MapFallbackToFile("index.html");
            app.MapGet("/", () => "CoreBanking API is running. Visit /swagger for REST or use a gRPC client.");

            app.Run();
        }
    }
}

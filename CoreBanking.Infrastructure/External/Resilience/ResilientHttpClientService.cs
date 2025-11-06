using CoreBankingTest.APP.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.External.Resilience
{
    public class ResilientHttpClientService : IResilientHttpClientService
    {
        private readonly ILogger<ResilientHttpClientService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;

        public ResilientHttpClientService(ILogger<ResilientHttpClientService> logger)
        {
            _logger = logger;
            _resiliencePolicy = CreateResiliencePolicy();
        }

        public async Task<TResponse> ExecuteWithResilienceAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> action,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            var context = new Context(operationName)
            {
                ["Logger"] = _logger,
                ["OperationName"] = operationName,
                ["StartTime"] = DateTime.UtcNow
            };

            try
            {
                // For non-HTTP tasks, just run the action directly.
                return await action(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resilience policy failed for operation {OperationName}", operationName);
                throw new ExternalServiceException($"Operation {operationName} failed after retries", ex);
            }
        }

        public async Task<HttpResponseMessage> ExecuteHttpRequestWithResilienceAsync(
            Func<Task<HttpResponseMessage>> request,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            var context = new Context(operationName)
            {
                ["Logger"] = _logger,
                ["OperationName"] = operationName
            };

            return await _resiliencePolicy.ExecuteAsync(
                async (ctx, ct) => await request(),
                context,
                cancellationToken);
        }

        private IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy()
        {
            // Retry policy with exponential backoff
            var retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutRejectedException>()
                .OrResult(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, delay, retryCount, context) =>
                    {
                        var logger = context.GetLogger();
                        var statusCode = outcome.Result?.StatusCode.ToString() ?? "Exception";
                        logger?.LogWarning("Retry {RetryCount} for {Operation}. Status: {StatusCode}. Waiting {Delay}ms",
                            retryCount, context.OperationKey, statusCode, delay.TotalMilliseconds);
                    });

            // Circuit breaker policy
            var circuitBreakerPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutRejectedException>()
                .OrResult(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, breakDelay, context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogError("Circuit breaker opened for {Operation}. Break duration: {BreakDelay}ms",
                            context.OperationKey, breakDelay.TotalMilliseconds);
                    },
                    onReset: context =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogInformation("Circuit breaker reset for {Operation}", context.OperationKey);
                    });

            // Timeout policy (generic)
            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
                TimeSpan.FromSeconds(30),
                TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, timespan, task, exception) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning("Timeout after {Timeout}ms for {Operation}",
                        timespan.TotalMilliseconds, context.OperationKey);
                    return Task.CompletedTask;
                });

            // Fallback policy (generic)
            var fallbackPolicy = Policy<HttpResponseMessage>
                .Handle<Exception>()
                .OrResult(response => !response.IsSuccessStatusCode)
                .FallbackAsync(
                    fallbackAction: (delegateContext, cancellationToken) =>
                    {
                        var fallbackResponse = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                        {
                            Content = new StringContent("{\"error\":\"Service unavailable\"}")
                        };
                        return Task.FromResult(fallbackResponse);
                    },
                    onFallbackAsync: (outcome, context) =>
                    {
                        var logger = context.GetLogger();
                        logger?.LogWarning("Fallback triggered for {Operation}", context.OperationKey);
                        return Task.CompletedTask;
                    });

            // Combine all policies (same generic type)
            return Policy.WrapAsync(fallbackPolicy, circuitBreakerPolicy, retryPolicy, timeoutPolicy);
        }
    }

    public static class ContextExtensions
    {
        public static ILogger? GetLogger(this Context context)
        {
            return context.TryGetValue("Logger", out var logger) ? logger as ILogger : null;
        }
    }

    public class ExternalServiceException : Exception
    {
        public ExternalServiceException(string message) : base(message) { }
        public ExternalServiceException(string message, Exception innerException) : base(message, innerException) { }
    }
}


using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.External.Resilience
{
    public class AdvancedPollyPolicies
    {
        private readonly ILogger<AdvancedPollyPolicies> _logger;

        public AdvancedPollyPolicies(ILogger<AdvancedPollyPolicies> logger)
        {
            _logger = logger;
        }

        // Exponential backoff with jitter to prevent retry storms
        public IAsyncPolicy<HttpResponseMessage> CreateJitterRetryPolicy()
        {
            var jitterer = new Random();

            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .OrResult(r => (int)r.StatusCode >= 500) // Server errors
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retryAttempt =>
                    {
                        var exponentialDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                        var jitter = TimeSpan.FromMilliseconds(jitterer.Next(0, 1000));
                        return exponentialDelay + jitter;
                    },
                    onRetry: (outcome, delay, retryCount, context) =>
                    {
                        var operationKey = context.GetOperationKey();
                        _logger.LogWarning(
                            "Retry {RetryCount} after {Delay}ms for {Operation}. Status: {StatusCode}",
                            retryCount, delay.TotalMilliseconds,
                            operationKey,
                            (int)(outcome.Result?.StatusCode ?? 0));
                    });
        }

        // Circuit breaker with advanced monitoring
        public IAsyncPolicy<HttpResponseMessage> CreateAdvancedCircuitBreakerPolicy()
        {
            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5,    // Break if 50% of requests fail
                    samplingDuration: TimeSpan.FromSeconds(30),
                    minimumThroughput: 8,      // Minimum requests in sampling duration
                    durationOfBreak: TimeSpan.FromSeconds(60),
                    onBreak: (outcome, breakDelay, context) =>
                    {
                        var operationKey = context.GetOperationKey();
                        _logger.LogError(
                            "Circuit breaker opened for {Operation}. Break duration: {BreakDelay}s",
                            operationKey, breakDelay.TotalSeconds);

                        NotifyCircuitBreakerOpened(operationKey);
                    },
                    onReset: context =>
                    {
                        var operationKey = context.GetOperationKey();
                        _logger.LogInformation(
                            "Circuit breaker reset for {Operation}",
                            operationKey);

                        NotifyCircuitBreakerReset(operationKey);
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker half-open - testing service health");
                    });
        }

        // Bulkhead isolation - prevent one slow service from consuming all resources
        public IAsyncPolicy<HttpResponseMessage> CreateBulkheadPolicy()
        {
            return Policy.BulkheadAsync<HttpResponseMessage>(
                maxParallelization: 10,        // Max concurrent executions
                maxQueuingActions: 5,          // Max queued executions
                onBulkheadRejectedAsync: context =>
                {
                    var operationKey = context.GetOperationKey();
                    _logger.LogWarning(
                        "Bulkhead rejected execution for {Operation}. Limit reached.",
                        operationKey);
                    return Task.CompletedTask;
                });
        }

        // Fallback with cache strategy
        public IAsyncPolicy<HttpResponseMessage> CreateCachedFallbackPolicy()
        {
            return Policy<HttpResponseMessage>
                .Handle<Exception>()
                .OrResult(r => !r.IsSuccessStatusCode)
                .FallbackAsync(
                    fallbackAction: async (outcome, context, cancellationToken) =>
                    {
                        var operationKey = context.GetOperationKey();
                        _logger.LogInformation(
                            "Using fallback response for {Operation}",
                            operationKey);

                        // Try to get cached data
                        var cachedResponse = await GetCachedResponse(operationKey);
                        if (cachedResponse != null)
                        {
                            return cachedResponse;
                        }

                        // Return a generic service unavailable response
                        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                        {
                            Content = new StringContent(
                                "{\"error\":\"Service temporarily unavailable\",\"usingFallback\":true}",
                                Encoding.UTF8,
                                "application/json")
                        };
                    },
                    onFallbackAsync: (outcome, context) =>
                    {
                        var operationKey = context.GetOperationKey();
                        _logger.LogWarning(
                            "Fallback triggered for {Operation}. Original error: {Error}",
                            operationKey, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                        return Task.CompletedTask;
                    });
        }

        // Timeout policy with cancellation support
        public IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy()
        {
            return Policy.TimeoutAsync<HttpResponseMessage>(
                timeout: TimeSpan.FromSeconds(15),
                timeoutStrategy: TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, timespan, task, exception) =>
                {
                    var operationKey = context.GetOperationKey();
                    _logger.LogWarning(
                        "Timeout after {Timeout}s for {Operation}",
                        timespan.TotalSeconds, operationKey);
                    return Task.CompletedTask;
                });
        }

        // Complete resilience pipeline combining all policies
        public IAsyncPolicy<HttpResponseMessage> CreateResiliencePipeline()
        {
            var retryPolicy = CreateJitterRetryPolicy();
            var circuitBreaker = CreateAdvancedCircuitBreakerPolicy();
            var timeoutPolicy = CreateTimeoutPolicy();
            var fallbackPolicy = CreateCachedFallbackPolicy();
            var bulkheadPolicy = CreateBulkheadPolicy();

            // Combine policies in strategic order
            return Policy.WrapAsync(fallbackPolicy, circuitBreaker, bulkheadPolicy, retryPolicy, timeoutPolicy);
        }

        private void NotifyCircuitBreakerOpened(string operation)
        {
            _logger.LogError("ALERT: Circuit breaker opened for {Operation}", operation);
        }

        private void NotifyCircuitBreakerReset(string operation)
        {
            _logger.LogInformation("ALERT: Circuit breaker reset for {Operation}", operation);
        }

        private async Task<HttpResponseMessage?> GetCachedResponse(string operationKey)
        {
            // Implement caching logic based on your requirements
            return null; // Simplified for example
        }
    }

    // Extension method to safely get the operation key from context
    public static class PollyContextExtensions
    {
        public static string GetOperationKey(this Context context)
        {
            if (context.TryGetValue("OperationKey", out var operationKeyObj) && operationKeyObj is string operationKey)
            {
                return operationKey;
            }

            // Fallback to the context key if OperationKey not set
            return context.OperationKey ?? "UnknownOperation";
        }
    }
}

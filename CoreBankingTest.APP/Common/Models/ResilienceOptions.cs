using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Common.Models
{
    public class ResilienceOptions
    {
        public RetryOptions Retry { get; set; } = new();
        public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
        public TimeoutOptions Timeout { get; set; } = new();
        public BulkheadOptions Bulkhead { get; set; } = new();
    }

    public class RetryOptions
    {
        public int MaxRetries { get; set; } = 3;
        public double BaseDelaySeconds { get; set; } = 2;
        public bool UseJitter { get; set; } = true;
        public int MaxJitterMilliseconds { get; set; } = 1000;
    }

    public class CircuitBreakerOptions
    {
        public double FailureThreshold { get; set; } = 0.5;
        public int SamplingDurationSeconds { get; set; } = 30;
        public int MinimumThroughput { get; set; } = 8;
        public int BreakDurationSeconds { get; set; } = 60;
    }

    public class TimeoutOptions
    {
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class BulkheadOptions
    {
        public int MaxParallelization { get; set; } = 10;
        public int MaxQueuingActions { get; set; } = 5;
    }
}

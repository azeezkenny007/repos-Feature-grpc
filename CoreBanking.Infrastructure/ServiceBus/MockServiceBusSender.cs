using CoreBankingTest.CORE.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public class MockServiceBusSender : IServiceBusSender
    {
        private readonly ILogger<MockServiceBusSender> _logger;

        public MockServiceBusSender(ILogger<MockServiceBusSender> logger) => _logger = logger;

        public Task SendMessageAsync(string queueOrTopicName, string message, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("MOCK: Would send message to {Destination}: {Message}", queueOrTopicName, message);
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(string queueOrTopicName, byte[] messageBody, IDictionary<string, object> properties = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("MOCK: Would send binary message to {Destination} with {PropertiesCount} properties",
                queueOrTopicName, properties?.Count ?? 0);
            return Task.CompletedTask;
        }

        public Task ScheduleMessageAsync(string queueOrTopicName, string message, DateTimeOffset scheduledEnqueueTime, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("MOCK: Would schedule message for {ScheduledTime} to {Destination}",
                scheduledEnqueueTime, queueOrTopicName);
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(string queueOrTopicName, string messageBody, IDictionary<string, object> properties = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}

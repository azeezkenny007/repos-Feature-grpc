using Azure.Messaging.ServiceBus;
using CoreBankingTest.CORE.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public class ServiceBusSender : IServiceBusSender, IAsyncDisposable
    {
        private readonly ServiceBusClient _client;
        private readonly ILogger<ServiceBusSender> _logger;

        public ServiceBusSender(string connectionString, ILogger<ServiceBusSender> logger)
        {
            _client = new ServiceBusClient(connectionString);
            _logger = logger;
        }

        public async Task SendMessageAsync(string queueOrTopicName, string message, CancellationToken cancellationToken = default)
        {
            await using var sender = _client.CreateSender(queueOrTopicName);
            var serviceBusMessage = new ServiceBusMessage(message);
            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            _logger.LogDebug("Message sent to {Destination}", queueOrTopicName);
        }

        public async Task SendMessageAsync(string queueOrTopicName, byte[] messageBody, IDictionary<string, object> properties = null, CancellationToken cancellationToken = default)
        {
            await using var sender = _client.CreateSender(queueOrTopicName);
            var serviceBusMessage = new ServiceBusMessage(messageBody);

            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    serviceBusMessage.ApplicationProperties.Add(prop.Key, prop.Value);
                }
            }

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
            _logger.LogDebug("Binary message sent to {Destination}", queueOrTopicName);
        }

        public async Task ScheduleMessageAsync(string queueOrTopicName, string message, DateTimeOffset scheduledEnqueueTime, CancellationToken cancellationToken = default)
        {
            await using var sender = _client.CreateSender(queueOrTopicName);
            var serviceBusMessage = new ServiceBusMessage(message);

            var sequenceNumber = await sender.ScheduleMessageAsync(serviceBusMessage, scheduledEnqueueTime, cancellationToken);
            _logger.LogInformation("Message scheduled for {ScheduledTime}", scheduledEnqueueTime);
        }

        public async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync();
        }

        public Task SendMessageAsync(string queueOrTopicName, string messageBody, IDictionary<string, object> properties = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}

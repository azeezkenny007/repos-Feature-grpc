using CoreBankingTest.CORE.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.ServiceBus
{
    public class ServiceBusEventPublisher : IEventPublisher
    {
        private readonly IServiceBusSender _serviceBusSender;
        private readonly ILogger<ServiceBusEventPublisher> _logger;

        public ServiceBusEventPublisher(IServiceBusSender serviceBusSender, ILogger<ServiceBusEventPublisher> logger)
        {
            _serviceBusSender = serviceBusSender;
            _logger = logger;
        }

        public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
        {
            var topicName = GetTopicNameForEvent(typeof(TEvent));
            var eventType = domainEvent.GetType().Name;
            var eventData = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["EventId"] = domainEvent.EventId.ToString(),
                ["OccurredOn"] = domainEvent.OccurredOn,
                ["Source"] = "CoreBanking"
            };

            await _serviceBusSender.SendMessageAsync(topicName, eventData, properties, cancellationToken);

            _logger.LogInformation("Published {EventType} with ID {EventId} to {Topic}",
                eventType, domainEvent.EventId, topicName);
        }

        public async Task PublishAsync(string topicName, string eventType, string eventData, CancellationToken cancellationToken = default)
        {
            var properties = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["EventId"] = Guid.NewGuid().ToString(),
                ["OccurredOn"] = DateTime.UtcNow,
                ["Source"] = "CoreBanking"
            };

            await _serviceBusSender.SendMessageAsync(topicName, eventData, properties, cancellationToken);

            _logger.LogDebug("Published custom event {EventType} to {Topic}", eventType, topicName);
        }

        private string GetTopicNameForEvent(Type eventType)
        {
            // Map domain events to appropriate topics
            if (eventType.Name.EndsWith("Event"))
            {
                var category = eventType.Name.Replace("Event", "").ToLowerInvariant();
                if (category.Contains("customer"))
                    return "customer-events";
                if (category.Contains("account"))
                    return "account-events";
                if (category.Contains("transaction"))
                    return "transaction-events";
            }

            return "general-events";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Interfaces
{
    public interface IEventPublisher
    {
        Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;
        Task PublishAsync(string topicName, string eventType, string eventData, CancellationToken cancellationToken = default);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Interfaces
{

    public interface IServiceBusSender
    {
        Task SendMessageAsync(string queueOrTopicName, string message, CancellationToken cancellationToken = default);
        Task SendMessageAsync(string queueOrTopicName, byte[] messageBody, IDictionary<string, object> properties = null, CancellationToken cancellationToken = default);

        Task SendMessageAsync(string queueOrTopicName, string messageBody, IDictionary<string, object> properties = null, CancellationToken cancellationToken = default);
        Task ScheduleMessageAsync(string queueOrTopicName, string message, DateTimeOffset scheduledEnqueueTime, CancellationToken cancellationToken = default);

    }
}

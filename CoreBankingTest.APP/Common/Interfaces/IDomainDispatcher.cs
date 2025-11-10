using CoreBankingTest.CORE.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Common.Interfaces
{
    public interface IDomainEventDispatcher
    {
        Task DispatchDomainEventsAsync(CancellationToken cancellationToken);
        //Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
        //Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Interfaces
{
    public interface IDomainEvent
    {
        Guid EventId { get; }
        string EventType { get; }
        DateTime OccurredOn { get; }

    }
}

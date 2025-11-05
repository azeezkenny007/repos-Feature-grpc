using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.CORE.Common;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.DAL.Data;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.DAL.Services
{
    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly BankingDbContext _context;
        private readonly IPublisher _publisher;
        private readonly ILogger<DomainEventDispatcher> _logger;

        public DomainEventDispatcher(
            BankingDbContext context,
            IPublisher publisher,
            ILogger<DomainEventDispatcher> logger)
        {
            _context = context;
            _publisher = publisher;
            _logger = logger;
        }

        public async Task DispatchDomainEventsAsync(CancellationToken cancellationToken = default)
        {
            var domainEntities = _context.ChangeTracker
                .Entries<IAggregateRoot>()
                .Where(x => x.Entity.DomainEvents.Any())
                .ToList();

            var domainEvents = domainEntities
                .SelectMany(x => x.Entity.DomainEvents)
                .ToList();

            foreach (var domainEvent in domainEvents)
            {
                _logger.LogInformation("Dispatching domain event: {EventType}", domainEvent.GetType().Name);
                await _publisher.Publish(domainEvent, cancellationToken);
            }

            domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());
        }
    }
}

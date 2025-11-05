using CoreBankingTest.CORE.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;

namespace CoreBankingTest.APP.Accounts.EventHandlers
{
    public class AccountCreatedEventHandler : INotificationHandler<AccountCreatedEvent>
    {
        private readonly ILogger<AccountCreatedEventHandler> _logger;
        //private readonly IEmailService _emailService;

        //public AccountCreatedEventHandler(ILogger<AccountCreatedEventHandler> logger, IEmailService emailService)
        public AccountCreatedEventHandler(ILogger<AccountCreatedEventHandler> logger)
        {
            _logger = logger;
            //_emailService = emailService;
        }

        public async Task Handle(AccountCreatedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing account created event for account {AccountNumber}",
                notification.AccountNumber);

            try
            {
                // Send welcome email
                //await _emailService.SendWelcomeEmailAsync(notification.CustomerId, notification.AccountNumber);

                // Could also: Update search index, trigger compliance checks, etc.
                _logger.LogInformation("Successfully processed account created event");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process account created event for {AccountNumber}",
                    notification.AccountNumber);
                throw;
            }
        }
    }
}

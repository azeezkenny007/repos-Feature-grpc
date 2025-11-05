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
    public class InsufficientFundsEventHandler : INotificationHandler<InsufficientFundsEvent>
    {
        private readonly ILogger<InsufficientFundsEventHandler> _logger;
        //private readonly IFraudDetectionService _fraudDetectionService;

        //public InsufficientFundsEventHandler(ILogger<InsufficientFundsEventHandler> logger, IFraudDetectionService fraudDetectionService)
        public InsufficientFundsEventHandler(ILogger<InsufficientFundsEventHandler> logger)
        {
            _logger = logger;
            //_fraudDetectionService = fraudDetectionService;
        }

        public async Task Handle(InsufficientFundsEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Processing insufficient funds event for account {AccountNumber}",
                notification.AccountNumber);

            // Trigger fraud detection analysis
            //await _fraudDetectionService.AnalyzeAccountActivityAsync(
            //  notification.AccountNumber,
            //notification.RequestedAmount,
            //notification.CurrentBalance);

            // Could also: Notify customer success team, update credit risk assessment, etc.
            _logger.LogInformation("Completed insufficient funds event processing");
        }
    }
}

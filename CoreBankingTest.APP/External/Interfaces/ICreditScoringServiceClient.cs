using CoreBankingTest.APP.External.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.External.Interfaces
{
    public interface ICreditScoringServiceClient
    {
        Task<CSCreditScoreResponse> GetCreditScoreAsync(string customerId, CancellationToken cancellationToken = default);
        Task<CSCreditReportResponse> GetCreditReportAsync(string customerId, CancellationToken cancellationToken = default);
        Task<bool> ValidateCustomerAsync(CSCustomerValidationRequest request, CancellationToken cancellationToken = default);
    }
}

using CoreBankingTest.APP.External.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Interfaces
{
    public interface ISimulatedCreditScoringService
    {
        Task<SimulatedCreditScoreResponse> GetCreditScoreAsync(string bvn, CancellationToken cancellationToken = default);
        Task<SimulatedCreditReportResponse> GetCreditReportAsync(string bvn, CancellationToken cancellationToken = default);
        Task<SimulatedValidationResponse> ValidateCustomerAsync(SimulatedValidationRequest request, CancellationToken cancellationToken = default);
        Task<SimulatedBVNResponse> ValidateBVNAsync(string bvn, CancellationToken cancellationToken = default);
    }
}

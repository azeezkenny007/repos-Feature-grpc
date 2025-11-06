using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.External.DTOs
{
    public record CSCreditReportResponse
    {
        public string CustomerId { get; init; } = string.Empty;
        public decimal TotalDebt { get; init; }
        public int ActiveAccounts { get; init; }
        public int LatePayments { get; init; }
        public string Status { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;

        public static CSCreditReportResponse Failed(string error) => new()
        {
            IsSuccess = false,
            ErrorMessage = error
        };
    }
}

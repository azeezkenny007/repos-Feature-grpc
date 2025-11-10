using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.External.DTOs
{
    public record SimulatedCreditReportResponse
    {
        public string BVN { get; init; } = string.Empty;
        public decimal TotalDebt { get; init; }
        public int ActiveAccounts { get; init; }
        public int LatePayments { get; init; }
        public decimal CreditUtilization { get; init; } // Percentage
        public int OldestAccountAgeMonths { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime ReportGeneratedAt { get; init; }
        public bool IsSuccess { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
    }
}

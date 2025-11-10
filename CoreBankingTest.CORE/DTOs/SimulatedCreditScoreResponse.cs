using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.External.DTOs
{
    public record SimulatedCreditScoreResponse
    {
        public string BVN { get; init; } = string.Empty;
        public int Score { get; init; }
        public string Band { get; init; } = string.Empty;
        public string[] Factors { get; init; } = Array.Empty<string>();
        public DateTime GeneratedAt { get; init; }
        public bool IsSuccess { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
    }
}

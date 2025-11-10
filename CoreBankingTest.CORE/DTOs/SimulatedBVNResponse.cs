using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.External.DTOs
{
    public record SimulatedBVNResponse
    {
        public string BVN { get; init; } = string.Empty;
        public bool IsValid { get; init; }
        public DateTime ValidationDate { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}

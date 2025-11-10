using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.External.DTOs
{
    public record SimulatedValidationRequest
    {
        public string BVN { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public DateTime DateOfBirth { get; init; }
    }
}

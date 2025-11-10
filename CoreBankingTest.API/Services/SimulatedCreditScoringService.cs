using CoreBankingTest.APP.External.DTOs;
using CoreBankingTest.CORE.Interfaces;

namespace CoreBankingTest.API.Services
{
  

    public class SimulatedCreditScoringService : ISimulatedCreditScoringService
    {
        private readonly ILogger<SimulatedCreditScoringService> _logger;
        private readonly Random _random = new();
        private readonly Dictionary<string, SimulatedCustomerProfile> _customerProfiles;

        public SimulatedCreditScoringService(ILogger<SimulatedCreditScoringService> logger)
        {
            _logger = logger;
            _customerProfiles = InitializeCustomerProfiles();
        }

        public async Task<SimulatedCreditScoreResponse> GetCreditScoreAsync(string bvn, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Simulating credit score lookup for BVN: {BVN}", bvn);

            // Simulate network latency
            await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(100, 500)), cancellationToken);

            // Simulate random failures (5% failure rate for testing resilience)
            if (_random.NextDouble() < 0.05)
            {
                throw new HttpRequestException("Simulated service outage");
            }

            if (_customerProfiles.TryGetValue(bvn, out var profile))
            {
                var response = new SimulatedCreditScoreResponse
                {
                    BVN = bvn,
                    Score = profile.BaseScore + _random.Next(-50, 50), // Add some variation
                    Band = GetCreditBand(profile.BaseScore),
                    Factors = profile.CreditFactors,
                    GeneratedAt = DateTime.UtcNow,
                    IsSuccess = true
                };

                _logger.LogInformation("Returning credit score {Score} for BVN {BVN}", response.Score, bvn);
                return response;
            }

            // Return a default score for unknown BVNs
            return new SimulatedCreditScoreResponse
            {
                BVN = bvn,
                Score = 550 + _random.Next(-100, 100),
                Band = "Fair",
                Factors = new[] { "Limited credit history" },
                GeneratedAt = DateTime.UtcNow,
                IsSuccess = true
            };
        }

        public async Task<SimulatedCreditReportResponse> GetCreditReportAsync(string bvn, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Simulating credit report generation for BVN: {BVN}", bvn);

            await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(200, 800)), cancellationToken);

            // Simulate occasional slow responses
            if (_random.NextDouble() < 0.1)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }

            if (_customerProfiles.TryGetValue(bvn, out var profile))
            {
                return new SimulatedCreditReportResponse
                {
                    BVN = bvn,
                    TotalDebt = profile.TotalDebt,
                    ActiveAccounts = profile.ActiveAccounts,
                    LatePayments = profile.LatePayments,
                    CreditUtilization = profile.CreditUtilization,
                    OldestAccountAgeMonths = profile.OldestAccountAgeMonths,
                    Status = GetCreditStatus(profile.BaseScore),
                    ReportGeneratedAt = DateTime.UtcNow,
                    IsSuccess = true
                };
            }

            return new SimulatedCreditReportResponse
            {
                BVN = bvn,
                TotalDebt = _random.Next(5000, 50000),
                ActiveAccounts = _random.Next(1, 5),
                LatePayments = _random.Next(0, 3),
                CreditUtilization = _random.Next(10, 80),
                OldestAccountAgeMonths = _random.Next(12, 120),
                Status = "Unknown",
                ReportGeneratedAt = DateTime.UtcNow,
                IsSuccess = true
            };
        }

        public async Task<SimulatedValidationResponse> ValidateCustomerAsync(SimulatedValidationRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validating customer with BVN: {BVN}", request.BVN);

            await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(150, 400)), cancellationToken);

            // Simulate validation logic
            var isValid = !string.IsNullOrEmpty(request.FullName) &&
                        request.FullName.Split(' ').Length >= 2 &&
                        request.DateOfBirth <= DateTime.UtcNow.AddYears(-18) &&
                        request.BVN.Length == 11;

            var reasons = new List<string>();

            if (!isValid)
            {
                if (string.IsNullOrEmpty(request.FullName) || request.FullName.Split(' ').Length < 2)
                    reasons.Add("Invalid full name format");
                if (request.DateOfBirth > DateTime.UtcNow.AddYears(-18))
                    reasons.Add("Customer must be at least 18 years old");
                if (request.BVN.Length != 11)
                    reasons.Add("Invalid BVN format");
            }

            return new SimulatedValidationResponse
            {
                IsValid = isValid,
                Reason = isValid ? "Validation successful" : string.Join("; ", reasons),
                ValidatedAt = DateTime.UtcNow
            };
        }

        public async Task<SimulatedBVNResponse> ValidateBVNAsync(string bvn, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validating BVN: {BVN}", bvn);

            await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(100, 300)), cancellationToken);

            // Simple BVN validation logic
            var isValid = bvn.Length == 11 && bvn.All(char.IsDigit);

            return new SimulatedBVNResponse
            {
                BVN = bvn,
                IsValid = isValid,
                ValidationDate = DateTime.UtcNow,
                Message = isValid ? "BVN validation successful" : "Invalid BVN format"
            };
        }

        private string GetCreditBand(int score) => score switch
        {
            >= 800 => "Excellent",
            >= 740 => "Very Good",
            >= 670 => "Good",
            >= 580 => "Fair",
            _ => "Poor"
        };

        private string GetCreditStatus(int score) => score switch
        {
            >= 670 => "Good Standing",
            >= 580 => "Needs Improvement",
            _ => "High Risk"
        };

        private Dictionary<string, SimulatedCustomerProfile> InitializeCustomerProfiles()
        {
            return new Dictionary<string, SimulatedCustomerProfile>
            {
                ["20000000001"] = new(750, 25000, 3, 0, 35, 48, new[] { "Low credit utilization", "No late payments", "Mixed credit types" }),
                ["20000000002"] = new(680, 45000, 5, 2, 65, 84, new[] { "High credit utilization", "Recent late payments", "Multiple credit inquiries" }),
                ["20000000003"] = new(820, 15000, 2, 0, 15, 60, new[] { "Excellent payment history", "Low credit utilization", "Long credit history" }),
                ["20000000009"] = new(400, 75000, 8, 5, 85, 24, new[] { "Very high credit utilization", "Multiple late payments", "Recent defaults" })
            };
        }
    }

}

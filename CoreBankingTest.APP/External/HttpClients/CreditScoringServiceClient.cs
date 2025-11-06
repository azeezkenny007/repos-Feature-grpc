using CoreBankingTest.APP.External.DTOs;
using CoreBankingTest.APP.External.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.External.HttpClients
{
    public class CreditScoringServiceClient : ICreditScoringServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CreditScoringServiceClient> _logger;

        public CreditScoringServiceClient(HttpClient httpClient, ILogger<CreditScoringServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<CSCreditScoreResponse> GetCreditScoreAsync(string customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Requesting credit score for customer {CustomerId}", customerId);

                var response = await _httpClient.GetAsync($"/api/credit/score/{customerId}", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Credit score request failed with status {StatusCode} for customer {CustomerId}",
                        response.StatusCode, customerId);
                    return CSCreditScoreResponse.Failed($"Service returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var scoreResponse = JsonSerializer.Deserialize<CSCreditScoreResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Successfully retrieved credit score for customer {CustomerId}", customerId);
                return scoreResponse ?? CSCreditScoreResponse.Failed("Invalid response format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving credit score for customer {CustomerId}", customerId);
                return CSCreditScoreResponse.Failed($"Service error: {ex.Message}");
            }
        }

        public async Task<CSCreditReportResponse> GetCreditReportAsync(string customerId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Requesting credit report for customer {CustomerId}", customerId);

                var response = await _httpClient.GetAsync($"/api/credit/report/{customerId}", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Credit report request failed with status {StatusCode} for customer {CustomerId}",
                        response.StatusCode, customerId);
                    return CSCreditReportResponse.Failed($"Service returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var reportResponse = JsonSerializer.Deserialize<CSCreditReportResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Successfully retrieved credit report for customer {CustomerId}", customerId);
                return reportResponse ?? CSCreditReportResponse.Failed("Invalid response format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving credit report for customer {CustomerId}", customerId);
                return CSCreditReportResponse.Failed($"Service error: {ex.Message}");
            }
        }

        public async Task<bool> ValidateCustomerAsync(CSCustomerValidationRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Validating customer {CustomerId}", request.CustomerId);

                var jsonContent = JsonSerializer.Serialize(request);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/customer/validate", httpContent, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Customer validation failed with status {StatusCode} for customer {CustomerId}",
                        response.StatusCode, request.CustomerId);
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var validationResponse = JsonSerializer.Deserialize<CSValidationResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Customer validation result for {CustomerId}: {IsValid}",
                    request.CustomerId, validationResponse?.IsValid ?? false);

                return validationResponse?.IsValid ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating customer {CustomerId}", request.CustomerId);
                return false;
            }
        }
    }
}

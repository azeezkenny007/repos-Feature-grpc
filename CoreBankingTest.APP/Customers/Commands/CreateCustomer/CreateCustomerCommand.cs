using CoreBankingTest.APP.Common.Exceptions;
using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Models;
using CoreBankingTest.APP.External.DTOs;
using CoreBankingTest.APP.External.Interfaces;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Enums;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;



namespace CoreBankingTest.APP.Customers.Commands.CreateCustomer
{



    public record CreateCustomerCommand : ICommand<CustomerId>
    {
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
        public string BVN { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public DateTime DateOfBirth { get; init; }
    }


    public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<CustomerId>>
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateCustomerCommandHandler> _logger;
        private readonly ICreditScoringServiceClient _creditScoringClient;
        private readonly IResilientHttpClientService _resilientClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISimulatedCreditScoringService _creditScoringService;
        private readonly IResilienceService _resilienceService;

        public CreateCustomerCommandHandler(
            ICustomerRepository customerRepository,
            IUnitOfWork unitOfWork,
            ILogger<CreateCustomerCommandHandler> logger,
            ICreditScoringServiceClient creditScoringClient,
            IResilientHttpClientService resilientClient,
            IHttpClientFactory httpClientFactory,
            ISimulatedCreditScoringService creditScoringService,
            IResilienceService resilienceService)
        {
            _customerRepository = customerRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _creditScoringClient = creditScoringClient;
            _resilientClient = resilientClient;
            _httpClientFactory = httpClientFactory;
            _creditScoringService = creditScoringService;
            _resilienceService = resilienceService;
        }

        public async Task<Result<CustomerId>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating customer for {Email}", request.Email);

            try
            {
                // Step 1: Validate customer with external BVN service
                //var bvnValidationResult = await ValidateBVNWithResilienceAsync(request, cancellationToken);
                //if (!bvnValidationResult.IsValid)
                //{

                //    return Result<CustomerId>.Failure("BVN validation failed: " + bvnValidationResult.Reason);
                //}

                // Validate BVN with advanced resilience
                var bvnValidationResult = await ValidateBVNWithAdvancedResilienceAsync(request.BVN, cancellationToken);
                if (!bvnValidationResult.IsValid)
                {
                    return Result<CustomerId>.Failure($"BVN validation failed: {bvnValidationResult.Message}");
                }

                // Validate customer details
                var customerValidation = await ValidateCustomerDetailsWithResilienceAsync(request, cancellationToken);
                if (!customerValidation.IsValid)
                {
                    return Result<CustomerId>.Failure($"Customer validation failed: {customerValidation.Reason}");
                }

                // Step 2: Check credit score with resilience
                //var creditScore = await GetCreditScoreWithResilienceAsync(request.BVN, cancellationToken);
                //if (!creditScore.IsSuccess || creditScore.Score < 300)
                //{
                //    return Result<CustomerId>.Failure("Credit score below minimum requirement");
                //}

                // Get credit score with circuit breaker protection
                var creditScore = await GetCreditScoreWithCircuitBreakerAsync(request.BVN, cancellationToken);
                if (!creditScore.IsSuccess || creditScore.Score < 350)
                {
                    return Result<CustomerId>.Failure(
                        $"Credit score {creditScore.Score} below minimum requirement (350)");
                }


                // Step 3: Create customer entity
                var customer = new Customer(
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.PhoneNumber,
                    request.DateOfBirth,
                    request.BVN,
                    creditScore.Score);

                // Step 5: Publish customer created event
                await _customerRepository.AddAsync(customer, cancellationToken);

                var affectedRows = await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (affectedRows == 0)
                {
                    _logger.LogWarning("No changes were saved to the database for customer {Email}", request.Email);
                    return Result<CustomerId>.Failure("Failed to save customer data");
                }

                _logger.LogInformation("Successfully created customer {CustomerId} with credit score {Score}",
                    customer.CustomerId, creditScore.Score);


                await PublishCustomerCreatedEvent(customer, creditScore);
                return Result<CustomerId>.Success(customer.CustomerId);


            }
            catch (ExternalServiceException ex)
            {
                _logger.LogError(ex, "External service failure during customer creation for {Email}", request.Email);
                return Result<CustomerId>.Failure("Unable to validate customer information at this time");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer for {Email}", request.Email);
                return Result<CustomerId>.Failure("Failed to create customer");
            }
        }

        private async Task<CSValidationResponse> ValidateBVNWithResilienceAsync(
            CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            var bvnClient = _httpClientFactory.CreateClient("BVNValidation");

            var validationRequest = new CSCustomerValidationRequest
            {
                CustomerId = request.BVN,
                FullName = $"{request.FirstName} {request.LastName}",
                DateOfBirth = request.DateOfBirth,
                BVN = request.BVN
            };

            // Using resilient execution for BVN validation
            var response = await _resilientClient.ExecuteHttpRequestWithResilienceAsync(
                async () =>
                {
                    var jsonContent = JsonSerializer.Serialize(validationRequest);
                    var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    return await bvnClient.PostAsync("/api/validate", httpContent, cancellationToken);
                },
                "BVNValidation",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<CSValidationResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new CSValidationResponse { IsValid = false, Reason = "Invalid response format" };
            }

            return new CSValidationResponse { IsValid = false, Reason = $"Service returned {response.StatusCode}" };
        }

        private async Task<CSCreditScoreResponse> GetCreditScoreWithResilienceAsync(
            string BVN, CancellationToken cancellationToken)
        {
            return await _resilientClient.ExecuteWithResilienceAsync(
                async (ct) => await _creditScoringClient.GetCreditScoreAsync(BVN, ct),
                "CreditScoreLookup",
                cancellationToken);
        }


        private async Task<SimulatedBVNResponse> ValidateBVNWithAdvancedResilienceAsync(
    string bvn, CancellationToken cancellationToken)
        {
            return await _resilienceService.ExecuteWithResilienceAsync(
                async (ct) => await _creditScoringService.ValidateBVNAsync(bvn, ct),
                $"BVNValidation-{bvn}",
                cancellationToken);
        }

        private async Task<SimulatedValidationResponse> ValidateCustomerDetailsWithResilienceAsync(
            CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            var validationRequest = new SimulatedValidationRequest
            {
                BVN = request.BVN,
                FullName = $"{request.FirstName} {request.LastName}",
                DateOfBirth = request.DateOfBirth
            };

            return await _resilientClient.ExecuteWithResilienceAsync(
                async (ct) => await _creditScoringService.ValidateCustomerAsync(validationRequest, ct),
                "CustomerValidation",
                cancellationToken);
        }

        private async Task<SimulatedCreditScoreResponse> GetCreditScoreWithCircuitBreakerAsync(
            string bvn, CancellationToken cancellationToken)
        {
            return await _resilienceService.ExecuteWithResilienceAsync(
                async (ct) => await _creditScoringService.GetCreditScoreAsync(bvn, ct),
                $"CreditScoreLookup-{bvn}",
                cancellationToken);
        }

        private async Task PublishCustomerCreatedEvent(Customer customer, SimulatedCreditScoreResponse creditScore)
        {
            // This will be implemented in Day 10 with Azure Service Bus
            _logger.LogInformation(
                "Would publish CustomerCreatedEvent for {CustomerId} with credit band {Band}",
                customer.CustomerId, creditScore.Band);
        }





    }
}
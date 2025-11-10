using AutoMapper;
using CoreBankingTest.API.Models;
using CoreBankingTest.API.Models.Requests;
using CoreBankingTest.APP.Customers.Commands.CreateCustomer;
using CoreBankingTest.APP.Customers.Queries.GetCustomer;
using CoreBankingTest.APP.Customers.Queries.GetCustomers;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CoreBankingTest.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(IMediator mediator, IMapper mapper, ILogger<CustomersController> logger)
        {
            _mediator = mediator;
            _mapper = mapper;
            _logger = logger;
        }


        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<CustomerDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<CustomerDto>>>> GetCustomers()
        {
            var query = new GetCustomersQuery();
            var result = await _mediator.Send(query);
            return Ok(ApiResponse<List<CustomerDto>>.CreateSuccess(result.Data!));
        }



        [HttpGet("{customerId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<CustomerDetailsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<CustomerDetailsDto>>> GetCustomer(Guid customerId)
        {
            var query = new GetCustomerDetailsQuery { CustomerId = customerId };
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
                return NotFound(ApiResponse.CreateFailure(result.Errors));

            return Ok(ApiResponse<CustomerDetailsDto>.CreateSuccess(result.Data!));
        }

        [HttpPost]
        public async Task<IActionResult> EnhancedCreateCustomer([FromBody] CreateCustomerRequest request)
        {
            _logger.LogInformation("Received customer creation request for {Email}", request.Email);

            var command = new CreateCustomerCommand
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.Phone,
                BVN = request.BVN,
                Address = request.Address,
                DateOfBirth = request.DateOfBirth
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully created customer with ID {CustomerId}", result.Data);
                return Ok(ApiResponse<CustomerId>.CreateSuccess(result.Data, "Customer created successfully"));
            }

            _logger.LogWarning("Failed to create customer: {Error}", result.Errors);
            return BadRequest(ApiResponse<object>.CreateFailure(result.Errors));
        }
    }
}

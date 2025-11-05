using CoreBankingTest.APP.Accounts.Queries.GetAccountDetails;
using CoreBankingTest.APP.Accounts.Queries.GetAccountSummary;
using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Models;
using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Customers.Queries.GetCustomer
{
    public record GetCustomersQuery : IQuery<List<CustomerDto>>;

    public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, Result<List<CustomerDto>>>
    {
        private readonly ICustomerRepository _customerRepository;

        public GetCustomersQueryHandler(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        public async Task<Result<List<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
        {

            var customers = await _customerRepository.GetAllAsync();

            var customerDtos = customers.Select(customer => new CustomerDto
            {
                CustomerId = customer.CustomerId,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Phone = customer.PhoneNumber,
                DateRegistered = customer.DateCreated,
                IsActive = customer.IsActive,
                Accounts = customer.Accounts.Select(account => new AccountSummaryDto
                {
                    AccountNumber = account.AccountNumber,
                    AccountType = account.AccountType.ToString(),
                    DisplayName = $"{customer.FirstName} {customer.LastName}",
                    Balance = account.Balance.Amount,
                    Currency = account.Balance.Currency,
                    IsActive = account.IsActive,
                    DateOpened = account.DateOpened
                }).ToList()
            }).ToList();

            return Result<List<CustomerDto>>.Success(customerDtos);
        }

    }
}

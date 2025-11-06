using CoreBankingTest.APP.Common.Interfaces;
using CoreBankingTest.APP.Common.Models;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Accounts.Queries.GetAccountDetails
{
    public record GetAccountDetailsQuery : IQuery<AccountDetailsDto>
    {
        public AccountNumber AccountNumber { get; init; }
    }

    public record AccountDetailsDto
    {
        public AccountNumber? AccountNumber { get; init; }
        public string AccountType { get; init; } = string.Empty;
        public Money Balance { get; init; } = new Money(0);
        public DateTime DateOpened { get; init; }
        public bool IsActive { get; init; }
        public string CustomerName { get; init; } = string.Empty;
    }

    public class GetAccountDetailsQueryHandler : IRequestHandler<GetAccountDetailsQuery, Result<AccountDetailsDto>>
    {
        private readonly IAccountRepository _accountRepository;

        public GetAccountDetailsQueryHandler(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

         public async Task<Result<AccountDetailsDto>> Handle(GetAccountDetailsQuery request, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByAccountNumberAsync(request.AccountNumber);

        if (account == null)
            return Result<AccountDetailsDto>.Failure("Account not found");

        if (account.Customer == null)
            return Result<AccountDetailsDto>.Failure("Account customer data not found");

        var dto = new AccountDetailsDto
        {
            AccountNumber = account.AccountNumber,
            AccountType = account.AccountType.ToString(),
            Balance = new Money(account.Balance.Amount, account.Balance.Currency),
            DateOpened = account.DateOpened,
            IsActive = account.IsActive,
            CustomerName = $"{account.Customer.FirstName} {account.Customer.LastName}"
        };

        return Result<AccountDetailsDto>.Success(dto);
    }
    }
}

using CoreBankingTest.CORE.Entities;
using CoreBankingTest.CORE.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Interfaces
{
    public interface ICustomerRepository
    {

        Task<Customer?> GetByIdAsync(CustomerId customerId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Customer>> GetAllAsync(CancellationToken cancellationToken = default);
        Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
        Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(CustomerId customerId, CancellationToken cancellationToken = default);

        Task<bool> EmailExistsAsync(string email);
    }
}

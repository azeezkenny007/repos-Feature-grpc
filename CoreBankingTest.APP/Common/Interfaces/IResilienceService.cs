using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Common.Interfaces
{
    public interface IResilienceService
    {
        Task<T> ExecuteWithResilienceAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default);

        Task<HttpResponseMessage> ExecuteHttpCallWithResilienceAsync(
            Func<CancellationToken, Task<HttpResponseMessage>> httpOperation,
            string operationName,
            CancellationToken cancellationToken = default);
    }
}

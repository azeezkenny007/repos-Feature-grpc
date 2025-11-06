using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.Common.Interfaces
{
    public interface IResilientHttpClientService
    {
        Task<TResponse> ExecuteWithResilienceAsync<TResponse>(
            Func<CancellationToken, Task<TResponse>> action,
            string operationName,
            CancellationToken cancellationToken = default);

        Task<HttpResponseMessage> ExecuteHttpRequestWithResilienceAsync(
            Func<Task<HttpResponseMessage>> request,
            string operationName,
            CancellationToken cancellationToken = default);
    }
}

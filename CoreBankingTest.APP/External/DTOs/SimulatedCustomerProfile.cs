using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.APP.External.DTOs
{
    public record SimulatedCustomerProfile(
        int BaseScore,
        decimal TotalDebt,
        int ActiveAccounts,
        int LatePayments,
        decimal CreditUtilization,
        int OldestAccountAgeMonths,
        string[] CreditFactors
    );

}

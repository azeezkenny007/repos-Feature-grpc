using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreBankingTest.CORE.Enums
{
    public enum TransactionType
    {
        Deposit = 1,
        Withdrawal = 2,
        Transfer = 3,
        Interest = 4,
        TransferIn = 5,
        TransferOut = 6,
        InterestCredit = 7
    }
}

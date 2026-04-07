using System.Collections.Generic;
using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

public interface IGalaChainClient
{
	Task<List<TokenBalanceModel>> FetchBalancesAsync(string ethAddress);
}

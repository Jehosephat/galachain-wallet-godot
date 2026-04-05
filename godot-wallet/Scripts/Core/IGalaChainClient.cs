using System.Collections.Generic;
using System.Threading.Tasks;

public interface IGalaChainClient
{
	Task<List<TokenBalanceModel>> FetchBalancesAsync(string ethAddress);
}

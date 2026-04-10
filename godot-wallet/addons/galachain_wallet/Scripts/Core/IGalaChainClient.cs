using System.Collections.Generic;
using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

public interface IGalaChainClient
{
	Task<NetworkResult<List<TokenBalanceModel>>> FetchBalancesAsync(string ethAddress);
	Task<NetworkResult<TransferPreviewResult>> DryRunTransferAsync(GalaTransferTokenRequest request);
	Task<NetworkResult<TransferPreviewResult>> DryRunBurnAsync(GalaBurnTokensRequest request, string signerAddress);
}

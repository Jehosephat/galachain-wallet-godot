using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

public interface IGalaTransferClient
{
	Task<NetworkResult<string>> TransferAsync(GalaTransferTokenRequest request, string walletAlias);
	Task<NetworkResult<string>> BurnTokensAsync(GalaBurnTokensRequest request);
}

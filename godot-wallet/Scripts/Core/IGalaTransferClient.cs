using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

public interface IGalaTransferClient
{
	Task<string> TransferAsync(GalaTransferTokenRequest request, string walletAlias);
}

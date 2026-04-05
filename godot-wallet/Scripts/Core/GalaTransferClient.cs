using System.Threading.Tasks;

public interface IGalaTransferClient
{
	Task<string> TransferAsync(GalaTransferTokenRequest request, string walletAlias);
}

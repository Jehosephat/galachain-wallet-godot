using System.Collections.Generic;
using System.Threading.Tasks;
using GalaWallet.Models;

namespace GalaWallet.Core;

public interface IWalletService
{
	bool HasWallet();
	bool IsUnlocked();
	string GetAddress();
	void CreateWallet(string password);
	void ImportPrivateKey(string privateKey, string password);
	void ImportMnemonic(string mnemonic, string password);
	bool Unlock(string password);
	void Lock();
	List<TokenBalanceModel> GetBalances();
	Task<NetworkResult<List<TokenBalanceModel>>> RefreshBalancesAsync();
	string ConsumePendingRecoveryPhrase();
	void LoadWalletMetadataIfPresent();
	ValidationResult ValidateTransfer(TransferDraft draft, decimal availableBalance);
	Task<NetworkResult<TransferPreviewResult>> PreviewTransferAsync(TransferDraft draft);
	Task<NetworkResult<string>> SubmitTransferAsync(TransferDraft draft);
	ValidationResult ValidateBurn(BurnDraft draft, decimal availableBalance);
	Task<NetworkResult<TransferPreviewResult>> PreviewBurnAsync(BurnDraft draft);
	Task<NetworkResult<string>> SubmitBurnAsync(BurnDraft draft);
	string SignMessage(string message);
}

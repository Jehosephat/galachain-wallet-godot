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
	Task RefreshBalancesAsync();
	string ConsumePendingRecoveryPhrase();
	void LoadWalletMetadataIfPresent();
	Task<TransferPreviewResult> PreviewTransferAsync(TransferDraft draft);
	Task SubmitTransferAsync(TransferDraft draft);
}

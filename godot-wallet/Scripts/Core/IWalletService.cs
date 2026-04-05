using System.Collections.Generic;

public interface IWalletService
{
	bool HasWallet();
	bool IsUnlocked();
	string GetAddress();
	void CreateWallet(string password);
	void ImportPrivateKey(string privateKey, string password);
	bool Unlock(string password);
	void Lock();
	List<TokenBalanceModel> GetBalances();

	string ConsumePendingRecoveryPhrase();
	
	void LoadWalletMetadataIfPresent();
}

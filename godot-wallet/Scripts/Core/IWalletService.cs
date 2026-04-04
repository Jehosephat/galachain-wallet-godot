using System.Collections.Generic;

public interface IWalletService
{
	bool HasWallet();
	bool IsUnlocked();
	string GetAddress();
	void CreateWallet();
	void ImportPrivateKey(string privateKey);
	bool Unlock(string password);
	void Lock();
	List<TokenBalanceModel> GetBalances();
}

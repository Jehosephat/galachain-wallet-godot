using GalaWallet.Models;

namespace GalaWallet.Core;

public interface IWalletStorage
{
	bool WalletExists();
	void Save(EncryptedWalletRecord record);
	EncryptedWalletRecord? Load();
	void Delete();
}

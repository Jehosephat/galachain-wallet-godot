public interface IWalletStorage
{
	bool WalletExists();
	void Save(EncryptedWalletRecord record);
	EncryptedWalletRecord? Load();
	void Delete();
}

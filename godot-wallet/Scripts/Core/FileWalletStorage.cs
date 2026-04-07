using Godot;
using System.Text.Json;
using GalaWallet.Models;

namespace GalaWallet.Core;

public class FileWalletStorage : IWalletStorage
{
	private const string WalletDirectoryPath = "user://wallet";
	private const string WalletFilePath = "user://wallet/wallet.json";

	public bool WalletExists()
	{
		return FileAccess.FileExists(WalletFilePath);
	}

	public void Save(EncryptedWalletRecord record)
	{
		DirAccess.MakeDirRecursiveAbsolute(WalletDirectoryPath);

		string json = JsonSerializer.Serialize(record, new JsonSerializerOptions
		{
			WriteIndented = true
		});

		using var file = FileAccess.Open(WalletFilePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			throw new System.InvalidOperationException("Failed to open wallet file for writing.");
		}

		file.StoreString(json);
	}

	public EncryptedWalletRecord? Load()
	{
		if (!WalletExists())
			return null;

		using var file = FileAccess.Open(WalletFilePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			throw new System.InvalidOperationException("Failed to open wallet file for reading.");
		}

		string json = file.GetAsText();
		return JsonSerializer.Deserialize<EncryptedWalletRecord>(json);
	}

	public void Delete()
	{
		if (WalletExists())
		{
			DirAccess.RemoveAbsolute(WalletFilePath);
		}
	}
}

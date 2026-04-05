using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class PasswordCryptoService
{
	private const int SaltSize = 16;
	private const int KeySize = 32;   // 256-bit AES key
	private const int NonceSize = 12; // standard GCM nonce size
	private const int TagSize = 16;   // 128-bit tag

	public EncryptedWalletRecord EncryptSecret(
		string address,
		WalletSecretType secretType,
		string secret,
		string password,
		int iterations = 100_000)
	{
		byte[] salt = new byte[SaltSize];
		RandomNumberGenerator.Fill(salt);

		byte[] nonce = new byte[NonceSize];
		RandomNumberGenerator.Fill(nonce);

		byte[] key = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			iterations,
			HashAlgorithmName.SHA256,
			KeySize);

		var payload = new WalletSecretPayload
		{
			SecretType = secretType,
			Secret = secret
		};

		string payloadJson = JsonSerializer.Serialize(payload);
		byte[] plaintext = Encoding.UTF8.GetBytes(payloadJson);

		byte[] ciphertext = new byte[plaintext.Length];
		byte[] tag = new byte[TagSize];

		using (var aes = new AesGcm(key, TagSize))
		{
			aes.Encrypt(nonce, plaintext, ciphertext, tag);
		}

		CryptographicOperations.ZeroMemory(key);
		CryptographicOperations.ZeroMemory(plaintext);

		return new EncryptedWalletRecord
		{
			Version = 1,
			Address = address,
			SecretType = secretType,
			KdfIterations = iterations,
			SaltBase64 = Convert.ToBase64String(salt),
			NonceBase64 = Convert.ToBase64String(nonce),
			TagBase64 = Convert.ToBase64String(tag),
			CipherTextBase64 = Convert.ToBase64String(ciphertext)
		};
	}

	public WalletSecretPayload DecryptSecret(EncryptedWalletRecord record, string password)
	{
		byte[] salt = Convert.FromBase64String(record.SaltBase64);
		byte[] nonce = Convert.FromBase64String(record.NonceBase64);
		byte[] tag = Convert.FromBase64String(record.TagBase64);
		byte[] ciphertext = Convert.FromBase64String(record.CipherTextBase64);

		byte[] key = Rfc2898DeriveBytes.Pbkdf2(
			password,
			salt,
			record.KdfIterations,
			HashAlgorithmName.SHA256,
			KeySize);

		byte[] plaintext = new byte[ciphertext.Length];

		try
		{
			using (var aes = new AesGcm(key, tag.Length))
			{
				aes.Decrypt(nonce, ciphertext, tag, plaintext);
			}

			string payloadJson = Encoding.UTF8.GetString(plaintext);
			var payload = JsonSerializer.Deserialize<WalletSecretPayload>(payloadJson);

			if (payload == null)
				throw new InvalidOperationException("Decrypted wallet payload was null.");

			return payload;
		}
		finally
		{
			CryptographicOperations.ZeroMemory(key);
			CryptographicOperations.ZeroMemory(plaintext);
		}
	}
}

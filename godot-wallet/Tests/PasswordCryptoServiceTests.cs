using GalaWallet.Core;
using GalaWallet.Models;

namespace GalaWallet.Tests;

public class PasswordCryptoServiceTests
{
    private readonly PasswordCryptoService _crypto = new();

    [Fact]
    public void EncryptThenDecrypt_RoundTrips_Mnemonic()
    {
        string secret = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        string password = "test-password-123";

        var record = _crypto.EncryptSecret("0xABC", WalletSecretType.Mnemonic, secret, password);
        var payload = _crypto.DecryptSecret(record, password);

        Assert.Equal(WalletSecretType.Mnemonic, payload.SecretType);
        Assert.Equal(secret, payload.Secret);
    }

    [Fact]
    public void EncryptThenDecrypt_RoundTrips_PrivateKey()
    {
        string secret = "0x4c0883a69102937d6231471b5dbb6204fe512961708279f703c9e48b76a8e324";
        string password = "my-strong-password";

        var record = _crypto.EncryptSecret("0xDEF", WalletSecretType.PrivateKey, secret, password);
        var payload = _crypto.DecryptSecret(record, password);

        Assert.Equal(WalletSecretType.PrivateKey, payload.SecretType);
        Assert.Equal(secret, payload.Secret);
    }

    [Fact]
    public void Decrypt_WithWrongPassword_Throws()
    {
        string secret = "some secret data";
        string correctPassword = "correct-password";
        string wrongPassword = "wrong-password";

        var record = _crypto.EncryptSecret("0x123", WalletSecretType.Mnemonic, secret, correctPassword);

        Assert.ThrowsAny<Exception>(() => _crypto.DecryptSecret(record, wrongPassword));
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachTime()
    {
        string secret = "same secret";
        string password = "same password";

        var record1 = _crypto.EncryptSecret("0x111", WalletSecretType.Mnemonic, secret, password);
        var record2 = _crypto.EncryptSecret("0x111", WalletSecretType.Mnemonic, secret, password);

        // Different salt and nonce each time
        Assert.NotEqual(record1.SaltBase64, record2.SaltBase64);
        Assert.NotEqual(record1.NonceBase64, record2.NonceBase64);
        Assert.NotEqual(record1.CipherTextBase64, record2.CipherTextBase64);
    }

    [Fact]
    public void Encrypt_SetsRecordMetadata()
    {
        var record = _crypto.EncryptSecret("0xABC", WalletSecretType.Mnemonic, "secret", "pass");

        Assert.Equal(1, record.Version);
        Assert.Equal("0xABC", record.Address);
        Assert.Equal(WalletSecretType.Mnemonic, record.SecretType);
        Assert.Equal(100_000, record.KdfIterations);
        Assert.False(string.IsNullOrWhiteSpace(record.SaltBase64));
        Assert.False(string.IsNullOrWhiteSpace(record.NonceBase64));
        Assert.False(string.IsNullOrWhiteSpace(record.TagBase64));
        Assert.False(string.IsNullOrWhiteSpace(record.CipherTextBase64));
    }

    [Fact]
    public void Encrypt_WithCustomIterations_PreservesInRecord()
    {
        var record = _crypto.EncryptSecret("0x000", WalletSecretType.PrivateKey, "key", "pass", iterations: 50_000);

        Assert.Equal(50_000, record.KdfIterations);

        // Should still decrypt correctly
        var payload = _crypto.DecryptSecret(record, "pass");
        Assert.Equal("key", payload.Secret);
    }
}

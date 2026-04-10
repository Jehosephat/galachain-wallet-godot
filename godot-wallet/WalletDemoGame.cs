using Godot;
using GalaWallet.Core;

public partial class WalletDemoGame : Control
{
	private WalletFacade _walletFacade = null!;
	private Button _openWalletButton = null!;
	private Button _closeWalletButton = null!;
	private Button _makePurchaseButton = null!;
	private Button _burnGalaButton = null!;
	private Button _loginButton = null!;
	private Control _walletMount = null!;
	private Label _gameStatusLabel = null!;
	private Label _gameBalanceLabel = null!;

	public override void _Ready()
	{
		_walletFacade = new WalletFacade();

		_openWalletButton = GetNode<Button>("%OpenWalletButton");
		_closeWalletButton = GetNode<Button>("%CloseWalletButton");
		_makePurchaseButton = GetNode<Button>("%MakePurchaseButton");
		_burnGalaButton = GetNode<Button>("%BurnGalaButton");
		_loginButton = GetNode<Button>("%LoginButton");
		_walletMount = GetNode<Control>("%WalletMount");
		_gameStatusLabel = GetNode<Label>("%GameStatusLabel");
		_gameBalanceLabel = GetNode<Label>("%GameBalanceLabel");

		_openWalletButton.Pressed += OnOpenWalletPressed;
		_closeWalletButton.Pressed += OnCloseWalletPressed;
		_makePurchaseButton.Pressed += OnMakePurchasePressed;
		_burnGalaButton.Pressed += OnBurnGalaPressed;
		_loginButton.Pressed += OnLoginPressed;

		_walletFacade.WalletCreated += _ => UpdateStatus();
		_walletFacade.WalletImported += _ => UpdateStatus();
		_walletFacade.WalletUnlocked += _ => UpdateStatus();
		_walletFacade.WalletLocked += UpdateStatus;
		_walletFacade.BalancesRefreshed += UpdateBalanceDisplay;
		_walletFacade.TransferCompleted += (to, qty, sym) =>
		{
			GD.Print($"[Demo] Transfer completed: {qty} {sym} to {to}");
		};
		_walletFacade.TransferFailed += err =>
		{
			GD.Print($"[Demo] Transfer failed: {err}");
		};
		_walletFacade.BurnCompleted += (qty, sym) =>
		{
			GD.Print($"[Demo] Burn completed: {qty} {sym}");
		};
		_walletFacade.BurnFailed += err =>
		{
			GD.Print($"[Demo] Burn failed: {err}");
		};
		_walletFacade.MessageSigned += (msg, sig, addr) =>
		{
			GD.Print($"[Demo] Login signature received");
			GD.Print($"  address:   {addr}");
			GD.Print($"  signature: {sig}");
			GD.Print("  (Send these to your game server, which verifies the signature recovers to the address.)");
		};
		_walletFacade.MessageSignDeclined += () =>
		{
			GD.Print("[Demo] Login declined by user.");
		};

		UpdateStatus();
	}

	private void OnOpenWalletPressed()
	{
		_walletFacade.OpenWallet(_walletMount);
		UpdateStatus();
	}

	private void OnCloseWalletPressed()
	{
		_walletFacade.CloseWallet();
		UpdateStatus();
	}

	private void OnMakePurchasePressed()
	{
		_walletFacade.OpenWallet(_walletMount);
		_walletFacade.RequestTransfer("client|5f58d8641586e117c5e68834", "15", "GALA");
		UpdateStatus();
	}

	private void OnBurnGalaPressed()
	{
		_walletFacade.OpenWallet(_walletMount);
		_walletFacade.RequestBurn("5", "GALA");
		UpdateStatus();
	}

	private void OnLoginPressed()
	{
		_walletFacade.OpenWallet(_walletMount);

		// A real game server would generate this nonce and timestamp and give it
		// to the client. The server then verifies the returned signature recovers
		// to the expected address.
		string nonce = System.Guid.NewGuid().ToString("N").Substring(0, 16);
		string timestamp = System.DateTime.UtcNow.ToString("o");
		string message =
			"Sign in to WalletDemoGame\n" +
			$"Nonce: {nonce}\n" +
			$"Timestamp: {timestamp}";

		_walletFacade.RequestSignMessage(message);
		UpdateStatus();
	}

	private void UpdateStatus()
	{
		if (!_walletFacade.HasWallet())
		{
			_gameStatusLabel.Text = "Game sees: no wallet";
			_gameBalanceLabel.Text = "";
			return;
		}

		_gameStatusLabel.Text = _walletFacade.IsUnlocked()
			? $"Game sees unlocked wallet: {_walletFacade.GetCurrentAddress()}"
			: $"Game sees locked wallet: {_walletFacade.GetCurrentAddress()}";

		UpdateBalanceDisplay();
	}

	private void UpdateBalanceDisplay()
	{
		var balances = _walletFacade.GetBalances();

		foreach (var b in balances)
		{
			if (string.Equals(b.Symbol, "GALA", System.StringComparison.OrdinalIgnoreCase))
			{
				_gameBalanceLabel.Text = $"GALA Balance: {b.AvailableAmount:0.########}";
				return;
			}
		}

		_gameBalanceLabel.Text = "";
	}
}

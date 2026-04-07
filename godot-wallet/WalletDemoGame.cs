using Godot;

public partial class WalletDemoGame : Control
{
	private WalletFacade _walletFacade = null!;
	private Button _openWalletButton = null!;
	private Button _closeWalletButton = null!;
	private Control _walletMount = null!;
	private Label _gameStatusLabel = null!;

	public override void _Ready()
	{
		_walletFacade = new WalletFacade();

		_openWalletButton = GetNode<Button>("%OpenWalletButton");
		_closeWalletButton = GetNode<Button>("%CloseWalletButton");
		_walletMount = GetNode<Control>("%WalletMount");
		_gameStatusLabel = GetNode<Label>("%GameStatusLabel");

		_openWalletButton.Pressed += OnOpenWalletPressed;
		_closeWalletButton.Pressed += OnCloseWalletPressed;

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

	private void UpdateStatus()
	{
		if (!_walletFacade.HasWallet())
		{
			_gameStatusLabel.Text = "Game sees: no wallet";
			return;
		}

		_gameStatusLabel.Text = _walletFacade.IsUnlocked()
			? $"Game sees unlocked wallet: {_walletFacade.GetCurrentAddress()}"
			: $"Game sees locked wallet: {_walletFacade.GetCurrentAddress()}";
	}
}

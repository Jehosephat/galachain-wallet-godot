using Godot;

namespace GalaWallet;

[Tool]
public partial class WalletPlugin : EditorPlugin
{
	private const string AutoloadName = "Wallet";
	private const string BridgeScriptPath = "res://addons/galachain_wallet/Scripts/Core/WalletBridge.cs";

	public override void _EnterTree()
	{
		AddAutoloadSingleton(AutoloadName, BridgeScriptPath);
	}

	public override void _ExitTree()
	{
		RemoveAutoloadSingleton(AutoloadName);
	}
}

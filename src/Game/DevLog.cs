using Godot;

namespace ConnectFour.Game;

/// <summary>
/// Timestamped developer log, off unless the game is launched with `--log`. Used to diagnose
/// timing and sound problems on real devices, where the output shows up in `adb logcat`.
/// </summary>
public static class DevLog
{
	public static bool Enabled { get; set; }

	public static void Write(string message)
	{
		if (Enabled)
			GD.Print($"[cf {Time.GetTicksMsec()}] {message}");
	}
}

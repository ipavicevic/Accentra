using System.Runtime.InteropServices;

namespace Accentra;

static class CharacterInjector
{
    public static void Replace(char c)
    {
        var inputs = new NativeMethods.INPUT[]
        {
            KeyInput(NativeMethods.VK_BACK, 0, 0),
            KeyInput(NativeMethods.VK_BACK, 0, NativeMethods.KEYEVENTF_KEYUP),
            KeyInput(0, (ushort)c, NativeMethods.KEYEVENTF_UNICODE),
            KeyInput(0, (ushort)c, NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP),
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT KeyInput(ushort vk, ushort scan, uint flags) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        ki = new() { wVk = vk, wScan = scan, dwFlags = flags, dwExtraInfo = NativeMethods.AccentraSentinel }
    };
}

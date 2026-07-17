using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Tww3Companion.Desktop.Startup;

[SupportedOSPlatform("windows")]
public sealed partial class NativeStartupNotification : IStartupNotification
{
    private const uint ErrorDialog = 0x00000010 | 0x00002000 | 0x00010000;

    public void ShowBlockingError(string message) =>
        NativeMethods.MessageBox(IntPtr.Zero, message, "TWW3 Companion", ErrorDialog);

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
        public static partial int MessageBox(IntPtr window, string text, string caption, uint type);
    }
}

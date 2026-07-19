using System.Runtime.InteropServices;

namespace Tww3Companion.Desktop.Startup;

public partial class NativeStartupDialog : IStartupNotification
{
  private const uint ErrorDialog = 0x00000010 | 0x00002000 | 0x00010000;

  public virtual void ShowBlockingError(string message)
  {
    if (OperatingSystem.IsWindows())
    {
      NativeMethods.MessageBox(IntPtr.Zero, message, "TWW3 Companion", ErrorDialog);
    }
  }

  private static partial class NativeMethods
  {
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBox(IntPtr window, string text, string caption, uint type);
  }
}

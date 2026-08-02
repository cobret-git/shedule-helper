using System.Runtime.InteropServices;

namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// Copies text to the Windows clipboard via a minimal user32/kernel32 P/Invoke - not worth a
    /// NuGet dependency for a single "copy this path" action.
    /// </summary>
    public static class Clipboard
    {
        #region Fields

        private const uint CfUnicodeText = 13;
        private const uint GmemMoveable = 0x0002;

        #endregion

        #region Methods

        /// <summary>
        /// Places <paramref name="text"/> on the clipboard as Unicode text.
        /// </summary>
        /// <returns><see langword="true"/> if the clipboard was successfully updated; otherwise, <see langword="false"/> (e.g. another process is holding it open).</returns>
        public static bool TrySetText(string text)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                return false;
            }

            try
            {
                EmptyClipboard();

                var byteCount = (text.Length + 1) * 2; // UTF-16 code units, plus a null terminator
                var handle = GlobalAlloc(GmemMoveable, (UIntPtr)byteCount);
                if (handle == IntPtr.Zero)
                {
                    return false;
                }

                var target = GlobalLock(handle);
                if (target == IntPtr.Zero)
                {
                    GlobalFree(handle);
                    return false;
                }

                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                    Marshal.WriteInt16(target, text.Length * 2, 0);
                }
                finally
                {
                    GlobalUnlock(handle);
                }

                if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
                {
                    GlobalFree(handle);
                    return false;
                }

                // The clipboard now owns `handle` - it frees it, so we must not.
                return true;
            }
            finally
            {
                CloseClipboard();
            }
        }

        #endregion

        #region Native Methods

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        #endregion
    }
}

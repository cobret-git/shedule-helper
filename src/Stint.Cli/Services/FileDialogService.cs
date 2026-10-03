using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Stint.Cli.Services
{
    /// <summary>
    /// Default <see cref="IFileDialogService"/>: the classic Win32 common file dialogs
    /// (<c>GetSaveFileName</c>/<c>GetOpenFileName</c>). Windows only, like the rest of the app's console setup.
    /// </summary>
    public sealed class FileDialogService : IFileDialogService
    {
        #region Fields

        // OPENFILENAME wants "description\0pattern\0...\0\0" - the final extra terminator comes
        // from marshalling the string, so this ends with just one.
        private const string DatabaseFilter = "Stint database (*.db)\0*.db\0All files (*.*)\0*.*\0";
        private const string DefaultExtension = "db";

        // The longest path Windows allows is 32,767 characters, so the dialog can never be handed a buffer it overflows.
        private const int PathBufferChars = 32768;

        private const uint OfnOverwritePrompt = 0x00000002;
        private const uint OfnHideReadOnly = 0x00000004;
        private const uint OfnNoChangeDir = 0x00000008;
        private const uint OfnPathMustExist = 0x00000800;
        private const uint OfnFileMustExist = 0x00001000;
        private const uint OfnExplorer = 0x00080000;

        #endregion

        #region Methods

        /// <inheritdoc />
        public string? PickFileToSave(string title, string suggestedFileName)
        {
            const uint flags = OfnExplorer | OfnPathMustExist | OfnOverwritePrompt | OfnHideReadOnly | OfnNoChangeDir;
            return RunOnStaThread(() => ShowDialog(isSave: true, title, suggestedFileName, flags));
        }

        /// <inheritdoc />
        public string? PickFileToOpen(string title)
        {
            const uint flags = OfnExplorer | OfnPathMustExist | OfnFileMustExist | OfnHideReadOnly | OfnNoChangeDir;
            return RunOnStaThread(() => ShowDialog(isSave: false, title, suggestedFileName: null, flags));
        }

        #endregion

        #region Helpers

        // The shell's dialog code expects a single-threaded-apartment thread; the app's own
        // thread (async Main's continuations) is a thread-pool one, so borrow a dedicated STA thread.
        private static string? RunOnStaThread(Func<string?> action)
        {
            string? result = null;
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return result;
        }

        private static string? ShowDialog(bool isSave, string title, string? suggestedFileName, uint flags)
        {
            var fileBuffer = Marshal.AllocHGlobal(PathBufferChars * sizeof(char));
            var filter = Marshal.StringToHGlobalUni(DatabaseFilter);
            var dialogTitle = Marshal.StringToHGlobalUni(title);
            var defaultExtension = isSave ? Marshal.StringToHGlobalUni(DefaultExtension) : IntPtr.Zero;

            try
            {
                var suggested = (suggestedFileName ?? string.Empty).ToCharArray();
                Marshal.Copy(suggested, 0, fileBuffer, suggested.Length);
                Marshal.WriteInt16(fileBuffer, suggested.Length * sizeof(char), 0);

                var dialog = new OpenFileName
                {
                    lStructSize = (uint)Marshal.SizeOf<OpenFileName>(),
                    // The terminal window that's in front right now - GetConsoleWindow() is a
                    // hidden pseudo-window under Windows Terminal and would leave the dialog behind it.
                    hwndOwner = GetForegroundWindow(),
                    lpstrFilter = filter,
                    nFilterIndex = 1,
                    lpstrFile = fileBuffer,
                    nMaxFile = PathBufferChars,
                    lpstrTitle = dialogTitle,
                    Flags = flags,
                    lpstrDefExt = defaultExtension
                };

                var accepted = isSave ? GetSaveFileName(ref dialog) : GetOpenFileName(ref dialog);
                if (accepted)
                {
                    return Marshal.PtrToStringUni(fileBuffer);
                }

                // Zero means the user just closed it; anything else is a real failure.
                var error = CommDlgExtendedError();
                return error == 0
                    ? null
                    : throw new InvalidOperationException($"The file dialog failed (error 0x{error:X}).");
            }
            finally
            {
                Marshal.FreeHGlobal(fileBuffer);
                Marshal.FreeHGlobal(filter);
                Marshal.FreeHGlobal(dialogTitle);
                if (defaultExtension != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(defaultExtension);
                }
            }
        }

        #endregion

        #region Native Methods

        [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", CharSet = CharSet.Unicode)]
        private static extern bool GetSaveFileName(ref OpenFileName dialog);

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileName(ref OpenFileName dialog);

        [DllImport("comdlg32.dll")]
        private static extern uint CommDlgExtendedError();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        #endregion

        #region Structures

        // OPENFILENAMEW. Every string is a raw pointer to memory this class allocates and frees
        // itself, which keeps the struct blittable and lets the filter keep its embedded nulls.
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public uint lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public IntPtr lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public uint nMaxCustFilter;
            public uint nFilterIndex;
            public IntPtr lpstrFile;
            public uint nMaxFile;
            public IntPtr lpstrFileTitle;
            public uint nMaxFileTitle;
            public IntPtr lpstrInitialDir;
            public IntPtr lpstrTitle;
            public uint Flags;
            public ushort nFileOffset;
            public ushort nFileExtension;
            public IntPtr lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
            public IntPtr pvReserved;
            public uint dwReserved;
            public uint FlagsEx;
        }

        #endregion
    }
}

using System.Runtime.InteropServices;
using System.Text;

namespace SandBox;

/// <summary>
/// Console preparation for the sample host: guarantees a visible console, makes its text UTF-8 and
/// turns off the Windows quick-edit mode so selecting text cannot freeze console output.
/// </summary>
/// <remarks>
/// Every Windows call is best effort: a failure (no console, redirected stream, restricted handle)
/// only costs the feature, never the startup. The resulting state is part of the host banner and the
/// log file, so a run without a visible console stays diagnosable.
/// </remarks>
internal static class ConsoleSetup
{
    private const int StdInputHandle = -10;

    // Quick edit is an input-mode flag: when it is set, selecting text in the console window pauses
    // every writer until the selection is cleared. The extended-flags bit must be set for the
    // console to honour the cleared quick-edit bit.
    private const uint EnableQuickEditMode = 0x0040;
    private const uint EnableExtendedFlags = 0x0080;

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;

    /// <summary>
    /// Prepares the console for the sample: allocates one when missing, switches it to UTF-8, sets the
    /// window title and disables quick edit.
    /// </summary>
    /// <returns>Short human-readable state for the host banner (never throws).</returns>
    internal static string Initialize()
    {
        var state = new List<string>(5);

        if (!OperatingSystem.IsWindows())
        {
            state.Add("non-Windows console (quick-edit patch skipped)");
            return string.Join("; ", state);
        }

        EnsureConsole(state);
        SetEncodingAndTitle(state);
        state.Add(ApplyQuickEditOff());

        return string.Join("; ", state);
    }

    /// <summary>
    /// Allocates a console when the process has none (a WinExe or a future publishing change) and
    /// re-opens the standard streams; an inherited console is never duplicated.
    /// </summary>
    private static void EnsureConsole(List<string> state)
    {
        try
        {
            if (GetConsoleWindow() != IntPtr.Zero)
            {
                state.Add("attached");
                return;
            }

            if (!AllocConsole())
            {
                state.Add("attach failed");
                return;
            }

            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
            state.Add("allocated");
        }
        catch (Exception)
        {
            state.Add("console probe failed");
        }
    }

    private static void SetEncodingAndTitle(List<string> state)
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.Title = "SilkEngine SandBox";
            state.Add("utf-8");
        }
        catch (Exception)
        {
            state.Add("encoding patch skipped");
        }
    }

    /// <summary>
    /// Clears <c>ENABLE_QUICK_EDIT_MODE</c> and sets <c>ENABLE_EXTENDED_FLAGS</c> on the console input
    /// buffer. The flag lives in the console input mode, and that mode is shared by the whole screen
    /// buffer, so the patch applies to the window the user selects text in.
    /// </summary>
    private static string ApplyQuickEditOff()
    {
        IntPtr stdin = GetStdHandle(StdInputHandle);
        if (TryGetMode(stdin, out uint inputMode))
            return Patch(stdin, "stdin", inputMode);

        // A redirected stdin still belongs to the console window: reach the input buffer by name.
        IntPtr consoleInput = IntPtr.Zero;
        try
        {
            consoleInput = CreateFileW(
                "CONIN$",
                GenericRead | GenericWrite,
                ShareReadWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            if (!TryGetMode(consoleInput, out uint mode))
                return "no console input buffer (quick-edit patch skipped)";

            return Patch(consoleInput, "CONIN$", mode);
        }
        catch (Exception)
        {
            return "console input unavailable (quick-edit patch skipped)";
        }
        finally
        {
            if (consoleInput != IntPtr.Zero && consoleInput != new IntPtr(-1))
                CloseHandle(consoleInput);
        }
    }

    private static string Patch(IntPtr handle, string target, uint current)
    {
        uint updated = (current | EnableExtendedFlags) & ~EnableQuickEditMode;
        if (SetConsoleMode(handle, updated))
            return $"quick-edit off ({target} 0x{current:X4} -> 0x{updated:X4})";

        return $"quick-edit patch failed ({target} 0x{current:X4}, win32 {Marshal.GetLastPInvokeError()})";
    }

    private static bool TryGetMode(IntPtr handle, out uint mode)
    {
        mode = 0;
        try
        {
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return false;

            return GetConsoleMode(handle, out mode);
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}

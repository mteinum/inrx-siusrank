using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace InrxToSiusRank;

public static class ClipboardService
{
    public static void SetText(string text)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WindowsClipboard.SetText(text);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            WriteToCommand("/usr/bin/pbcopy", text);
            return;
        }

        if (TryWriteToCommand("wl-copy", text) || TryWriteToCommand("xclip", text, ["-selection", "clipboard"]))
        {
            return;
        }

        throw new InvalidOperationException("Clipboard is only supported on Windows, macOS, or Linux with wl-copy/xclip.");
    }

    private static bool TryWriteToCommand(string fileName, string text, string[]? arguments = null)
    {
        try
        {
            WriteToCommand(fileName, text, arguments);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static void WriteToCommand(string fileName, string text, string[]? arguments = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments ?? [])
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        process.StandardInput.Write(text);
        process.StandardInput.Close();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Clipboard command '{fileName}' failed: {error}");
        }
    }

    private static class WindowsClipboard
    {
        private const uint CfUnicodeText = 13;
        private const uint GmemMoveable = 0x0002;

        public static void SetText(string text)
        {
            OpenClipboardWithRetry();

            var handle = IntPtr.Zero;
            try
            {
                EmptyClipboard();
                var bytes = Encoding.Unicode.GetBytes(text + '\0');
                handle = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
                if (handle == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var target = GlobalLock(handle);
                if (target == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    Marshal.Copy(bytes, 0, target, bytes.Length);
                }
                finally
                {
                    GlobalUnlock(handle);
                }

                if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                handle = IntPtr.Zero;
            }
            finally
            {
                CloseClipboard();
                if (handle != IntPtr.Zero)
                {
                    GlobalFree(handle);
                }
            }
        }

        private static void OpenClipboardWithRetry()
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    return;
                }

                Thread.Sleep(50);
            }

            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);
    }
}

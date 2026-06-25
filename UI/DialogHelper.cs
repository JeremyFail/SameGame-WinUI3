using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SameGame.Model;
using WinRT.Interop;

namespace SameGame.UI;

public static class DialogHelper
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseeventfMove = 0x0001;
    private const uint MouseeventfLeftdown = 0x0002;
    private const uint MouseeventfLeftup = 0x0004;
    private const uint MouseeventfAbsolute = 0x8000;
    private const uint KeyeventfKeyup = 0x0002;
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;
    private const ushort VkEscape = 0x1B;

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref PointNative point);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int metric);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

    public static void ApplyTheme(FrameworkElement element)
    {
        ThemeHelper.ApplyTheme(App.CurrentUiTheme, element);
        ThemeResources.ApplyChrome(element);
    }

    public static async Task PrepareForModalDialogAsync()
    {
        var window = App.MainWindowContent;
        if (window is null)
        {
            return;
        }

        // If the crash were to happen inside a menu click handler, wait until that unwinds.
        var frameReady = new TaskCompletionSource();
        window.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => frameReady.SetResult());
        await frameReady.Task;
        await Task.Yield();

        window.Activate();
        BringWindowToForeground(window);

        if (window.Content is MainPage mainPage)
        {
            mainPage.PrepareForFatalErrorDialog();
            if (mainPage.TryGetMenuDismissPoint(out Windows.Foundation.Point dismissPoint))
            {
                SimulateClientClick(window, dismissPoint);
            }
        }

        DismissOpenMenus(window);
        if (window.Content is DependencyObject root)
        {
            DismissOpenOverlays(root);
        }

        await Task.Delay(50);

        DismissOpenMenus(window);
        if (window.Content is DependencyObject refreshedRoot)
        {
            DismissOpenOverlays(refreshedRoot);
        }
    }

    private static void BringWindowToForeground(Window window)
    {
        SetForegroundWindow(WindowNative.GetWindowHandle(window));
    }

    private static void DismissOpenMenus(Window window)
    {
        SendEscapeKeys();
        var hwnd = WindowNative.GetWindowHandle(window);
        const uint wmKeyDown = 0x0100;
        for (int i = 0; i < 3; i++)
        {
            PostMessage(hwnd, wmKeyDown, (IntPtr)VkEscape, IntPtr.Zero);
        }
    }

    private static void SendEscapeKeys(int count = 3)
    {
        for (int i = 0; i < count; i++)
        {
            var inputs = new INPUT[2];
            inputs[0].type = InputKeyboard;
            inputs[0].U.ki.wVk = VkEscape;
            inputs[1].type = InputKeyboard;
            inputs[1].U.ki.wVk = VkEscape;
            inputs[1].U.ki.dwFlags = KeyeventfKeyup;
            SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }
    }

    private static void SimulateClientClick(Window window, Windows.Foundation.Point clientPoint)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var screenPoint = new PointNative
        {
            X = (int)clientPoint.X,
            Y = (int)clientPoint.Y
        };
        if (!ClientToScreen(hwnd, ref screenPoint))
        {
            return;
        }

        int screenWidth = GetSystemMetrics(SmCxScreen);
        int screenHeight = GetSystemMetrics(SmCyScreen);
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return;
        }

        int absoluteX = (screenPoint.X * 65535) / screenWidth;
        int absoluteY = (screenPoint.Y * 65535) / screenHeight;

        var inputs = new INPUT[3];
        inputs[0].type = InputMouse;
        inputs[0].U.mi.dwFlags = MouseeventfAbsolute | MouseeventfMove;
        inputs[0].U.mi.dx = absoluteX;
        inputs[0].U.mi.dy = absoluteY;

        inputs[1].type = InputMouse;
        inputs[1].U.mi.dwFlags = MouseeventfAbsolute | MouseeventfLeftdown;
        inputs[1].U.mi.dx = absoluteX;
        inputs[1].U.mi.dy = absoluteY;

        inputs[2].type = InputMouse;
        inputs[2].U.mi.dwFlags = MouseeventfAbsolute | MouseeventfLeftup;
        inputs[2].U.mi.dx = absoluteX;
        inputs[2].U.mi.dy = absoluteY;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void DismissOpenOverlays(DependencyObject node)
    {
        if (node is Popup { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }

        if (node is FlyoutBase { IsOpen: true } flyout)
        {
            flyout.Hide();
        }

        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            DismissOpenOverlays(VisualTreeHelper.GetChild(node, i));
        }
    }

    public static ContentDialog CreateDialog(string title, object content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            XamlRoot = App.DialogXamlRoot
        };
        ApplyTheme(dialog);
        return dialog;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}

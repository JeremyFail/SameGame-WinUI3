using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SameGame.Model;
using WinRT.Interop;

namespace SameGame.UI;

/// <summary>
/// Helpers for creating themed dialogs and preparing the main window for modal display.
/// </summary>
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

    /// <summary>
    /// Posts a Windows message to the specified window handle.
    /// </summary>
    /// <param name="hWnd">The target window handle.</param>
    /// <param name="msg">The message identifier.</param>
    /// <param name="wParam">The message wParam value.</param>
    /// <param name="lParam">The message lParam value.</param>
    /// <returns><see langword="true"/> if the message was posted successfully.</returns>
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Brings the specified window to the foreground.
    /// </summary>
    /// <param name="hWnd">The window handle to activate.</param>
    /// <returns><see langword="true"/> if the window was brought to the foreground.</returns>
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Converts client-area coordinates to screen coordinates.
    /// </summary>
    /// <param name="hWnd">The window handle.</param>
    /// <param name="point">The client point to convert; updated in place with screen coordinates.</param>
    /// <returns><see langword="true"/> if the conversion succeeded.</returns>
    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref PointNative point);

    /// <summary>
    /// Retrieves a system metric value.
    /// </summary>
    /// <param name="metric">The system metric index.</param>
    /// <returns>The metric value in pixels.</returns>
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int metric);

    /// <summary>
    /// Synthesizes keyboard or mouse input events.
    /// </summary>
    /// <param name="inputCount">The number of input structures in the array.</param>
    /// <param name="inputs">The array of input events to send.</param>
    /// <param name="inputSize">The size of one <see cref="INPUT"/> structure in bytes.</param>
    /// <returns>The number of events successfully inserted into the input stream.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

    /// <summary>
    /// Applies the current app theme and chrome styling to a framework element.
    /// </summary>
    /// <param name="element">The element to theme.</param>
    public static void ApplyTheme(FrameworkElement element)
    {
        ThemeHelper.ApplyTheme(App.CurrentUiTheme, element);
        ThemeResources.ApplyChrome(element);
    }

    /// <summary>
    /// Prepares the main window for a modal dialog by dismissing menus, flyouts, and overlays.
    /// </summary>
    /// <returns>A task that completes when the window is ready for modal display.</returns>
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

        // Second pass after layout settles.
        DismissOpenMenus(window);
        if (window.Content is DependencyObject refreshedRoot)
        {
            DismissOpenOverlays(refreshedRoot);
        }
    }

    /// <summary>
    /// Brings the given window to the foreground using its native handle.
    /// </summary>
    /// <param name="window">The window to activate.</param>
    private static void BringWindowToForeground(Window window)
    {
        SetForegroundWindow(WindowNative.GetWindowHandle(window));
    }

    /// <summary>
    /// Sends Escape key input and posts Escape key-down messages to dismiss open menus.
    /// </summary>
    /// <param name="window">The window whose menu bar should receive dismiss messages.</param>
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

    /// <summary>
    /// Synthesizes Escape key press-and-release events via SendInput.
    /// </summary>
    /// <param name="count">The number of Escape key sequences to send.</param>
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

    /// <summary>
    /// Synthesizes a left-click at a client-coordinate point within the window.
    /// </summary>
    /// <param name="window">The target window.</param>
    /// <param name="clientPoint">The click location in client coordinates.</param>
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

        // Convert to absolute mouse coordinates (0–65535).
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

    /// <summary>
    /// Recursively closes open popups and flyouts in the visual tree.
    /// </summary>
    /// <param name="node">The root node of the subtree to inspect.</param>
    private static void DismissOpenOverlays(DependencyObject node)
    {
        // Close any open popup at this node.
        if (node is Popup { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }

        // Hide any open flyout attached to this element.
        if (node is FlyoutBase { IsOpen: true } flyout)
        {
            flyout.Hide();
        }

        // Walk children depth-first.
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            DismissOpenOverlays(VisualTreeHelper.GetChild(node, i));
        }
    }

    /// <summary>
    /// Creates a themed <see cref="ContentDialog"/> with the given title and content.
    /// </summary>
    /// <param name="title">The dialog title text.</param>
    /// <param name="content">The dialog body content.</param>
    /// <returns>A configured, themed content dialog ready to show.</returns>
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

    /// <summary>
    /// A Win32 POINT structure for screen coordinate conversion.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative
    {
        /// <summary>The horizontal coordinate in pixels.</summary>
        public int X;
        /// <summary>The vertical coordinate in pixels.</summary>
        public int Y;
    }

    /// <summary>
    /// A Win32 INPUT structure for SendInput.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        /// <summary>The input type (mouse or keyboard).</summary>
        public uint type;
        /// <summary>The input event data union.</summary>
        public InputUnion U;
    }

    /// <summary>
    /// A Win32 input union overlaying mouse and keyboard input data.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        /// <summary>Mouse input data when <see cref="INPUT.type"/> is mouse.</summary>
        [FieldOffset(0)] public MOUSEINPUT mi;
        /// <summary>Keyboard input data when <see cref="INPUT.type"/> is keyboard.</summary>
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    /// <summary>
    /// A Win32 MOUSEINPUT structure for SendInput.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        /// <summary>The horizontal delta or absolute coordinate.</summary>
        public int dx;
        /// <summary>The vertical delta or absolute coordinate.</summary>
        public int dy;
        /// <summary>Mouse wheel delta or X-button data.</summary>
        public uint mouseData;
        /// <summary>Mouse event flags.</summary>
        public uint dwFlags;
        /// <summary>Timestamp for the event.</summary>
        public uint time;
        /// <summary>Extra information associated with the event.</summary>
        public IntPtr dwExtraInfo;
    }

    /// <summary>
    /// A Win32 KEYBDINPUT structure for SendInput.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        /// <summary>The virtual-key code.</summary>
        public ushort wVk;
        /// <summary>The hardware scan code.</summary>
        public ushort wScan;
        /// <summary>Keyboard event flags.</summary>
        public uint dwFlags;
        /// <summary>Timestamp for the event.</summary>
        public uint time;
        /// <summary>Extra information associated with the event.</summary>
        public IntPtr dwExtraInfo;
    }
}

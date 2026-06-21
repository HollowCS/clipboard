using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;
using Microsoft.UI.Windowing;

namespace ClipboardManager;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private IntPtr _hwnd;
    private SUBCLASSPROC _subclassDelegate;
    private const int HOTKEY_ID = 9000;
    
    // Modifiers: MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_V = 0x56;
    private const uint WM_HOTKEY = 0x0312;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        _hwnd = WindowNative.GetWindowHandle(this);
        
        // Register Ctrl + Shift + V
        RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_V);

        _subclassDelegate = new SUBCLASSPROC(WindowSubClass);
        SetWindowSubclass(_hwnd, _subclassDelegate, (IntPtr)1, IntPtr.Zero);

        AppWindow.Closing += AppWindow_Closing;

        RootFrame.Navigate(typeof(MainPage));
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        AppWindow.Hide();
    }

    private void ShowApp_Click(object sender, RoutedEventArgs e)
    {
        AppWindow.Show();
        Activate();
        SetForegroundWindow(_hwnd);
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
    }

    private IntPtr WindowSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            AppWindow.Show();
            Activate();
            SetForegroundWindow(_hwnd);
            return IntPtr.Zero;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }
}

using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Simryx.App.Services;

/// <summary>
/// Иконка в системном трее на «голом» Win32 (Shell_NotifyIcon) — без сторонних пакетов.
/// Подписывается (subclass) на HWND главного окна, чтобы принимать клики по иконке
/// и показывать контекстное меню. Полностью работает в unpackaged-сборке.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    // Наше callback-сообщение от иконки (WM_APP + 1).
    private const uint WM_TRAYICON = 0x8000 + 1;

    // Shell_NotifyIcon
    private const uint NIM_ADD = 0x0;
    private const uint NIM_MODIFY = 0x1;
    private const uint NIM_DELETE = 0x2;
    private const uint NIM_SETVERSION = 0x4;
    private const uint NIF_MESSAGE = 0x1;
    private const uint NIF_ICON = 0x2;
    private const uint NIF_TIP = 0x4;
    private const uint NIF_INFO = 0x10;
    private const uint NIF_SHOWTIP = 0x80;
    private const uint NOTIFYICON_VERSION_4 = 4;

    // Сообщения мыши/меню
    private const uint WM_USER = 0x0400;
    private const uint NIN_SELECT = WM_USER + 0;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_CONTEXTMENU = 0x007B;

    // Меню
    private const uint MF_STRING = 0x0;
    private const uint MF_SEPARATOR = 0x800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;
    private const int IDM_OPEN = 1;
    private const int IDM_EXIT = 2;

    // LoadImage
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x10;
    private const uint LR_DEFAULTSIZE = 0x40;

    /// <summary>Пользователь хочет открыть/восстановить окно (двойной клик или «Открыть»).</summary>
    public event Action? OpenRequested;

    /// <summary>Пользователь выбрал «Выход» в меню трея.</summary>
    public event Action? ExitRequested;

    private IntPtr _hwnd;
    private IntPtr _hicon;
    private bool _added;
    private bool _initialized;

    private string _tooltip = "Simryx Hub";
    private string _openLabel = "Открыть";
    private string _exitLabel = "Выход";

    // Сообщение «TaskbarCreated» — Explorer перезапустился, иконку надо добавить заново.
    private uint _taskbarCreatedMsg;

    // Держим делегат живым, иначе GC соберёт его и subclass упадёт.
    private SUBCLASSPROC? _subclassProc;
    private static readonly UIntPtr SubclassId = (UIntPtr)0x53524958; // 'SRIX'

    public void Initialize(Window window, string iconPath, string tooltip, string openLabel, string exitLabel)
    {
        if (_initialized) return;

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _tooltip = string.IsNullOrWhiteSpace(tooltip) ? "Simryx Hub" : tooltip;
        _openLabel = openLabel;
        _exitLabel = exitLabel;
        _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");

        // Иконка из .ico рядом с приложением.
        if (!string.IsNullOrWhiteSpace(iconPath) && System.IO.File.Exists(iconPath))
            _hicon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);

        // Подписываемся на оконную процедуру, чтобы ловить клики по иконке.
        _subclassProc = WndProc;
        SetWindowSubclass(_hwnd, _subclassProc, SubclassId, UIntPtr.Zero);

        _initialized = true;
    }

    /// <summary>Показать иконку в трее (идемпотентно).</summary>
    public void Show()
    {
        if (!_initialized || _added) return;

        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hicon,
            szTip = _tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };

        if (Shell_NotifyIcon(NIM_ADD, ref data))
        {
            data.uVersionOrTimeout = NOTIFYICON_VERSION_4;
            Shell_NotifyIcon(NIM_SETVERSION, ref data);
            _added = true;
        }
    }

    /// <summary>Убрать иконку из трея (идемпотентно).</summary>
    public void Hide()
    {
        if (!_added) return;
        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
        };
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _added = false;
    }

    // ===== Оконная процедура (subclass) =====

    private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
                           UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        // Explorer перезапустился — пере-добавляем иконку.
        if (uMsg == _taskbarCreatedMsg && _initialized)
        {
            _added = false;
            Show();
            return IntPtr.Zero;
        }

        if (uMsg == WM_TRAYICON)
        {
            // Версия 4: событие мыши — в младшем слове lParam, координаты — в wParam.
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
            switch (mouseMsg)
            {
                case NIN_SELECT:
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    OpenRequested?.Invoke();
                    break;

                case WM_CONTEXTMENU:
                case WM_RBUTTONUP:
                    int x = (short)(wParam.ToInt64() & 0xFFFF);
                    int y = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    ShowContextMenu(hWnd, x, y);
                    break;
            }
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private void ShowContextMenu(IntPtr hWnd, int x, int y)
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        AppendMenu(menu, MF_STRING, (UIntPtr)IDM_OPEN, _openLabel);
        AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
        AppendMenu(menu, MF_STRING, (UIntPtr)IDM_EXIT, _exitLabel);

        // Обязательный «фокус» окну, иначе меню не закроется по клику мимо.
        SetForegroundWindow(hWnd);
        int cmd = TrackPopupMenuEx(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, x, y, hWnd, IntPtr.Zero);
        DestroyMenu(menu);

        if (cmd == IDM_OPEN) OpenRequested?.Invoke();
        else if (cmd == IDM_EXIT) ExitRequested?.Invoke();
    }

    public void Dispose()
    {
        Hide();
        if (_initialized && _subclassProc is not null)
            RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
        if (_hicon != IntPtr.Zero)
        {
            DestroyIcon(_hicon);
            _hicon = IntPtr.Zero;
        }
        _subclassProc = null;
        _initialized = false;
    }

    // ===== Win32 interop =====

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
                                         UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);
}
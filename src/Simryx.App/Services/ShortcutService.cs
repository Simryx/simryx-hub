using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Simryx.App.Services;

// Создаёт ярлык в «Пуске» с заданным AppUserModelID (AUMID).
// Для unpackaged WinUI это ОБЯЗАТЕЛЬНОЕ условие показа тостов: Windows
// сопоставляет тост (помеченный AUMID процесса) с ярлыком, у которого тот же
// AUMID, и берёт оттуда имя и иконку. Без ярлыка тост молча отбрасывается.
public static class ShortcutService
{
    public static void EnsureStartMenuShortcut(string aumid, string displayName)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var shortcutPath = Path.Combine(startMenu, displayName + ".lnk");

            // Создаём только если ярлыка ещё нет (canonical ставит инсталлятор).
            if (File.Exists(shortcutPath)) return;

            CreateShortcut(shortcutPath, exePath, aumid);
        }
        catch
        {
            // Уведомления — некритичный путь; никогда не роняем запуск приложения.
        }
    }

    private static void CreateShortcut(string shortcutPath, string exePath, string aumid)
    {
        var link = (IShellLinkW)new CShellLink();
        link.SetPath(exePath);
        link.SetArguments(string.Empty);
        link.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? string.Empty);
        link.SetIconLocation(exePath, 0); // иконка из самого exe

        // Прописываем AUMID в свойства ярлыка (PKEY_AppUserModel_ID).
        var store = (IPropertyStore)link;
        InitPropVariantFromString(aumid, out var pv);
        try
        {
            var key = PKEY_AppUserModel_ID;
            store.SetValue(ref key, ref pv);
            store.Commit();
        }
        finally
        {
            PropVariantClear(ref pv);
        }

        var file = (IPersistFile)link;
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        file.Save(shortcutPath, true);
    }

    private static PROPERTYKEY PKEY_AppUserModel_ID => new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5,
    };

    [DllImport("propsys.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void InitPropVariantFromString(
        [MarshalAs(UnmanagedType.LPWStr)] string psz, out PROPVARIANT ppropvar);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
        public int p2;
    }
}
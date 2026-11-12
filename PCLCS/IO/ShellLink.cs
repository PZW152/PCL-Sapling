using System.Runtime.InteropServices;
using System.Text;

namespace PCLCS;

/// <summary>
/// 创建与读取 Windows 快捷方式（.lnk），通过系统的 IShellLink 接口，不依赖 WScript。
/// </summary>
public static class ShellLink {

    /// <summary>快捷方式的内容。</summary>
    public record Info(string Target, string Arguments, string WorkingDirectory, string Description, string IconPath);

    public static void Create(string LinkPath, Info Link) {
        var Shell = (IShellLinkW)new CShellLink();
        try {
            Shell.SetPath(Link.Target);
            Shell.SetArguments(Link.Arguments);
            Shell.SetWorkingDirectory(Link.WorkingDirectory);
            Shell.SetDescription(Link.Description);
            Shell.SetIconLocation(Link.IconPath, 0);
            ((IPersistFile)Shell).Save(LinkPath, true);
        } finally {
            Marshal.FinalReleaseComObject(Shell);
        }
    }

    public static Info Read(string LinkPath) {
        var Shell = (IShellLinkW)new CShellLink();
        try {
            ((IPersistFile)Shell).Load(LinkPath, 0);
            var Buffer = new StringBuilder(1024);
            Shell.GetPath(Buffer, Buffer.Capacity, IntPtr.Zero, 0);
            string Target = Buffer.ToString();
            Buffer.Clear();
            Shell.GetArguments(Buffer, Buffer.Capacity);
            string Arguments = Buffer.ToString();
            Buffer.Clear();
            Shell.GetWorkingDirectory(Buffer, Buffer.Capacity);
            string WorkingDirectory = Buffer.ToString();
            Buffer.Clear();
            Shell.GetDescription(Buffer, Buffer.Capacity);
            string Description = Buffer.ToString();
            Buffer.Clear();
            Shell.GetIconLocation(Buffer, Buffer.Capacity, out _);
            return new Info(Target, Arguments, WorkingDirectory, Description, Buffer.ToString());
        } finally {
            Marshal.FinalReleaseComObject(Shell);
        }
    }

    /// <summary>
    /// 把文字变成可以用作文件名的形式：去掉 Windows 文件名中不允许的字符。
    /// </summary>
    public static string SafeFileName(string Name) {
        var Invalid = Path.GetInvalidFileNameChars();
        string Result = new string(Name.Select(c => Invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return Result.Length == 0 ? "_" : Result;
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}

/// <summary>
/// 从快捷方式启动某个实例：命令行参数与“已打开的启动器接手请求”的约定。
/// </summary>
public static class LaunchRequest {

    public const string Argument = "--launch";

    /// <summary>快捷方式的参数：--launch "实例文件夹"。末尾不带反斜杠，否则 \" 会被当成转义的引号。</summary>
    public static string BuildArguments(string InstanceFolder) => $"{Argument} \"{InstanceFolder.TrimEnd('\\')}\"";

    /// <summary>从命令行参数中取出要启动的实例文件夹；没有时返回 null。</summary>
    public static string? Parse(IReadOnlyList<string> Args) {
        for (int i = 0; i < Args.Count - 1; i++) {
            if (Args[i] == Argument && !string.IsNullOrWhiteSpace(Args[i + 1]))
                return Args[i + 1].Trim().Trim('"').TrimEnd('\\') + "\\";
        }
        return null;
    }
}

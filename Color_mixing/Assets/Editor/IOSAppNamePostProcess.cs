#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Unity menghapus spasi dari nama target Xcode (PRODUCT_NAME), sehingga
/// CFBundleName jadi "TrioSemirKharisma". Script ini memaksa nama yang
/// tampil di iOS (home screen, Settings, Spotlight, dialog izin) pakai spasi.
/// </summary>
public class IOSAppNamePostProcess : IPostprocessBuildWithReport
{
    const string AppName = "Trio Semir Kharisma";

    public int callbackOrder => 1000;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;

        string plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
        if (!File.Exists(plistPath)) return;

        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("CFBundleDisplayName", AppName);
        plist.root.SetString("CFBundleName", AppName);
        plist.WriteToFile(plistPath);
    }
}
#endif

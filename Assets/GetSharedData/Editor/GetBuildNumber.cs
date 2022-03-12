using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
using SharedConstant;

public class GetBuildNumber : IPreprocessBuildWithReport, IPostprocessBuildWithReport {

	/// <summary>次点の優先順位</summary>
	public int callbackOrder { get { return 1; } }

	/// <summary>ビルド前にコールバックされる</summary>
	public void OnPreprocessBuild (BuildReport report) {
		var ScriptPathPrefKey = $"{PlayerSettings.companyName}/{PlayerSettings.productName}/ScriptPath";
		var AssetPath = EditorPrefs.GetString (ScriptPathPrefKey, "Assets/GetSharedData/Scripts/");
		if (GetSharedData.UnifiedNumber) {
			var maxbn = maxBuildNumber ();
			if (maxbn >= 0) {
				PlayerSettings.Android.bundleVersionCode = maxbn;
				PlayerSettings.macOS.buildNumber = PlayerSettings.iOS.buildNumber = maxbn.ToString ();
			}
		}
		File.WriteAllText (Path.Combine (AssetPath, "BuildNumber.cs"),
$@"namespace {nameof (SharedConstant)} {{

	public static partial class {nameof (Cns)} {{
		/// <summary>ビルド番号</summary>
#if UNITY_STANDALONE
		public const string BuildNumber = ""{PlayerSettings.macOS.buildNumber}"";
#elif UNITY_IOS
		public const string BuildNumber = ""{PlayerSettings.iOS.buildNumber}"";
#elif UNITY_ANDROID
		public const string BuildNumber = ""{PlayerSettings.Android.bundleVersionCode}"";
#else
		public const string BuildNumber = ""{PlayerSettings.WSA.packageVersion}"";
#endif
		/// <summary>バンドルバージョン</summary>
		public const string BundleVersion = ""{PlayerSettings.bundleVersion}"";
	}}

}}
");
		AssetDatabase.Refresh ();
	}

	/// <summary>数字のみの正規表現</summary>
	private static readonly Regex isIntegerRegex = new Regex (@"^[0-9]+$");
	/// <summary>MacOS、iOS、Androidが空欄または整数の時、0以上の最大値を返す</summary>
	private static int maxBuildNumber () {
		int mac = 0, ios = 0;
		if ((string.IsNullOrEmpty (PlayerSettings.macOS.buildNumber) || (isIntegerRegex.IsMatch (PlayerSettings.macOS.buildNumber) && int.TryParse (PlayerSettings.macOS.buildNumber, out mac)))
			&& (string.IsNullOrEmpty (PlayerSettings.iOS.buildNumber) || (isIntegerRegex.IsMatch (PlayerSettings.iOS.buildNumber) && int.TryParse (PlayerSettings.iOS.buildNumber, out ios)))
		) {
			return Math.Max (Math.Max (mac, ios), PlayerSettings.Android.bundleVersionCode);
		}
		return int.MinValue;
	}

	/// <summary>ビルド後にコールバックされる</summary>
	public void OnPostprocessBuild (BuildReport report) {
		var target = report.summary.platform;
		if (GetSharedData.AutoIncrement) {
			if (GetSharedData.UnifiedNumber) {
				var maxbn = maxBuildNumber ();
				if (maxbn >= 0) {
					maxbn++;
					PlayerSettings.Android.bundleVersionCode = maxbn;
					PlayerSettings.macOS.buildNumber = PlayerSettings.iOS.buildNumber = maxbn.ToString ();
				}
			} else if (target == BuildTarget.iOS) {
				int ver = 0;
				if (string.IsNullOrEmpty (PlayerSettings.iOS.buildNumber) || (isIntegerRegex.IsMatch (PlayerSettings.iOS.buildNumber) && int.TryParse (PlayerSettings.iOS.buildNumber, out ver))) {
					PlayerSettings.iOS.buildNumber = (ver + 1).ToString ();
				}
			} else if (target == BuildTarget.Android) {
				PlayerSettings.Android.bundleVersionCode++;
			} else {
				int ver = 0;
				if (string.IsNullOrEmpty (PlayerSettings.macOS.buildNumber) || (isIntegerRegex.IsMatch (PlayerSettings.macOS.buildNumber) && int.TryParse (PlayerSettings.macOS.buildNumber, out ver))) {
					PlayerSettings.macOS.buildNumber = (ver + 1).ToString ();
				}
			}
			SettingsService.NotifySettingsProviderChanged ();
			SettingsService.RepaintAllSettingsWindow ();
		}
	}

}

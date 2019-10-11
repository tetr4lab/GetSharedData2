using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using GetSharedDataTranslator;

public sealed class GetSharedData : EditorWindow {

	#region Static

	private static string Application;
	private static string Keyword;
	private static string Document;
	private static string AssetPath;

	private const string ApplicationPrefKey = "GetSharedData/Application";
	private const string KeywordPrefKey = "GetSharedData/Keyword";
	private static string DocumentPrefKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/Document";
	private static string ScriptPathPrefKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/ScriptPath";
	private const string WindowIconPath = "Assets/GetSharedData/Textures/SpreadsheetFavicon3.png";
	private const string TimeoutPrefKey = "GetSharedData/Timeout";
	private const string WebIntervalPrefKey = "GetSharedData/WebInterval";

	/// <summary>ウィンドウオブジェクト</summary>
	private static GetSharedData SingletonObject;

	/// <summary>進捗</summary>
	public static string Progress;
	private static string LastProgress;
	private static CancellationTokenSource TokenSource;
	/// <summary>変換タスク</summary>
	private static Task Translator = null;

	/// <summary>結果 (空なら正常終了)</summary>
	public static string ErrorMessage;

	/// <summary>生成されたパーサー</summary>
	private static Parser parser = null;

	/// <summary>取得と変換</summary>
	[MenuItem ("Window/GetSharedData/Get SpreadSheet %&g")]
	private static void onGetData () {
		Progress = "";
		LastProgress = "";
		if (Translator != null) {
			Debug.LogWarning ("GetSharedData: already running");
			return;
		}
		Debug.Log ("GetSharedData:┌─── BEGIN");
		if (AssetPath [AssetPath.Length - 1] != '/') {
			AssetPath = $"{AssetPath}/";
		}
		checkFolder (AssetPath);
		ErrorMessage = null;
		TokenSource = new CancellationTokenSource ();
		var token = TokenSource.Token;
		Translator = Task.Run (async () => { parser = await GetSharedDataTranslator.Translator.Translate (token, Application, Keyword, Document, AssetPath); }, token);

		bool checkFolder (string path) { // ターゲットフォルダがなければ作成
			if (path.EndsWith ("/")) { path = path.TrimEnd ('/'); }
			if (AssetDatabase.IsValidFolder (path)) { return true; }
			var match = Regex.Match (path, @"^(.+)\/([^\/]+)\/?$");
			if (match.Success) {
				var folder = match.Groups [1].Value;
				var file = match.Groups [2].Value;
				if (checkFolder (folder)) {
					if (AssetDatabase.CreateFolder (folder, file) != null) {
						AssetDatabase.Refresh ();
						return true;
					}
				}
			}
			return false;
		}
	}

	/// <summary>中断</summary>
	[MenuItem ("Window/GetSharedData/Abort")]
	private static void onAbort () {
		if (Translator == null) {
			Debug.LogWarning ("GetSharedData: not running");
		} else if (TokenSource.Token.IsCancellationRequested) {
			Debug.LogWarning ("GetSharedData: already requested");
		} else {
			TokenSource.Cancel ();
			Debug.LogWarning ("GetSharedData: cancel-request by user");
		}
	}

	/// <summary>ウインドウを開く</summary>
	[MenuItem ("Window/GetSharedData/Open Setting Window")]
	private static void Open () {
		SingletonObject = GetWindow<GetSharedData> ();
		var icon = AssetDatabase.LoadAssetAtPath<Texture> (WindowIconPath);
		SingletonObject.titleContent = new GUIContent ("GetSharedData", icon);
		Debug.Log ($"GetSharedData: Project {PlayerSettings.companyName}/{PlayerSettings.productName}");
	}

	/// <summary>終了時の処理</summary>
	private static void onCompleted () {
		if (string.IsNullOrEmpty (ErrorMessage) && (Translator == null || Translator.Status == TaskStatus.RanToCompletion) && parser !=null && parser.Generate (AssetPath)) {
			Debug.Log ("GetSharedData:└─── END");
		} else {
			Debug.LogError (string.IsNullOrEmpty (ErrorMessage) ? $"GetSharedData: Task {Translator.Status}" : ErrorMessage);
			Debug.Log ("GetSharedData:└─── ABORT");
		}
		TokenSource.Dispose ();
		Translator.Dispose ();
		Translator = null;
		AssetDatabase.Refresh (); // アセットを更新
		if (SingletonObject) { SingletonObject.Repaint (); }
	}

	/// <summary>終了の監視</summary>
	[InitializeOnLoadMethod]
	private static void Init () {
		EditorApplication.update += () => {
			if (Translator != null) {
				if (Translator.IsCompleted) {
					onCompleted ();
				} else if (LastProgress != Progress) {
					Debug.Log ($"GetSharedData: {Progress}");
					LastProgress = Progress;
				}
			}
		};
	}

	#endregion

	#region EventHandler

	private void OnDestroy () {
		SingletonObject = null;
	}

	private void OnEnable () {
		Application = EditorPrefs.GetString (ApplicationPrefKey, "https://script.google.com/macros/s/[APP ID]/exec");
		Keyword = EditorPrefs.GetString (KeywordPrefKey);
		Document = EditorPrefs.GetString (DocumentPrefKey);
		AssetPath = EditorPrefs.GetString (ScriptPathPrefKey, "Assets/GetSharedData/Scripts/");
	}

	private void OnDisable () {
		EditorPrefs.SetString (ApplicationPrefKey, Application);
		EditorPrefs.SetString (KeywordPrefKey, Keyword);
		EditorPrefs.SetString (DocumentPrefKey, Document);
		EditorPrefs.SetString (ScriptPathPrefKey, AssetPath);
	}

	private bool isOpen = true;

	private void OnGUI () {
		//GUILayout.Label ("Settings");
		isOpen = EditorGUILayout.Foldout (isOpen, "Settings", true);
		if (isOpen) {
			Application = EditorGUILayout.TextField (new GUIContent ("Application URL*", "the URL of Google Apps Script"), Application);
			Keyword = EditorGUILayout.TextField (new GUIContent ("Access Key*", "the key written on Google Apps Script"), Keyword);
			Document = EditorGUILayout.TextField (new GUIContent ("Document ID", "the ID of Google Spreadsheet"), Document);
			AssetPath = EditorGUILayout.TextField (new GUIContent ("Asset Folder", "the path of assets to create"), AssetPath);
			EditorGUILayout.HelpBox ("* Shared setting between multiple projects.", MessageType.None);
		}
		GUILayout.Space (20f);
		EditorGUI.BeginDisabledGroup (EditorApplication.isCompiling || Translator != null);
		if (GUILayout.Button ("GetSharedData", GUILayout.Height (30))) { onGetData (); }
		EditorGUI.EndDisabledGroup ();
		GUILayout.Space (30f);
		EditorGUI.BeginDisabledGroup (EditorApplication.isCompiling || Translator == null || TokenSource.Token.IsCancellationRequested);
		if (GUILayout.Button ("Abort", GUILayout.Height (30))) { onAbort (); }
		EditorGUI.EndDisabledGroup ();
	}

	#endregion

}

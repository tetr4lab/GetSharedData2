using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GetSharedDataTranslator;
using OAuthHandler;
using Tetr4lab;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

[InitializeOnLoad]
public sealed class GetSharedData : EditorWindow, IPreprocessBuildWithReport {

	/// <summary>ビルド前に最優先で実行</summary>
	public int callbackOrder { get { return 0; } }

	#region Static

	private static string Application;
	private static string ClientId;
	private static string ClientSecret;
	private static string AccessToken;
	private static string RefreshToken;
	private static string Document;
	private static string AssetPath;
	public static bool UnifiedNumber { get; private set; }
	public static bool AutoIncrement { get; private set; }
	private const string Scope = "https://www.googleapis.com/auth/drive%20https://www.googleapis.com/auth/spreadsheets";
	private const string ApplicationPrefKey = "GetSharedData/Application";
	private const string ClientIdPrefKey = "GetSharedData/ClientId";
	private const string ClientSecretPrefKey = "GetSharedData/ClientSecret";
	private const string AccessTokenPrefKey = "GetSharedData/AccessToken";
	private const string RefreshTokenPrefKey = "GetSharedData/RefreshToken";
	private const string OAuthSettingsIsOpenPrefKey = "GetSharedData/OAuthSettingsIsOpen";
	private const string BuildNumberSettingsIsOpenPrefKey = "GetSharedData/BuildNumberSettingsIsOpen";
	private static string DocumentPrefKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/Document";
	private static string ScriptPathPrefKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/ScriptPath";
	private const string WindowIconPath = "Assets/GetSharedData/Textures/SpreadsheetFavicon3.png";
	private const string TimeoutPrefKey = "GetSharedData/Timeout";
	private const string WebIntervalPrefKey = "GetSharedData/WebInterval";
	private const string DefaultAssetPath = "Assets/GetSharedData/Scripts/SharedData/";
	private static string UnifiedBuildNumberPrefsKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/UnifiedBuildNumber";
	private static string AutoIncrementBuildNumberPrefsKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/AutoIncrementBuildNumber";

	/// <summary>ロード済み</summary>
	private static bool isLoaded = false;

	/// <summary>認証情報</summary>
	private static GoogleOAuth OAuth = null;

	/// <summary>認証設定の開閉状態</summary>
	private static bool OAuthSettingsIsOpen = false;

	/// <summary>ビルド番号設定の開閉状態</summary>
	private static bool BuildNumberSettingsIsOpen = false;

	/// <summary>ウィンドウオブジェクト</summary>
	private static GetSharedData SingletonObject;

	/// <summary>中断トークン</summary>
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
		if (Translator != null) {
			Debug.LogWarning ("GetSharedData: already running");
			return;
		}
		Debug.Log ("GetSharedData:┌─── BEGIN");
		if (AssetPath [AssetPath.Length - 1] != '/') {
			AssetPath = $"{AssetPath}/";
		}
		SavePrefs ();
		checkFolder (AssetPath);
		translateAsync ();

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

	/// <summary>非同期呼び出しと終了時の処理</summary>
	private static async void translateAsync () {
		try {
			await InitOAuth ();
			ErrorMessage = null;
			TokenSource = new CancellationTokenSource ();
			var token = TokenSource.Token; // 中断トークン
			var progress = new Progress<object> (str => Debug.Log ($"GetSharedData: {str}")); // 進捗報告ハンドラ
			Translator = Task.Run (async () => { parser = await GetSharedDataTranslator.Translator.Translate (token, progress, OAuth, Application, Document, AssetPath); }, token);
			await Translator;
		} finally {
			if (string.IsNullOrEmpty (ErrorMessage) && (Translator == null || Translator.Status == TaskStatus.RanToCompletion) && parser != null && parser.Generate (AssetPath)) {
				Debug.Log ("GetSharedData:└─── END");
			} else {
				Debug.LogError (string.IsNullOrEmpty (ErrorMessage) ? $"GetSharedData: Task {Translator.Status}" : ErrorMessage);
				Debug.Log ("GetSharedData:└─── ABORT");
			}
			TokenSource.Dispose ();
			Translator.Dispose ();
			Translator = null;
			await InitOAuth ();
			AssetDatabase.Refresh (); // アセットを更新
			if (SingletonObject) { SingletonObject.Repaint (); }
		}
	}

	/// <summary>OAuth初期化</summary>
	private static async Task InitOAuth () {
		if (OAuth == null) {
			if (!string.IsNullOrEmpty (ClientId) && !string.IsNullOrEmpty (ClientSecret)) {
				OAuth = new GoogleOAuth (ClientId, ClientSecret, Scope, AccessToken, RefreshToken, applicationUrl: Application);
				(AccessToken, RefreshToken) = await OAuth.GetTokensAsync (silent: true);
			}
		} else {
			(AccessToken, RefreshToken) = OAuth.Tokens;
		}
	}

	/// <summary>設定のロード</summary>
	private static void LoadPrefs () {
		if (!isLoaded) {
			// ロードは一度だけ
			Application = EditorPrefs.GetString (ApplicationPrefKey);
			ClientId = EditorPrefs.GetString (ClientIdPrefKey);
			ClientSecret = EditorPrefs.GetString (ClientSecretPrefKey);
			AccessToken = EditorPrefs.GetString (AccessTokenPrefKey);
			RefreshToken = EditorPrefs.GetString (RefreshTokenPrefKey);
			OAuthSettingsIsOpen = EditorPrefs.GetBool (OAuthSettingsIsOpenPrefKey, false);
			Document = EditorPrefs.GetString (DocumentPrefKey);
			AssetPath = EditorPrefs.GetString (ScriptPathPrefKey, DefaultAssetPath);
			UnifiedNumber = EditorPrefs.GetBool (UnifiedBuildNumberPrefsKey, false);
			AutoIncrement = EditorPrefs.GetBool (AutoIncrementBuildNumberPrefsKey, false);
			BuildNumberSettingsIsOpen = EditorPrefs.GetBool (BuildNumberSettingsIsOpenPrefKey, false);
			_ = InitOAuth ();
			isLoaded = true;
		}
	}

	/// <summary>設定のセーブ</summary>
	private static void SavePrefs () {
		// ロードされたことがない(初期化されていない)可能性に配慮
		LoadPrefs ();

		if (OAuth != null) {
			(AccessToken, RefreshToken) = OAuth.Tokens;
		}
		EditorPrefs.SetString (ApplicationPrefKey, Application);
		EditorPrefs.SetString (ClientIdPrefKey, ClientId);
		EditorPrefs.SetString (ClientSecretPrefKey, ClientSecret);
		EditorPrefs.SetString (AccessTokenPrefKey, AccessToken);
		EditorPrefs.SetString (RefreshTokenPrefKey, RefreshToken);
		EditorPrefs.SetBool (OAuthSettingsIsOpenPrefKey, OAuthSettingsIsOpen);
		EditorPrefs.SetString (DocumentPrefKey, Document);
		EditorPrefs.SetString (ScriptPathPrefKey, AssetPath);
		EditorPrefs.SetBool (UnifiedBuildNumberPrefsKey, UnifiedNumber);
		EditorPrefs.SetBool (AutoIncrementBuildNumberPrefsKey, AutoIncrement);
		EditorPrefs.SetBool (BuildNumberSettingsIsOpenPrefKey, BuildNumberSettingsIsOpen);
	}

	#endregion

	#region EventHandler

	private void OnDestroy () {
		SingletonObject = null;
	}

	private void OnEnable () => LoadPrefs ();
	
	private void OnDisable () => SavePrefs ();

	private void OnGUI () {
		EditorGUILayout.LabelField ("Project Settings", EditorStyles.boldLabel);
		EditorGUILayout.BeginHorizontal ();
		GUILayout.Space (10f);
		EditorGUILayout.BeginVertical ();
		// document
		Document = EditorGUILayout.TextField (new GUIContent ("Document ID", "the ID of Google Spreadsheet"), Document);
		// asset path
		AssetPath = EditorGUILayout.TextField (new GUIContent ("Asset Folder", "the path of assets to create"), AssetPath);
		EditorGUILayout.EndVertical ();
		EditorGUILayout.EndHorizontal ();

		// get button
		GUILayout.Space (20f);
		EditorGUILayout.BeginHorizontal ();
		GUILayout.Space (10f);
		EditorGUI.BeginDisabledGroup (EditorApplication.isCompiling || Translator != null || string.IsNullOrEmpty (Application) || string.IsNullOrEmpty (Document) || string.IsNullOrEmpty (AssetPath));
		if (GUILayout.Button ("GetSharedData", GUILayout.Height (30))) { onGetData (); }
		EditorGUI.EndDisabledGroup ();
		GUILayout.Space (40f);
		// abort button
		EditorGUI.BeginDisabledGroup (EditorApplication.isCompiling || Translator == null || TokenSource.Token.IsCancellationRequested);
		if (GUILayout.Button ("Abort", GUILayout.Height (30), GUILayout.Width (100))) { onAbort (); }
		EditorGUI.EndDisabledGroup ();
		GUILayout.Space (10f);
		EditorGUILayout.EndHorizontal ();
		GUILayout.Space (20f);

		// build number
		BuildNumberSettingsIsOpen = EditorGUILayout.Foldout (BuildNumberSettingsIsOpen, "Build/Bundle Number Settings", false);
		if (BuildNumberSettingsIsOpen) {
			EditorGUILayout.BeginHorizontal ();
			GUILayout.Space (20f);
			EditorGUILayout.BeginVertical ();
			UnifiedNumber = EditorGUILayout.Toggle (new GUIContent ("Unified", "give all targets the maximum build number"), UnifiedNumber);
			AutoIncrement = EditorGUILayout.Toggle (new GUIContent ("Auto Increment", "automatically increment build number after build"), AutoIncrement);
			EditorGUILayout.EndVertical ();
			EditorGUILayout.EndHorizontal ();
		}
		GUILayout.Space (10f);

		// oauth
		OAuthSettingsIsOpen = EditorGUILayout.Foldout (OAuthSettingsIsOpen, "OAuth Settings", false);
		if (OAuthSettingsIsOpen) {
			EditorGUILayout.BeginHorizontal ();
			GUILayout.Space (10f);
			EditorGUILayout.BeginVertical ();
			// application
			Application = EditorGUILayout.TextField (new GUIContent ("Application URL", "the URL of Google Apps Script"), Application);
			// client id
			ClientId = EditorGUILayout.TextField (new GUIContent ("Client ID", "the ID for Google Apps Script"), ClientId);
			// client secret
			ClientSecret = EditorGUILayout.TextField (new GUIContent ("Client Secret", "the secret for Google Apps Script"), ClientSecret);
			// tokens
			EditorGUILayout.BeginHorizontal ();
			EditorGUILayout.BeginVertical ();
			// access token
			EditorGUI.BeginDisabledGroup (true);
			EditorGUILayout.TextField (new GUIContent ("Access Token"), AccessToken);
			EditorGUI.EndDisabledGroup ();
			// refresh token
			EditorGUI.BeginDisabledGroup (true);
			EditorGUILayout.TextField (new GUIContent ("Refresh Token"), RefreshToken);
			EditorGUI.EndDisabledGroup ();
			EditorGUILayout.EndVertical ();
			EditorGUI.BeginDisabledGroup ((string.IsNullOrEmpty (AccessToken) && string.IsNullOrEmpty (RefreshToken)) || EditorApplication.isCompiling || Translator != null);
			if (GUILayout.Button ("Clear", GUILayout.Height (30), GUILayout.Width (60))) { AccessToken = RefreshToken = ""; OAuth.ClearTokens (); }
			EditorGUI.EndDisabledGroup ();
			EditorGUILayout.EndHorizontal ();
			// guid
			EditorGUILayout.HelpBox ("Shared settings between projects.", MessageType.None);
			// 
			EditorGUILayout.EndVertical ();
			EditorGUILayout.EndHorizontal ();
		}
	}

	/// <summary>ビルド前に実行</summary>
    public void OnPreprocessBuild (BuildReport report) {
		SavePrefs ();
    }

    #endregion

}

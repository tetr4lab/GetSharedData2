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

public sealed class GetSharedData : IPreprocessBuildWithReport {

	// ビルド前に最優先で実行
	#region IPreprocessBuildWithReport
	public int callbackOrder => 0;
	public void OnPreprocessBuild (BuildReport report) => SavePrefs ();
	#endregion IPreprocessBuildWithReport

	#region Static

	/// <summary>GASアプリケーションURL</summary>
	public static string Application;
	/// <summary>GCPクライアントID</summary>
	public static string ClientId;
	/// <summary>GCPクライアントシークレット</summary>
	public static string ClientSecret;
	/// <summary>アクセストークン</summary>
	public static string AccessToken { get; private set; }
	/// <summary>リフレッシュトークン</summary>
	public static string RefreshToken { get; private set; }
	/// <summary>スプレッドシートのドキュメントID</summary>
	public static string Document;
	/// <summary>共有データを格納するフォルダ</summary>
	public static string AssetPath;
	/// <summary>プラットフォーム間でビルド番号を共有する</summary>
	public static bool UnifiedNumber;
	/// <summary>ビルド毎に番号を更新する</summary>
	public static bool AutoIncrement;

	// OAuth2スコープ
	private const string Scope = "https://www.googleapis.com/auth/drive%20https://www.googleapis.com/auth/spreadsheets";

	// EditorPreffkeys
	private const string ApplicationPrefKey = "GetSharedData/Application";
	private const string ClientIdPrefKey = "GetSharedData/ClientId";
	private const string ClientSecretPrefKey = "GetSharedData/ClientSecret";
	private const string AccessTokenPrefKey = "GetSharedData/AccessToken";
	private const string RefreshTokenPrefKey = "GetSharedData/RefreshToken";
	private static string DocumentPrefKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/Document";
	private static string ScriptPathPrefKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/ScriptPath";
	private static string UnifiedBuildNumberPrefsKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/UnifiedBuildNumber";
	private static string AutoIncrementBuildNumberPrefsKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/AutoIncrementBuildNumber";

	/// <summary>デフォルトの格納フォルダ</summary>
	private const string DefaultAssetPath = "Assets/GetSharedData/Scripts/SharedData/";

	/// <summary>ロード済み</summary>
	private static bool isLoaded = false;

	/// <summary>認証情報</summary>
	private static GoogleOAuth OAuth = null;

	/// <summary>中断トークン</summary>
	private static CancellationTokenSource TokenSource;

	/// <summary>中断請求済み</summary>
	public static bool IsCancellationRequested => TokenSource != null && TokenSource.Token.IsCancellationRequested;

	/// <summary>変換タスク</summary>
	private static Task Translator = null;

	/// <summary>変換中</summary>
	public static bool IsTranslating => Translator != null;

	/// <summary>結果 (空なら正常終了)</summary>
	public static string ErrorMessage;

	/// <summary>生成されたパーサー</summary>
	private static Parser parser = null;

	/// <summary>取得と変換</summary>
	[MenuItem ("Window/GetSharedData/Get SpreadSheet %&g")]
	public static void GetData () {
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
	public static void Abort () {
		if (Translator == null) {
			Debug.LogWarning ("GetSharedData: not running");
		} else if (TokenSource.Token.IsCancellationRequested) {
			Debug.LogWarning ("GetSharedData: already requested");
		} else {
			TokenSource.Cancel ();
			Debug.LogWarning ("GetSharedData: cancel-request by user");
		}
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
			GetSharedDataWindow.Update ();
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

	/// <summary>OAuthトークン消去</summary>
	public static void ClearOAuth () {
		AccessToken = RefreshToken = "";
		OAuth.ClearTokens ();
	}

	/// <summary>設定のロード</summary>
	public static void LoadPrefs () {
		if (!isLoaded) {
			// ロードは一度だけ
			Application = EditorPrefs.GetString (ApplicationPrefKey);
			ClientId = EditorPrefs.GetString (ClientIdPrefKey);
			ClientSecret = EditorPrefs.GetString (ClientSecretPrefKey);
			AccessToken = EditorPrefs.GetString (AccessTokenPrefKey);
			RefreshToken = EditorPrefs.GetString (RefreshTokenPrefKey);
			Document = EditorPrefs.GetString (DocumentPrefKey);
			AssetPath = EditorPrefs.GetString (ScriptPathPrefKey, DefaultAssetPath);
			UnifiedNumber = EditorPrefs.GetBool (UnifiedBuildNumberPrefsKey, false);
			AutoIncrement = EditorPrefs.GetBool (AutoIncrementBuildNumberPrefsKey, false);
			_ = InitOAuth ();
			isLoaded = true;
		}
	}

	/// <summary>設定のセーブ</summary>
	public static void SavePrefs () {
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
		EditorPrefs.SetString (DocumentPrefKey, Document);
		EditorPrefs.SetString (ScriptPathPrefKey, AssetPath);
		EditorPrefs.SetBool (UnifiedBuildNumberPrefsKey, UnifiedNumber);
		EditorPrefs.SetBool (AutoIncrementBuildNumberPrefsKey, AutoIncrement);
	}

	#endregion Static

}

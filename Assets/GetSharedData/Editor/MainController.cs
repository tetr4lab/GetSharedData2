using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using OAuthHandler;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GetSharedData {

	/// <summary>主制御</summary>
	public sealed class MainController : IPreprocessBuildWithReport {

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

		/// <summary>GASアプリケーションURL 保存キー</summary>
		private const string ApplicationPrefKey = "GetSharedData/Application";
		/// <summary>GCPクライアントID 保存キー</summary>
		private const string ClientIdPrefKey = "GetSharedData/ClientId";
		/// <summary>GCPクライアントシークレット 保存キー</summary>
		private const string ClientSecretPrefKey = "GetSharedData/ClientSecret";
		/// <summary>アクセストークン 保存キー</summary>
		private const string AccessTokenPrefKey = "GetSharedData/AccessToken";
		/// <summary>リフレッシュトークン 保存キー</summary>
		private const string RefreshTokenPrefKey = "GetSharedData/RefreshToken";
		/// <summary>スプレッドシートのドキュメントID 保存キー</summary>
		private static string DocumentPrefKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/Document";
		/// <summary>共有データを格納するフォルダ 保存キー</summary>
		private static string ScriptPathPrefKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/ScriptPath";
		/// <summary>プラットフォーム間でビルド番号を共有する 保存キー</summary>
		private static string UnifiedBuildNumberPrefsKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/UnifiedBuildNumber";
		/// <summary>ビルド毎に番号を更新する 保存キー</summary>
		private static string AutoIncrementBuildNumberPrefsKey => $"{PlayerSettings.companyName}/{PlayerSettings.productName}/AutoIncrementBuildNumber";

		// OAuth2スコープ
		private const string Scope = "https://www.googleapis.com/auth/spreadsheets https://www.googleapis.com/auth/drive.readonly";

		/// <summary>デフォルトの格納フォルダ</summary>
		private const string DefaultAssetPath = "Assets/GetSharedData/Scripts/SharedData/";

		/// <summary>ロード済み</summary>
		private static bool isLoaded = false;

		/// <summary>認証情報</summary>
		private static GoogleOAuth oAuth = null;

		/// <summary>中断トークン</summary>
		private static CancellationTokenSource tokenSource;

		/// <summary>中断請求済み</summary>
		public static bool IsCancellationRequested => tokenSource != null && tokenSource.Token.IsCancellationRequested;

		/// <summary>変換タスク</summary>
		private static Task translator = null;

		/// <summary>変換中</summary>
		public static bool IsTranslating => translator != null;

		/// <summary>結果 (空なら正常終了)</summary>
		public static string ErrorMessage;

		/// <summary>生成されたパーサー</summary>
		private static Parser parser = null;

		/// <summary>取得と変換</summary>
		[MenuItem ("Window/GetSharedData/Get SpreadSheet %&g")]
		public static void GetData () {
			if (translator != null) {
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
			if (translator == null) {
				Debug.LogWarning ("GetSharedData: not running");
			} else if (tokenSource.Token.IsCancellationRequested) {
				Debug.LogWarning ("GetSharedData: already requested");
			} else {
				tokenSource.Cancel ();
				Debug.LogWarning ("GetSharedData: cancel-request by user");
			}
		}

		/// <summary>非同期呼び出しと終了時の処理</summary>
		private static async void translateAsync () {
			try {
				await InitOAuth ();
				ErrorMessage = null;
				tokenSource = new CancellationTokenSource ();
				var token = tokenSource.Token; // 中断トークン
				var progress = new Progress<object> (str => Debug.Log ($"GetSharedData: {str}")); // 進捗報告ハンドラ
				translator = Task.Run (async () => { parser = await Translator.Translate (token, progress, oAuth, Application, Document, AssetPath); }, token);
				await translator;
			}
			finally {
				if (string.IsNullOrEmpty (ErrorMessage) && (translator == null || translator.Status == TaskStatus.RanToCompletion) && parser != null && parser.Generate (AssetPath)) {
					Debug.Log ("GetSharedData:└─── END");
				} else {
					Debug.LogError (string.IsNullOrEmpty (ErrorMessage) ? $"GetSharedData: Task {translator.Status}" : ErrorMessage);
					Debug.Log ("GetSharedData:└─── ABORT");
				}
				tokenSource.Dispose ();
				translator.Dispose ();
				translator = null;
				await InitOAuth ();
				AssetDatabase.Refresh (); // アセットを更新
				Window.Update ();
			}
		}

		/// <summary>OAuth初期化</summary>
		private static async Task InitOAuth () {
			if (oAuth == null) {
				if (!string.IsNullOrEmpty (ClientId) && !string.IsNullOrEmpty (ClientSecret)) {
					oAuth = new GoogleOAuth (ClientId, ClientSecret, Scope, AccessToken, RefreshToken, applicationUrl: Application);
					(AccessToken, RefreshToken) = await oAuth.GetTokensAsync (silent: true);
				}
			} else {
				(AccessToken, RefreshToken) = oAuth.Tokens;
			}
		}

		/// <summary>OAuthトークン消去</summary>
		public static void ClearOAuth () {
			AccessToken = RefreshToken = "";
			oAuth.ClearTokens ();
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

			if (oAuth != null) {
				(AccessToken, RefreshToken) = oAuth.Tokens;
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

}
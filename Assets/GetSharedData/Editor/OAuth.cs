using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;
using Tetr4lab;

namespace OAuthHandler {

	public class GoogleOAuth {
		// ref: https://github.com/googlesamples/oauth-apps-for-windows/blob/master/OAuthConsoleApp/OAuthConsoleApp/Program.cs

		#region static

		private const string AuthorizationUri = "https://accounts.google.com/o/oauth2/v2/auth";
		private const string TokenRequestUri = "https://www.googleapis.com/oauth2/v4/token";
		private const string AccessTokenKey = "access_token";
		private const string RefreshTokenKey = "refresh_token";
		private const string ResponsePage = "<html><head><meta http-equiv='refresh' content='10;url=https://google.com'></head><body>Please return to the app.</body></html>";

		/// <summary>排他制御</summary>
		private static bool busy;

		/// <summary>未使用のポートを取得 (ref: http://stackoverflow.com/a/3978040)</summary>
		private static int GetUnusedPort () {
			var listener = new TcpListener (IPAddress.Loopback, 0);
			listener.Start ();
			try {
				return ((IPEndPoint) listener.LocalEndpoint).Port;
			} finally {
				listener.Stop ();
			}
		}

		/// <summary>指定の長さのランダムなバイト列を生成し、Base64urlに変換して返す</summary>
		private static string GenerateRandomDataBase64url (uint length) {
			var bytes = new byte [length];
			(new RNGCryptoServiceProvider ()).GetBytes (bytes);
			return bytes.ToBase64url ();
		}

		/// <summary>ASCII文字列のSHA256ハッシュ</summary>
		private static byte [] Sha256Ascii (string text) {
			var bytes = Encoding.ASCII.GetBytes (text);
			using (var sha256 = new SHA256Managed ()) {
				return sha256.ComputeHash (bytes);
			}
		}

#if UNITY_EDITOR_WIN
		// ウインドウのフォーカス制御
		// ref: https://dobon.net/vb/dotnet/process/appactivate.html#section3

		[DllImport ("user32.dll")]
		private static extern IntPtr GetForegroundWindow ();

		[DllImport ("user32.dll")]
		[return: MarshalAs (UnmanagedType.Bool)]
		private static extern bool SetForegroundWindow (IntPtr hWnd);
#endif

		/// <summary>簡易Json -> Dictionary変換</summary>
		private static Dictionary<string, string> Json2Dict (string json) {
			var dict = new Dictionary<string, string> { };
			var match = (new Regex (@"""(?<key>[^""]*)""\s*:\s*""(?<value>[^""]*)""")).Matches (json);
			for (var i = 0; i < match.Count; i++) {
				dict.Add (match [i].Groups ["key"].Value, match [i].Groups ["value"].Value);
			}
			return dict;
		}

		#endregion

		private string ClientId;
		private string ClientSecret;
		private string AccessToken;
		private string RefreshToken;
		private string Scope;
		/// <summary>対象のアプリ (トークンの検証に使用)</summary>
		public string ApplicationUrl;
		/// <summary>最後に使用されたトークン</summary>
		public (string access, string refresh) Tokens => (AccessToken, RefreshToken);

		/// <summary>コンストラクタ</summary>
		public GoogleOAuth (string clientId, string clientSecret, string scope, string accessToken = null, string refreshToken = null, string applicationUrl = null) {
			if (string.IsNullOrEmpty (clientId) || string.IsNullOrEmpty (clientSecret) || scope is null) { throw new ArgumentNullException (); }
			ClientId = clientId;
			ClientSecret = clientSecret;
			Scope = scope;
			AccessToken = accessToken;
			RefreshToken = refreshToken;
			ApplicationUrl = applicationUrl;
			busy = false;
			_ = GetTokensAsync (silent: true);
		}

		/// <summary>トークンを抹消</summary>
		public void ClearTokens () {
			AccessToken = RefreshToken = null;
		}

		/// <summary>トークンを取得</summary>
		public async Task<(string access, string refresh)> GetTokensAsync (CancellationToken cancellationToken = default, bool silent = false) {
			await TaskEx.DelayWhile (() => busy);
			busy = true;
			try {
				if (!string.IsNullOrEmpty (ApplicationUrl)) {
					// 既存のトークンを検証
					if (!string.IsNullOrEmpty (AccessToken) && await HttpPostAsync (ApplicationUrl, useAccessToken: true, silent: true) is object) {
						return (AccessToken, RefreshToken);
					}
					// リフレッシュの試行
					if (!string.IsNullOrEmpty (RefreshToken)) {
						var res = await HttpPostAsync (TokenRequestUri, $"client_id={ClientId}&client_secret={ClientSecret}&refresh_token={RefreshToken}&grant_type=refresh_token", silent: true);
						if (res is object && res.ContainsKey (AccessTokenKey)) {
							AccessToken = res [AccessTokenKey];
							return (AccessToken, RefreshToken);
						}
					}
				}
				// 認証要求を含むトークン取得
				ClearTokens ();
				if (silent) { return (AccessToken, RefreshToken); } // ユーザの手は患わせない
				// PKCE: Proof Key for Code Exchange by OAuth Public Clients
				var state = GenerateRandomDataBase64url (32);
				var codeVerifier = GenerateRandomDataBase64url (32);
				var codeChallenge = Sha256Ascii (codeVerifier).ToBase64url ();
				const string codeChallengeMethod = "S256";
				// 簡易サーバを立てる
				using (var http = new HttpListener ()) {
					// コンテキストのコンテナ
					HttpListenerContext context = null;
					// 承認後に表示するページのURI
					var redirectUri = $"http://{IPAddress.Loopback}:{GetUnusedPort ()}/";
					/// サーバ開始
					http.Prefixes.Add (redirectUri);
					http.Start ();
					try {
#if UNITY_EDITOR_WIN
						// 現在のウインドウを記録
						var foregroundWindow = GetForegroundWindow ();
#endif
						// OAuth認証要求のためにブラウザーを開く
						string authorizationRequest = $"{AuthorizationUri}?client_id={ClientId}&redirect_uri={Uri.EscapeDataString (redirectUri)}&scope={Scope}&response_type=code&approval_prompt=force&access_type=offline&state={state}&code_challenge={codeChallenge}&code_challenge_method={codeChallengeMethod}";
						Process.Start (authorizationRequest);
						// 中断可能な形で承認を待つ
						http.BeginGetContext (new AsyncCallback (result => {
							context = http.EndGetContext (result);
						}), null);
						await TaskEx.DelayUntil (() => context is object || cancellationToken.IsCancellationRequested);
						if (cancellationToken.IsCancellationRequested) {
							throw new OperationCanceledException ();
						}
#if UNITY_EDITOR_WIN
						// ウインドウのフォーカスを戻す
						SetForegroundWindow (foregroundWindow);
#endif
						// HTTPレスポンスをブラウザへ送出
						var response = context.Response;
						var buffer = Encoding.UTF8.GetBytes (ResponsePage);
						response.ContentLength64 = buffer.Length;
						using (var responseOutput = response.OutputStream) {
							await responseOutput.WriteAsync (buffer, 0, buffer.Length);
							responseOutput.Close ();
						}
					} finally {
						// サーバ終了
						http.Stop ();
					}
					// エラーチェック
					var error = context.Request.QueryString.Get ("error");
					if (error is object) {
						Debug.LogError ($"OAuth authorization error: {error}.");
						return (null, null);
					}
					if (context.Request.QueryString.Get ("code") is null || context.Request.QueryString.Get ("state") is null) {
						Debug.LogError ($"Malformed authorization response. {context.Request.QueryString}");
						return (null, null);
					}
					// 認証コードを抽出
					var code = context.Request.QueryString.Get ("code");
					// stateの検証
					var incomingState = context.Request.QueryString.Get ("state");
					if (incomingState != state) {
						Debug.LogError ($"Received request with invalid state ({incomingState})");
						return (null, null);
					}
					// 認証コードでトークンを取得
					var responce = await HttpPostAsync (TokenRequestUri, $"code={code}&redirect_uri={Uri.EscapeDataString (redirectUri)}&client_id={ClientId}&code_verifier={codeVerifier}&client_secret={ClientSecret}&grant_type=authorization_code");
					if (responce is object) {
						if (responce.ContainsKey (AccessTokenKey)) {
							AccessToken = responce [AccessTokenKey];
						}
						if (responce.ContainsKey (RefreshTokenKey)) {
							RefreshToken = responce [RefreshTokenKey];
						}
					}
				}
				return (AccessToken, RefreshToken);
			} finally {
				busy = false;
			}
		}

		/// <summary>URLにパラメータをPOSTする</summary>
		private async Task<Dictionary<string, string>> HttpPostAsync (string url, string param = "", bool useAccessToken = false, bool silent = false) {
			using (var client = await CreateHttpClientAsync (withAccessToken: useAccessToken))
			using (var content = new StringContent (param, Encoding.UTF8, "application/x-www-form-urlencoded")) {
				try {
					var response = await client.PostAsync (url, content);
					if (response.StatusCode == HttpStatusCode.OK) {
						try {
							var responseText = await response.Content.ReadAsStringAsync ();
							if ((new Regex (@"^\s*[\{\[]")).IsMatch (responseText)) {
								return Json2Dict (responseText);
							} else {
								if (!silent) Debug.LogWarning ($"oauth: not json\n{responseText}");
							}
						} catch (Exception exception) {
							if (!silent) Debug.LogWarning ($"oauth: {exception}");
						}
					} else {
						if (!silent) Debug.LogWarning ($"oauth: {response.StatusCode} {url}{(string.IsNullOrEmpty (param) ? "" : "?")}{param} {client.DefaultRequestHeaders.Authorization}");
					}
				} catch (Exception exception) {
					if (!silent) Debug.LogError (exception);
				}
			}
			return null;
		}

		/// <summary>トークン付きのHttpClientを生成して返す</summary>
		public async Task<HttpClient> CreateHttpClientAsync (CancellationToken cancellationToken = default, bool withAccessToken = true) {
			var client = new HttpClient ();
			if (withAccessToken) {
				if (string.IsNullOrEmpty (AccessToken)) { _ = await GetTokensAsync (cancellationToken); }
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue ("Bearer", AccessToken ?? "");
			}
			return client;
		}

	}

}

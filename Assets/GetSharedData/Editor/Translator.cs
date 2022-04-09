using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using OAuthHandler;

namespace GetSharedData {

	/// <summary>変換器</summary>
	public static class Translator {

		/// <summary>タイムアウト</summary>
		private const int WebTimeout = 10000;
		/// <summary>インターバル</summary>
		private const int WebInterval = 200;
		/// <summary>リトライ回数</summary>
		private const int RetryCount = 3;

		/// <summary>トランスレーター</summary>
		public static async Task<Parser> Translate (CancellationToken token, Progress<object> progress, GoogleOAuth oauth, string application, string document, string dstPath) {
			using (var client = await oauth.CreateHttpClientAsync (token))
			using (var dummy = new Log (document, token, progress)) {
				client.Timeout = TimeSpan.FromMilliseconds (WebTimeout);
				Log.Progress ($"started at {DateTime.Now}");
				var book = await getSpreadsheets (client, application, document);
				Log.Progress ("spreadsheet data recieved");
				var parser = new Parser (book);
				parser.Parse ();
				Log.Progress ($"finished at {DateTime.Now}");
				return parser;
			}
		}

		/// <summary>スプレッドシートを取得してjsonファイルを保存</summary>
		private static async Task<Book> getSpreadsheets (HttpClient client, string application, string document, params string [] names) {
			Exception lastException = null;
			Catalog<string> sheetNames = null;
			Catalog<int> sheetIDs = null;
			try {
				var sheets = new Book { };
				await getCatalog ();
				if (names.Length > 0) {
					for (var i = 0; i < names.Length; i++) {
						var index = sheetNames.IndexOf (names [i]);
						if (index < 0) {
							Log.Debug ($"not found sheet '{names [i]}'");
						} else {
							sheets.Add (names [i], await getSheet (index));
						}
					}
				} else {
					for (var i = 0; i < sheetNames.Length; i++) {
						if (new Regex (@"^[\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]*$").IsMatch (sheetNames [i])) {
							sheets.Add (sheetNames [i], await getSheet (i));
						}
                    }
                }
				await Task.Delay (WebInterval);
				return sheets;
			} catch (Exception exception) {
				Log.FatalError (exception, "LOAD ERROR (NETWORK)");
			}
			return null;

			// カタログの取得
			async Task getCatalog () {
				for (var retry = 1; retry < RetryCount; retry++) {
					try {
						Log.Debug ("get sheet catalog");
						var json = (await wwwPost (client, application, $"d={document}")).nameJson ("values");
						Log.WriteAllText ("SheetNames.json", json);
						sheetNames = Catalog<string>.FromJson (json);
						await Task.Delay (WebInterval);
						json = (await wwwPost (client, application, $"d={document}&id=true")).nameJson ("values");
						Log.WriteAllText ("SheetIDs.json", json);
						sheetIDs = Catalog<int>.FromJson (json);
						return;
					} catch (Exception exception) {
						Log.Debug ($"JSON ERROR {retry}: {lastException = exception}");
					}
				}
				Log.FatalError (lastException, "JSON ERROR (NETWORK)");
			}

			// シートの取得
			async Task<SpreadSheet> getSheet (int index) {
				for (var retry = 1; retry < RetryCount; retry++) {
					try {
						Log.Progress ($"load sheet '{sheetNames [index]}' (#{sheetIDs [index]})");
						await Task.Delay (WebInterval);
						var json = (await wwwPost (client, application, $"d={document}&s={sheetNames [index]}")).nameJson ("values");
						Log.WriteAllText ($"_{sheetNames [index]}.json", json);
						var sheetMatrix = new SpreadSheet (sheetIDs [index], sheetNames [index], Sheet<string>.FromJson (json));
						Log.Debug ($" loaded [{sheetMatrix.GetLength (0)}, {sheetMatrix.GetLength (1)}]");
						return sheetMatrix;
					} catch (KeyNotFoundException) {
						throw; // Abort
					} catch (Exception exception) {
						Log.Debug ($"JSON ERROR {retry}: {lastException = exception}");
					}
				}
				Log.FatalError (lastException, "JSON ERROR (NETWORK)");
				return null;
			}
		}

		/// <summary>jsonに名前を付ける</summary>
		private static string nameJson (this string json, string name) {
			return string.IsNullOrEmpty (name) ? json : $"{{\"{name}\":{json}}}";
		}

		/// <summary>urlとパラメータからレスポンスを取得して返す</summary>
		private static async Task<string> wwwPost (HttpClient client, string url, string param) {
			Exception lastException = null;
			for (var retry = 1; retry < RetryCount; retry++) {
				try {
					using (var content = new StringContent (Uri.EscapeUriString (param), Encoding.UTF8, "application/x-www-form-urlencoded")) {
						//Log.Debug ($"http request {url}?{param}");
						var response = await client.PostAsync (url, content);
						try {
							await Task.Delay (WebInterval);
						} catch (Exception exception) {
							Log.Debug ($"NETWORK_ERROR: {exception}");
						}
						if (response.StatusCode == HttpStatusCode.OK) {
							try {
								return await response.Content.ReadAsStringAsync ();
							} catch (Exception exception) {
								Log.Debug ($"NETWORK_ERROR: {exception} {response.StatusCode}");
							}
						} else {
							Log.Debug ($"NETWORK ERROR: {response.StatusCode}");
						}
					}
				} catch (TaskCanceledException exception) {
					Log.Debug ($"NETWORK ERROR {retry}: {lastException = exception}");
				} catch (Exception exception) {
					Log.FatalError (lastException = exception, "NETWORK ERROR");
				}
				await Task.Delay (WebInterval);
			}
			Log.FatalError (lastException, "NETWORK ERROR");
			return null;
		}

	}

}

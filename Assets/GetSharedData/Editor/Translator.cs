using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace GetSharedDataTranslator {

	/// <summary>変換器</summary>
	public static class Translator {

		private const int WebTimeout = 10000; // タイムアウト
		private const int WebInterval = 200; // インターバル
		private const int RetryCount = 3; // リトライ回数

		/// <summary>Httpハンドラ</summary>
		private static HttpClient httpClient = null;

		/// <summary>ハンドラの破棄</summary>
		public static void Dispose () {
			httpClient.Dispose ();
			httpClient = null;
		}

		/// <summary>トランスレーター</summary>
		public static async Task<Parser> Translate (CancellationToken token, Progress<object> progress, string application, string keyword, string document, string dstPath) {
			if (httpClient == null) { httpClient = new HttpClient (); }
			using (var dummy = new Log (document, token, progress)) {
				httpClient.Timeout = TimeSpan.FromMilliseconds (WebTimeout);
				Log.Progress ($"started at {DateTime.Now}");
				var book = await getSpreadsheets (application, keyword, document, "Text", "Const");
				Log.Progress ("spreadsheet data recieved");
				var parser = new Parser (book);
				parser.Parse ();
				Log.Progress ($"finished at {DateTime.Now}");
				return parser;
			}
		}

		/// <summary>スプレッドシートを取得してjsonファイルを保存</summary>
		private static async Task<Book> getSpreadsheets (string application, string keyword, string document, params string [] names) {
			Exception lastException = null;
			Catalog<string> sheetNames = null;
			Catalog<int> sheetIDs = null;
			var sheets = new Book { };
			await getCatalog ();
			for (var i = 0; i < names.Length; i++) {
				var index = sheetNames.IndexOf (names [i]);
				if (index < 0) {
					Log.Debug ($"not found sheet '{names [i]}'");
				} else {
					sheets.Add (names [i], await getSheet (index));
				}
			}
			await Task.Delay (WebInterval);
			return sheets;

			// カタログの取得
			async Task getCatalog () {
				for (var retry = 1; retry < RetryCount; retry++) {
					try {
						Log.Debug ("get sheet catalog");
						var json = (await wwwPost (application, $"k={keyword}&d={document}")).nameJson ("values");
						Log.WriteAllText ("SheetNames.json", json);
						sheetNames = Catalog<string>.FromJson (json);
						await Task.Delay (WebInterval);
						json = (await wwwPost (application, $"k={keyword}&d={document}&id=true")).nameJson ("values");
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
						var json = (await wwwPost (application, $"k={keyword}&d={document}&s={sheetNames [index]}")).nameJson ("values");
						Log.WriteAllText ($"_{sheetNames [index]}.json", json);
						var sheetMatrix = new SpreadSheet (sheetIDs [index], sheetNames [index], Sheet<string>.FromJson (json));
						Log.Debug ($" loaded [{sheetMatrix.GetLength (0)}, {sheetMatrix.GetLength (1)}]");
						return sheetMatrix;
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
		private static async Task<string> wwwPost (string url, string param, string data = "") {
			Exception lastException = null;
			for (var retry = 1; retry < RetryCount; retry++) {
				try {
					using (var content = new StringContent ($"{Uri.EscapeUriString (param)}{Uri.EscapeDataString (data)}", Encoding.UTF8, "application/x-www-form-urlencoded")) {
						//Log.Debug ($"http request {url}?{param}");
						var response = await httpClient.PostAsync (url, content);
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

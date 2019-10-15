using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace GetSharedDataTranslator {

	/// <summary>ログ</summary>
	public sealed class Log : IDisposable {

		/// <summary>ログ出力ディレクトリ</summary>
		public const string LogPath = "Assets/GetSharedData/Log~/";

		/// <summary>ログファイル</summary>
		public static TextWriter LogFile { get; private set; }

		/// <summary>クラス初期化</summary>
		static Log () {
			LogFile = default;
		}

		/// <summary>ドキュメントID</summary>
		private static string Document;

		/// <summary>進捗報告ハンドラ</summary>
		private static IProgress<object> iPprogress;

		/// <summary>中断進行中</summary>
		private static bool isAbortProgress;

		/// <summary>中断要求トークン</summary>
		private static CancellationToken Token;

		/// <summary>中断処理</summary>
		private static void logAbort (string message, Exception exception = null, bool force = false) {
			if (!force && isAbortProgress) { return; }
			Task.Delay (100);
			LogFile.Dispose ();
			LogFile = null;
			Document = null;
			GetSharedData.ErrorMessage = message;
			if (message.Contains ("NETWORK")) { Translator.Dispose (); } // 回線を閉じる
			Task.Delay (100);
			throw exception ?? new Exception ();
		}

		/// <summary>ログ出力</summary>
		public static void WriteLine (object line) {
			if (line == null) { line = "ERROR: Log.WriteLine: null"; }
			LogFile.WriteLine (line);
			if (Token.IsCancellationRequested) {
				logAbort ("GetSharedData: canceled by user");
			}
		}

		/// <summary>ログ記録</summary>
		private static void Logging (object message, string head = default) {
			var header = (head == default) ? "" : $"{head}: ";
			WriteLine ($"{header}{message}");
		}

		/// <summary>コンストラクタ</summary>
		public Log (string document, CancellationToken token, IProgress<object> progress) {
			Token = token;
			iPprogress = progress;
			if (LogFile != default) { LogFile.Close (); }
			if (Directory.Exists (LogPath)) {
				DeleteFiles (LogPath);
			} else {
				Directory.CreateDirectory (LogPath);
			}
			var logFile = File.CreateText ($"{LogPath}Debug.log");
			logFile.AutoFlush = true;
			LogFile = logFile;
			LogFile.NewLine = "\n";
			Document = document;
		}
		public void Dispose () => LogFile.Close ();

		/// <summary>フォルダ内のファイルを削除</summary>
		public static void DeleteFiles (string path, string pattern = "*") {
			foreach (var file in Directory.GetFiles (path, pattern)) {
				File.Delete (file);
			}
		}

		/// <summary>テキストをログファイルに書き出す</summary>
		public static void WriteAllText (string name, string text) => File.WriteAllText ($"{LogPath}{name}", text);

		/// <summary>進捗報告</summary>
		public static void Progress (object message) {
			iPprogress.Report (message);
			Logging (message);
		}

		/// <summary>ログ記録</summary>
		public static void Debug (object message) {
			Logging (message);
		}

		/// <summary>警告記録</summary>
		public static void Worning (object message) {
			Logging (message, "WORNING");
			Task.Delay (200);
			FatalError (null, message);
		}

		/// <summary>エラー記録</summary>
		public static void Error (object message) {
			Logging (message, "ERROR");
			Task.Delay (200);
			FatalError (null, message);
		}

		/// <summary>致命的エラー</summary>
		public static void FatalError (Exception exception, object message = null) {
			isAbortProgress = true;
			message = $"{message ?? "GetSharedData"}: {exception}";
			Logging (message, "FATAL EXCEPTION ERROR");
			logAbort (message.ToString (), exception, true);
		}

	}

}

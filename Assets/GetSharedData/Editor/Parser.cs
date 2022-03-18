using System;
using System.Collections;
using System.Collections.Generic;
using SystemLanguage = UnityEngine.SystemLanguage;

namespace GetSharedDataTranslator {

	/// <summary>解析器</summary>
	public sealed class Parser {

		/// <summary>スプレッドシートBook</summary>
		public Book Book { get; private set; }

		/// <summary>キー</summary>
		public List<string> TextKeys { get; private set; }

		/// <summary>文字列</summary>
		public Dictionary<SystemLanguage, List<string>> TextStrings { get; private set; }

		/// <summary>言語</summary>
		public List<SystemLanguage> Languages { get; private set; }

		/// <summary>定数</summary>
		public Dictionary<string, List<string>> Constants { get; private set; }

		/// <summary>コンストラクタ</summary>
		public Parser (Book sheets) {
			Book = sheets;
		}

		/// <summary>解析</summary>
		public void Parse () {
			// テキスト
			var rows = Book ["Text"].GetRows (key => Enum.TryParse<SystemLanguage> (key, out _), "Key", "Comment");
			TextKeys = new List<string> { };
			TextStrings = new Dictionary<SystemLanguage, List<string>> { };
			Languages = new List<SystemLanguage> { };
			if (rows [0] [0] == "Key") {
				for (var r = 1; r < rows.Count; r++) {
					TextKeys.Add ($"public const int @{rows [r] [0]} = {r - 1}; // {rows [r] [1]}");
				}
			}
			for (var c = 2; c < rows [0].Count; c++) {
				if (Enum.TryParse<SystemLanguage> (rows [0] [c], out var language)) {
					Languages.Add (language);
					var list = new List<string> { };
					for (var r = 1; r < rows.Count; r++) {
						list.Add (rows [r] [c]);
					}
					TextStrings.Add (language, list);
				}
			}
			// 定数
			Constants = new Dictionary<string, List<string>> { };
			foreach (var sheetName in Book.Keys) {
				if (sheetName == "Text") continue;
				Constants.Add (sheetName, new List<string> { });
				rows = Book [sheetName].GetRows (null, "Key", "Type", "Value", "Comment");
				for (var r = 1; r < rows.Count; r++) {
					var head = "";
					var type = rows [r] [1].ToLower ();
					var name = rows [r] [0];
					var value = rows [r] [2];
					var comment = rows [r] [3];
					switch (type) {
						case "int":
							if (int.TryParse (value, out var @int)) { value = @int.ToString (); } else error ();
							break;
						case "float":
							if (float.TryParse (value, out var @float)) { value = $"{@float}f"; } else error ();
							break;
						case "string":
							value = $"\"{value.Escape ()}\"";
							break;
						case "bool":
							if (bool.TryParse (value, out var @bool)) { value = @bool.ToString ().ToLower (); } else error ();
							break;
						default:
							head = "//";
							break;
					}
					Constants [sheetName].Add ($"{head}public const {type} @{name} = {value}; // {comment}");
					void error () { comment += "  /// ERROR ///"; head = "//"; }
				}
			}
		}

	}

}

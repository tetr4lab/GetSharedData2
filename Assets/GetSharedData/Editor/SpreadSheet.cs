using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GetSharedDataTranslator {

	/// <summary>ブック</summary>
	public sealed class Book : Dictionary<string, SpreadSheet> {

		/// <summary>キーの存在をチェックしてシートを得る 必要に応じてログを吐く</summary>
		public SpreadSheet GetSheetWithKey (string name, params string [] keys) {
			if (ContainsKey (name)) {
				var sheet = this [name];
				if (sheet.HasKey (keys)) {
					return sheet;
				} else {
					Log.Error ($"key not found ('{string.Join ("', '", keys)}')");
				}
			} else {
				Log.Error ($"sheet not found '{name}'");
			}
			return null;
		}

	}

	/// <summary>スプレッドシート</summary>
	public sealed class SpreadSheet : Sheet<string> {

		/// <summary>無効なインデックス</summary>
		[IgnoreDataMember] public static readonly (int row, int column) InvalidIndex = (-1, -1);

		/// <summary>名前</summary>
		public string Name { get; private set; }

		/// <summary>シートID</summary>
		public int Id { get; private set; }

		/// <summary>見出し行番号</summary>
		public int KeyRow { get; private set; }

		/// <summary>キーの並び</summary>
		public string [] KeyList { get; private set; }

		/// <summary>キーの列番号</summary>
		public int IndexOf (string key) => (KeyList == null) ? -1 : Array.IndexOf (KeyList, key);

		/// <summary>キーの存在確認</summary>
		public bool HasKey (params string [] keys) {
			foreach (var key in keys) {
				if (IndexOf (key) < 0) { return false; }
			}
			return true;
		}

		/// <summary>指定範囲を返す</summary>
		public List<List<string>> GetRows (Func<string, bool> whereKey, params string [] keys) {
			var rows = new List<List<string>> { };
			var keycols = new List<int> { };
			foreach (var key in keys) {
				var col = IndexOf (key);
				if (col < 0) {
					Log.Error ($"key not found '{key}'");
				} else {
					keycols.Add (col);
				}
			}
			if (whereKey != null) {
				for (var key = 0; key < KeyList.Length; key++) {
					if ((keys == null || Array.IndexOf (keys, KeyList [key]) < 0) && whereKey (KeyList [key])) {
						keycols.Add (key);
					}
				}
			}
			if (keycols.Count > 0) {
				for (var r = KeyRow; r < GetLength (0); r++) {
					var name = this [r, keycols [0]];
					if (!string.IsNullOrEmpty (name)) {
						var row = new List<string> { name };
						for (var k = 1; k < keycols.Count; k++) {
							row.Add (this [r, keycols [k]] ?? "");
						}
						rows.Add (row);
					}
				}
			}
			return rows;
		}

		/// <summary>コンストラクタ</summary>
		public SpreadSheet () { Id = -1; }

		/// <summary>コンストラクタ</summary>
		/// <param name="id">シートID</param>
		/// <param name="sheet">文字列マトリクス</param>
		/// <param name="complementEmptyKey">空欄キーの補間</param>
		public SpreadSheet (int id, string name, Sheet<string> sheet, bool complementEmptyKey = false) {
			Id = id;
			Name = name;
			values = sheet.values;
			if (Id < int.MaxValue) {
				var index = FindIndex ("Key"); // あるはずのキーを探す
				if (index == InvalidIndex) {
					Log.Error ("not found the column 'Key'");
				} else {
					KeyRow = index.row;
					KeyList = values [KeyRow];
				}
			}
		}

		/// <summary>指定されたキーが最初に出現するインデックスを返す</summary>
		private (int row, int column) FindIndex (string key) {
			for (int r = 0; r < values.Length; r++) {
				for (int c = 0; c < values [r].Length; c++) {
					if (key.Equals (values [r] [c])) {
						return (r, c);
					}
				}
			}
			return InvalidIndex;
		}

	}

}

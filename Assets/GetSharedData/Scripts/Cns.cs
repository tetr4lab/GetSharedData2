using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using ScriptableClass;

namespace SharedConstant {

	/// <summary>定数</summary>
	public static partial class Cns { }

	/// <summary>テキスト</summary>
	public static partial class Txt {

		/// <summary>テキストアセット名の先頭</summary>
		public const string TextAssetPrefix = "Text_";

		/// <summary>テキストアセットのアドレスパス</summary>
		public const string TextAssetPath = "Texts/";

		/// <summary>文字列</summary>
		private static ScriptableStringList str = default;

		/// <summary>準備完了</summary>
		public static bool IsReady => (str != default && locale == select);

		/// <summary>正引き</summary>
		public static string S (params int [] nams) => IsReady ? string.Join ("", Array.ConvertAll (nams, (nam) => str [nam])) : "";

		/// <summary>正引き (Format)</summary>
		public static string F (int format, params object [] args) => string.Format (S (format), args);

		/// <summary>逆引き (インデックス)</summary>
		public static int N (string item) => str.IndexOf (item);

		/// <summary>言語設定 (結果)</summary>
		private static SystemLanguage locale = SystemLanguage.Unknown;

		/// <summary>言語設定 (選択)</summary>
		private static SystemLanguage select = SystemLanguage.Unknown;

		/// <summary>ローダー</summary>
		private static AsyncOperationHandle<ScriptableStringList> TextLoader;

		/// <summary>初期化</summary>
		static Txt () {
			Locale = SystemLanguage.Unknown;
		}

		/// <summary>設定・取得</summary>
		public static SystemLanguage Locale {
			get { return (str != default) ? locale : SystemLanguage.Unknown; }
			set {
				if (value == SystemLanguage.Unknown) {
					value = Application.systemLanguage; // 無指定ならシステム設定に従う
				}
				if (Array.IndexOf (Languages, value) < 0) {
					value = Languages [0]; // 非対応ならデフォルト言語にする
				}
				if (select == value) { return; }
				select = value;
				if (TextLoader.IsValid ()) {
					TextLoader.Completed -= OnLoad;
					Addressables.Release (TextLoader);
				}
				TextLoader = Addressables.LoadAssetAsync<ScriptableStringList> ($"{TextAssetPath}{TextAssetPrefix}{select}.asset");
				if (TextLoader.IsValid ()) {
					TextLoader.Completed += OnLoad;
				}
			}
		}

		/// <summary>完了コールバック処理</summary>
		private static void OnLoad (AsyncOperationHandle<ScriptableStringList> loader) {
			TextLoader.Completed -= OnLoad;
			if (loader.Status == AsyncOperationStatus.Succeeded) {
				str = loader.Result;
				locale = select;
				Debug.Log ($"Addressables.LoadAssetAsync Loadedlocale={locale}");
			} else {
				Debug.LogError ($"Addressables.LoadAssetAsync Error select={select}");
			}
		}

	}

	/// <summary>テキスト名</summary>
	public static partial class Nam { }

}

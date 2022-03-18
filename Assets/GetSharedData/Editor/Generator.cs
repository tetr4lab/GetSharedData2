using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GetSharedDataTranslator;
using ScriptableClass;
using SharedConstant;

/// <summary>アセット生成</summary>
public static class Generator {

	/// <summary>生成</summary>
	/// スクリプタブルアセットを生成するため、メインスレッドで実行する必要がある。
	public static bool Generate (this Parser parser, string path) {
		try {
			// 言語別文字列
			foreach (var language in parser.Languages) {
				using (var asset = new ScriptableObjectHandler<ScriptableStringList> (Path.Combine (path, $"{Txt.TextAssetPrefix}{language}.asset"))) {
					asset.Object.Clear ();
					for (var key = 0; key < parser.TextKeys.Count; key++) {
						if (!parser.TextKeys [key].StartsWith ("//")) {
							asset.Object.Add (parser.TextStrings [language] [key] ?? string.Empty);
						}
					}
				}
			}
			// 文字列目録
			File.WriteAllText (Path.Combine (path, "Text.cs"), 
$@"using UnityEngine;

namespace {nameof (SharedConstant)} {{

	public static partial class {nameof (Txt)} {{
		/// <summary>収録言語</summary>
		public static readonly SystemLanguage [] Languages = {{
			{string.Join ($",{Environment.NewLine}\t\t\t", parser.Languages.ConvertAll (l => $"SystemLanguage.{l}"))}
		}};
	}}

	/// <summary>テキスト</summary>
	public static partial class {nameof (Nam)} {{
		{string.Join ($"{Environment.NewLine}\t\t", parser.TextKeys)}
	}}

}}
");

			// 定数
			File.WriteAllText (Path.Combine (path, "Number.cs"),string.Join ($"{Environment.NewLine}",
$@"// 定数
namespace {nameof (SharedConstant)} {{
	public static partial class {nameof (Cns)} {{
		{string.Join ($"{Environment.NewLine}\t\t", parser.Constants ["Const"])}
	}}
}}
", string.Join ($"{Environment.NewLine}", new List<string> (parser.Constants.Keys).FindAll (key => key != "Text" && key != "Const").ConvertAll (key => 
$@"public partial class @{key} {{
	{string.Join ($"{Environment.NewLine}\t", parser.Constants [key])}
}}
"))
));
			return true;
		} catch (Exception e) {
			GetSharedData.ErrorMessage = $"GetSharedDataGenarator: {e}";
			return false;
		}
	}

}


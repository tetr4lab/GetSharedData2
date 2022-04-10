using UnityEngine;

namespace SharedConstant {

	public static partial class Txt {
		/// <summary>収録言語</summary>
		public static readonly SystemLanguage [] Languages = {
			SystemLanguage.English,
			SystemLanguage.Japanese,
			SystemLanguage.Chinese
		};
	}

	/// <summary>テキスト</summary>
	public static partial class Nam {
		public const int @None = 0; // なし
		public const int @Language = 1; // 外部言語名
		public const int @Welcome = 2; // 記入例
		public const int @Welcome_ = 3;
	}

}

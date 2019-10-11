namespace SharedConstant {

	public static partial class Cns {
		// 定数
		public const int Test = 15; // 整数記入例
		public const float Test2 = 1.2f; // 浮動小数点数
		public const string Test3 = "あいう\"かき\"えお"; // 文字列
		public const bool Test4 = true; // 真偽値
		/// <summary>ビルド番号</summary>
#if UNITY_STANDALONE
		public const string BuildNumber = "0";
#elif UNITY_IPHONE
		public const string BuildNumber = "0";
#elif UNITY_ANDROID
		public const string BuildNumber = "1";
#else
		public const string BuildNumber = "1.0.0.0";
#endif
		/// <summary>バンドルバージョン</summary>
		public const string BundleVersion = "0.1";
	}

}

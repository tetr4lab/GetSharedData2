namespace SharedConstant {

	public static partial class Cns {
		/// <summary>ビルド番号</summary>
#if UNITY_STANDALONE
		public const string BuildNumber = "2";
#elif UNITY_IOS
		public const string BuildNumber = "2";
#elif UNITY_ANDROID
		public const string BuildNumber = "2";
#else
		public const string BuildNumber = "1.0.0.0";
#endif
		/// <summary>バンドルバージョン</summary>
		public const string BundleVersion = "0.1";
	}

}

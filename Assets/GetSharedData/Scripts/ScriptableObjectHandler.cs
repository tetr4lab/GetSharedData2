using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ScriptableClass {

	/// <summary>スクリプタブルオブジェクト・ハンドラ</summary>
	public sealed class ScriptableObjectHandler<T>
#if UNITY_EDITOR
		: IDisposable where T
#endif
		: ScriptableObject {

		/// <summary>対象オブジェクト</summary>
		public T Object { get; private set; }

#if UNITY_EDITOR
		/// <summary>開くまたは新規作成</summary>
		public ScriptableObjectHandler (string path) {
			Object = AssetDatabase.LoadAssetAtPath<T> (path);
			if (Object == null) {
				Object = ScriptableObject.CreateInstance<T> ();
				AssetDatabase.CreateAsset (Object, path);
			}
			EditorUtility.SetDirty (Object);
		}

		/// <summary>閉じる</summary>
		public void Dispose () {
			if (Object != null) {
				EditorUtility.SetDirty (Object);
				AssetDatabase.SaveAssets ();
				Object = null;
			}
		}
#endif

	}

}

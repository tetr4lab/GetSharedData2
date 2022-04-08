using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>スクリプタブルオブジェクト・ハンドラ</summary>
public sealed class ScriptableObjectHandler<T> : IDisposable where T : ScriptableObject {

	/// <summary>対象オブジェクト</summary>
	public T Object { get; private set; }

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

	// 型変換
	public static implicit operator T (ScriptableObjectHandler<T> handler) => handler.Object;

}


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptableClass {

	/// <summary>スクリプタブルリスト</summary>
	public class ScriptableList<T> : ScriptableObject {
		[SerializeField] protected List<T> values = new List<T> ();
		public virtual List<T> Values => values;
		public virtual T this [int index] { get => values [index]; set { values [index] = value; } }
		public virtual List<T>.Enumerator GetEnumerator () => values.GetEnumerator ();
		public virtual void Add (T item) => values.Add (item);
		public virtual void AddRange (IEnumerable<T> items) => values.AddRange (items);
		public virtual int IndexOf (T item) => values.IndexOf (item);
		public virtual void Clear () => values.Clear ();
		public virtual int Count => values.Count;
	}

	/// <summary>スクリプタブル辞書</summary>
	public class ScriptableDictionary<TKey, TValue> : ScriptableObject {
		[SerializeField] protected List<TKey> keys = new List<TKey> ();
		[SerializeField] protected List<TValue> values = new List<TValue> ();
		public virtual List<TKey> Keys => keys;
		public virtual List<TValue> Values => values;
		public virtual int Count => keys.Count;
		public virtual TValue this [TKey key] { get => values [keys.IndexOf (key)]; set { values [keys.IndexOf (key)] = value; } }
		public virtual void Add (TKey key, TValue value) { keys.Add (key); values.Add (value); }
		public virtual void AddRange (IDictionary<TKey, TValue> items) { keys.AddRange (items.Keys); values.AddRange (items.Values); }
		public virtual void Clear () { keys.Clear (); values.Clear (); }
		public virtual bool ContainsKey (TKey key) => keys.Contains (key);
		public virtual bool ContainsValye (TValue value) => values.Contains (value);
	}

}

using System;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace GetSharedData {

	/// <summary>シリアライズ可能な2次元配列(として振る舞うジャグ配列)</summary>
	[DataContract]
	public class Sheet<T> : IEnumerable {

		/// <summary>実体</summary>
		[DataMember] public T [] [] values = default;

		/// <summary>偽装</summary>
		public virtual T this [int row, int column] { // セル
			get => values [row] [column];
			set { values [row] [column] = value; }
		}
		public virtual T [] this [int index] => values [index]; // 行
		public virtual int Length => values.Length * values [0].Length;
		public virtual int GetLength (int d) => (d == 0) ? values.Length : values [0].Length;

		/// <summary>イテレータ</summary>
		public IEnumerator GetEnumerator () => new SheetMatrixEnumerator (this);
		protected class SheetMatrixEnumerator : IEnumerator {
			private Sheet<T> matrix;
			private int currentIndex;
			public object Current => (currentIndex >= 0 && currentIndex < matrix.Length) ? (object) matrix [currentIndex / matrix.GetLength (1), currentIndex % matrix.GetLength (1)] : null;
			public bool MoveNext () { return ++currentIndex < matrix.Length; }
			public void Reset () { currentIndex = -1; }
			public SheetMatrixEnumerator (Sheet<T> matrix) { this.matrix = matrix; }
		}

		/// <summary>コンストラクタ</summary>
		public Sheet () { }

		/// <summary>コンストラクタ</summary>
		public Sheet (int row, int column) {
			values = new T [row] [];
			for (var r = 0; r < row; r++) {
				values [r] = new T [column];
			}
		}

		/// <summary>シリアライザ</summary>
		public string ToJson () {
			return $"[ {string.Join (", ", Array.ConvertAll (values, r => $"[ {string.Join (", ", Array.ConvertAll (r, c => $"\"{c.ToString ().Escape ()}\""))} ]"))} ]";
		}

		/// <summary>デシリアライザ</summary>
		public static Sheet<T> FromJson (string json) => (Sheet<T>) new DataContractJsonSerializer (typeof (Sheet<T>)).ReadObject (new MemoryStream (Encoding.UTF8.GetBytes (json)));

	}

	/// <summary>シリアライズ可能な1次元配列</summary>
	[DataContract]
	public class Catalog<T> : IEnumerable {

		/// <summary>実体</summary>
		[DataMember] public T [] values = default;

		/// <summary>偽装</summary>
		public virtual int Length => values.Length;
		public virtual T this [int i] => values [i];
		public int IndexOf (T item) => Array.IndexOf (values, item);

		/// <summary>イテレータ</summary>
		public IEnumerator GetEnumerator () => values.GetEnumerator ();

		/// <summary>コンストラクタ</summary>
		public Catalog () { }

		/// <summary>コンストラクタ</summary>
		public Catalog (int size) {
			values = new T [size];
		}

		/// <summary>コンストラクタ</summary>
		public Catalog (IReadOnlyList<T> list) {
			values = new T [list.Count];
			for (var i = 0; i < values.Length; i++) {
				values [i] = list [i];
			}
		}

		/// <summary>シリアライザ</summary>
		public string ToJson () {
			return $"[ {string.Join (", ", Array.ConvertAll (values, c => $"\"{c.ToString ()}\""))} ]";
		}

		/// <summary>デシリアライザ</summary>
		public static Catalog<T> FromJson (string json) => (Catalog<T>) new DataContractJsonSerializer (typeof (Catalog<T>)).ReadObject (new MemoryStream (Encoding.UTF8.GetBytes (json)));

	}

	/// <summary>文字列拡張</summary>
	public static class StringExtensions {

		/// <summary>エスケープ</summary>
		public static string Escape (this string str) {
			return (str == null) ? str : str
				.Replace ("\\", "\\\\")
				.Replace ("\r", "\\r")
				.Replace ("\n", "\\n")
				.Replace ("\t", "\\t")
				.Replace ("\v", "\\v")
				.Replace ("\f", "\\f")
				.Replace ("\b", "\\b")
				.Replace ("\0", "\\0")
				.Replace ("\"", "\\\"")
				//.Replace ("\'", "\\\'")
				;
		}

		/// <summary>逆エスケープ</summary>
		public static string Unescape (this string str) {
			return (str == null) ? str : str
				.Replace ("\\r", "\r")
				.Replace ("\\n", "\n")
				.Replace ("\\t", "\t")
				.Replace ("\\v", "\v")
				.Replace ("\\f", "\f")
				.Replace ("\\b", "\b")
				.Replace ("\\0", "\0")
				.Replace ("\\\"", "\"")
				//.Replace ("\\\'", "\'")
				.Replace ("\\\\", "\\")
				;
		}

	}

}

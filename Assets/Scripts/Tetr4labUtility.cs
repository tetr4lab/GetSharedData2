//	Copyright© tetr4lab.

using System;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Tetr4lab {

	/// <summary>タスク拡張</summary>
	public static class TaskEx {

		/// <summary>休止間隔</summary>
		private const int Tick = 16;

		/// <summary>1フレーム待機</summary>
		public static Task DelayOneFrame => Task.Delay (Tick);

		/// <summary>条件が成立する間待機</summary>
		/// <param name="predicate">条件</param>
		/// <param name="limit">msec単位の制限</param>
		/// <param name="tick">刻み</param>
		public static async Task DelayWhile (Func<bool> predicate, int limit = 0, int tick = 0) {
			tick = (tick > 0) ? tick : Tick;
			if (limit <= 0) {
				while (predicate ()) {
					await Task.Delay (tick);
				}
			} else {
				limit /= tick;
				while (predicate () && limit-- > 0) {
					await Task.Delay (tick);
				}
			}
		}

		/// <summary>条件が成立するまで待機</summary>
		/// <param name="predicate">条件</param>
		/// <param name="limit">msec単位の制限</param>
		/// <param name="tick">刻み</param>
		public static async Task DelayUntil (Func<bool> predicate, int limit = 0, int tick = 0) {
			tick = (tick > 0) ? tick : Tick;
			if (limit <= 0) {
				while (!predicate ()) {
					await Task.Delay (tick);
				}
			} else {
				limit /= tick;
				while (!predicate () && limit-- > 0) {
					await Task.Delay (tick);
				}
			}
		}

	}

	/// <summary>汎用ユーティリティ</summary>
	public static class Tetr4labUtility {

		/// <summary>バイト列をBase64urlに変換</summary>
		public static string ToBase64url (this byte [] buffer) => Convert.ToBase64String (buffer).Trim ('=').Replace ("+", "-").Replace ("/", "_");

		/// <summary>文字列をBase64urlに変換</summary>
		public static string ToBase64url (this string target) => Convert.ToBase64String (Encoding.UTF8.GetBytes (target)).Trim ('=').Replace ("+", "-").Replace ("/", "_");

	}

}

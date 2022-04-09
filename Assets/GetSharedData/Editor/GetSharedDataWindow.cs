using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>エディタウインドウ</summary>
public sealed class GetSharedDataWindow : EditorWindow {

    #region Static

    // アイコン画像のパス
    private const string WindowIconPath = "Assets/GetSharedData/Textures/SpreadsheetFavicon3.png";

	/// <summary>認証設定の開閉状態</summary>
	private static bool OAuthSettingsIsOpen = false;
	private const string OAuthSettingsIsOpenPrefKey = "GetSharedData/OAuthSettingsIsOpen";

	/// <summary>ビルド番号設定の開閉状態</summary>
	private static bool BuildNumberSettingsIsOpen = false;
	private const string BuildNumberSettingsIsOpenPrefKey = "GetSharedData/BuildNumberSettingsIsOpen";

	/// <summary>ウィンドウオブジェクト</summary>
	private static GetSharedDataWindow SingletonObject = null;

	/// <summary>ウインドウを開く</summary>
	[MenuItem ("Window/GetSharedData/Open Setting Window")]
	private static void Open () {
		SingletonObject = GetWindow<GetSharedDataWindow> ();
		var icon = AssetDatabase.LoadAssetAtPath<Texture> (WindowIconPath);
		SingletonObject.titleContent = new GUIContent ("GetSharedData", icon);
	}

	/// <summary>ウインドウを更新</summary>
	public static void Update () => SingletonObject?.Repaint ();

	#endregion Static

	/// <summary>破棄</summary>
	private void OnDestroy () => SingletonObject = null;

	/// <summary>有効化</summary>
	private void OnEnable () {
		OAuthSettingsIsOpen = EditorPrefs.GetBool (OAuthSettingsIsOpenPrefKey, false);
		BuildNumberSettingsIsOpen = EditorPrefs.GetBool (BuildNumberSettingsIsOpenPrefKey, false);
		GetSharedData.LoadPrefs ();
	}

	/// <summary>無効化</summary>
	private void OnDisable () {
		EditorPrefs.SetBool (OAuthSettingsIsOpenPrefKey, OAuthSettingsIsOpen);
		EditorPrefs.SetBool (BuildNumberSettingsIsOpenPrefKey, BuildNumberSettingsIsOpen);
		GetSharedData.SavePrefs ();
	}

	/// <summary>表示</summary>
	private void OnGUI () {
		EditorGUILayout.LabelField ("Project Settings", EditorStyles.boldLabel);
		EditorGUILayout.BeginHorizontal ();
		GUILayout.Space (10f);
		EditorGUILayout.BeginVertical ();
		// document
		GetSharedData.Document = EditorGUILayout.TextField (new GUIContent ("Document ID", "the ID of Google Spreadsheet"), GetSharedData.Document);
		// asset path
		GetSharedData.AssetPath = EditorGUILayout.TextField (new GUIContent ("Asset Folder", "the path of assets to create"), GetSharedData.AssetPath);
		EditorGUILayout.EndVertical ();
		EditorGUILayout.EndHorizontal ();

		// get button
		GUILayout.Space (20f);
		EditorGUILayout.BeginHorizontal ();
		GUILayout.Space (10f);
		EditorGUI.BeginDisabledGroup (EditorApplication.isCompiling || GetSharedData.IsTranslating || string.IsNullOrEmpty (GetSharedData.Application) || string.IsNullOrEmpty (GetSharedData.Document) || string.IsNullOrEmpty (GetSharedData.AssetPath));
		if (GUILayout.Button ("GetSharedData", GUILayout.Height (30))) { GetSharedData.GetData (); }
		EditorGUI.EndDisabledGroup ();
		GUILayout.Space (40f);
		// abort button
		EditorGUI.BeginDisabledGroup (EditorApplication.isCompiling || !GetSharedData.IsTranslating || GetSharedData.IsCancellationRequested);
		if (GUILayout.Button ("Abort", GUILayout.Height (30), GUILayout.Width (100))) { GetSharedData.Abort (); }
		EditorGUI.EndDisabledGroup ();
		GUILayout.Space (10f);
		EditorGUILayout.EndHorizontal ();
		GUILayout.Space (20f);

		// build number
		BuildNumberSettingsIsOpen = EditorGUILayout.Foldout (BuildNumberSettingsIsOpen, "Build/Bundle Number Settings", false);
		if (BuildNumberSettingsIsOpen) {
			EditorGUILayout.BeginHorizontal ();
			GUILayout.Space (20f);
			EditorGUILayout.BeginVertical ();
			GetSharedData.UnifiedNumber = EditorGUILayout.Toggle (new GUIContent ("Unified", "give all targets the maximum build number"), GetSharedData.UnifiedNumber);
			GetSharedData.AutoIncrement = EditorGUILayout.Toggle (new GUIContent ("Auto Increment", "automatically increment build number after build"), GetSharedData.AutoIncrement);
			EditorGUILayout.EndVertical ();
			EditorGUILayout.EndHorizontal ();
		}
		GUILayout.Space (10f);

		// oauth
		OAuthSettingsIsOpen = EditorGUILayout.Foldout (OAuthSettingsIsOpen, "OAuth Settings", false);
		if (OAuthSettingsIsOpen) {
			EditorGUILayout.BeginHorizontal ();
			GUILayout.Space (10f);
			EditorGUILayout.BeginVertical ();
			// application
			GetSharedData.Application = EditorGUILayout.TextField (new GUIContent ("Application URL", "the URL of Google Apps Script"), GetSharedData.Application);
			// client id
			GetSharedData.ClientId = EditorGUILayout.TextField (new GUIContent ("Client ID", "the ID for Google Apps Script"), GetSharedData.ClientId);
			// client secret
			GetSharedData.ClientSecret = EditorGUILayout.TextField (new GUIContent ("Client Secret", "the secret for Google Apps Script"), GetSharedData.ClientSecret);
			// tokens
			EditorGUILayout.BeginHorizontal ();
			EditorGUILayout.BeginVertical ();
			// access token
			EditorGUI.BeginDisabledGroup (true);
			EditorGUILayout.TextField (new GUIContent ("Access Token"), GetSharedData.AccessToken);
			EditorGUI.EndDisabledGroup ();
			// refresh token
			EditorGUI.BeginDisabledGroup (true);
			EditorGUILayout.TextField (new GUIContent ("Refresh Token"), GetSharedData.RefreshToken);
			EditorGUI.EndDisabledGroup ();
			EditorGUILayout.EndVertical ();
			EditorGUI.BeginDisabledGroup ((string.IsNullOrEmpty (GetSharedData.AccessToken) && string.IsNullOrEmpty (GetSharedData.RefreshToken)) || EditorApplication.isCompiling || GetSharedData.IsTranslating);
			if (GUILayout.Button ("Clear", GUILayout.Height (30), GUILayout.Width (60))) { GetSharedData.ClearOAuth (); }
			EditorGUI.EndDisabledGroup ();
			EditorGUILayout.EndHorizontal ();
			// guid
			EditorGUILayout.HelpBox ("Shared settings between projects.", MessageType.None);
			// 
			EditorGUILayout.EndVertical ();
			EditorGUILayout.EndHorizontal ();
		}
	}

}

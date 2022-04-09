using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace GetSharedData {

	/// <summary>�G�f�B�^�E�C���h�E</summary>
	public sealed class Window : EditorWindow {

		#region Static

		/// <summary>�A�C�R���摜�̃p�X</summary>
		private const string WindowIconPath = "Assets/GetSharedData/Textures/SpreadsheetFavicon3.png";

		/// <summary>�F�ؐݒ�̊J���</summary>
		private static bool OAuthSettingsIsOpen = false;
		/// <summary>�r���h�ԍ��ݒ�̊J���</summary>
		private static bool BuildNumberSettingsIsOpen = false;

		/// <summary>�F�ؐݒ�̊J��� �ۑ��L�[</summary>
		private const string OAuthSettingsIsOpenPrefKey = "GetSharedData/OAuthSettingsIsOpen";
		/// <summary>�r���h�ԍ��ݒ�̊J��� �ۑ��L�[</summary>
		private const string BuildNumberSettingsIsOpenPrefKey = "GetSharedData/BuildNumberSettingsIsOpen";

		/// <summary>�E�B���h�E�I�u�W�F�N�g</summary>
		private static Window SingletonObject = null;

		/// <summary>�E�C���h�E���J��</summary>
		[MenuItem ("Window/GetSharedData/Open Setting Window")]
		private static void Open () {
			SingletonObject = GetWindow<Window> ();
			var icon = AssetDatabase.LoadAssetAtPath<Texture> (WindowIconPath);
			SingletonObject.titleContent = new GUIContent ("GetSharedData", icon);
		}

		/// <summary>�E�C���h�E���X�V</summary>
		public static void Update () => SingletonObject?.Repaint ();

		#endregion Static

		/// <summary>�j��</summary>
		private void OnDestroy () => SingletonObject = null;

		/// <summary>�L����</summary>
		private void OnEnable () {
			OAuthSettingsIsOpen = EditorPrefs.GetBool (OAuthSettingsIsOpenPrefKey, false);
			BuildNumberSettingsIsOpen = EditorPrefs.GetBool (BuildNumberSettingsIsOpenPrefKey, false);
			MainController.LoadPrefs ();
		}

		/// <summary>������</summary>
		private void OnDisable () {
			EditorPrefs.SetBool (OAuthSettingsIsOpenPrefKey, OAuthSettingsIsOpen);
			EditorPrefs.SetBool (BuildNumberSettingsIsOpenPrefKey, BuildNumberSettingsIsOpen);
			MainController.SavePrefs ();
		}

		/// <summary>�\��</summary>
		private void OnGUI () {
			EditorGUILayout.LabelField ("Project Settings", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal ();
			GUILayout.Space (10f);
			EditorGUILayout.BeginVertical ();
			// document
			MainController.Document = EditorGUILayout.TextField (new GUIContent ("Document ID", "the ID of Google Spreadsheet"), MainController.Document);
			// asset path
			MainController.AssetPath = EditorGUILayout.TextField (new GUIContent ("Asset Folder", "the path of assets to create"), MainController.AssetPath);
			EditorGUILayout.EndVertical ();
			EditorGUILayout.EndHorizontal ();

			// get button
			GUILayout.Space (20f);
			EditorGUILayout.BeginHorizontal ();
			GUILayout.Space (10f);
			EditorGUI.BeginDisabledGroup (EditorApplication.isCompiling || MainController.IsTranslating || string.IsNullOrEmpty (MainController.Application) || string.IsNullOrEmpty (MainController.Document) || string.IsNullOrEmpty (MainController.AssetPath));
			if (GUILayout.Button ("GetSharedData", GUILayout.Height (30))) { MainController.GetData (); }
			EditorGUI.EndDisabledGroup ();
			GUILayout.Space (40f);
			// abort button
			EditorGUI.BeginDisabledGroup (EditorApplication.isCompiling || !MainController.IsTranslating || MainController.IsCancellationRequested);
			if (GUILayout.Button ("Abort", GUILayout.Height (30), GUILayout.Width (100))) { MainController.Abort (); }
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
				MainController.UnifiedNumber = EditorGUILayout.Toggle (new GUIContent ("Unified", "give all targets the maximum build number"), MainController.UnifiedNumber);
				MainController.AutoIncrement = EditorGUILayout.Toggle (new GUIContent ("Auto Increment", "automatically increment build number after build"), MainController.AutoIncrement);
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
				MainController.Application = EditorGUILayout.TextField (new GUIContent ("Application URL", "the URL of Google Apps Script"), MainController.Application);
				// client id
				MainController.ClientId = EditorGUILayout.TextField (new GUIContent ("Client ID", "the ID for Google Apps Script"), MainController.ClientId);
				// client secret
				MainController.ClientSecret = EditorGUILayout.TextField (new GUIContent ("Client Secret", "the secret for Google Apps Script"), MainController.ClientSecret);
				// tokens
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.BeginVertical ();
				// access token
				EditorGUI.BeginDisabledGroup (true);
				EditorGUILayout.TextField (new GUIContent ("Access Token"), MainController.AccessToken);
				EditorGUI.EndDisabledGroup ();
				// refresh token
				EditorGUI.BeginDisabledGroup (true);
				EditorGUILayout.TextField (new GUIContent ("Refresh Token"), MainController.RefreshToken);
				EditorGUI.EndDisabledGroup ();
				EditorGUILayout.EndVertical ();
				EditorGUI.BeginDisabledGroup ((string.IsNullOrEmpty (MainController.AccessToken) && string.IsNullOrEmpty (MainController.RefreshToken)) || EditorApplication.isCompiling || MainController.IsTranslating);
				if (GUILayout.Button ("Clear", GUILayout.Height (30), GUILayout.Width (60))) { MainController.ClearOAuth (); }
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

}

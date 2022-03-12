using System;
using UnityEngine;
using UnityEngine.UI;
using SharedConstant;

public class SwitchPanel : MonoBehaviour {

	[SerializeField] private Text TextPanel = default;
	[SerializeField] private ToggleGroup togleGroup = default;

	private bool inited;
	private void Init () {
		if (!inited) {
			foreach (var toggle in togleGroup.GetComponentsInChildren<Toggle> ()) {
				if (Enum.TryParse<SystemLanguage> (toggle.name, out var language) && language == Txt.Locale) {
					toggle.isOn = true;
					Debug.Log ($"Turn On Toggle '{toggle.name}'");
				}
			}
			inited = true;
		}
	}

	public void OnChange (Toggle toggle) {
		if (TextPanel != default) {
			if (Enum.TryParse<SystemLanguage> (toggle.name, out var language)) {
				if (toggle.isOn) { Txt.Locale = language; }
				Debug.Log ($"OnChange '{toggle.name}' {toggle.isOn}");
			}
		}
	}

	private SystemLanguage lastLocale = SystemLanguage.Unknown;
	private void Update () {
		if (TextPanel != default && lastLocale != Txt.Locale && Txt.Locale != SystemLanguage.Unknown) {
			Debug.Log ($"Change {lastLocale} => {Txt.Locale} {Txt.S (Nam.Language)}");
			Init ();
			TextPanel.text = $"{Txt.S (Nam.Welcome)}\nTest = {Cns.Test}\n\nLanguage: {Txt.S (Nam.Language)}\n\nVersion: {Cns.BundleVersion} ({Cns.BuildNumber})";
			lastLocale = Txt.Locale;
		}
	}


}

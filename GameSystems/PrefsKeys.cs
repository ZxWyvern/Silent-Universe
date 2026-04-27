using UnityEngine;

/// <summary>
/// PrefsKeys — konstanta kunci untuk PlayerPrefs.
///
/// DEPRECATED: Sensitivity dan KeyBindings kini disimpan ke SaveFile (JSON),
/// bukan PlayerPrefs. Konstanta ini dipertahankan hanya untuk migrasi data lama.
/// Gunakan SaveFile.Data.sensitivity dan SaveFile.Data.keyBindings.
/// </summary>
public static class PrefsKeys
{
    [System.Obsolete("Gunakan SaveFile.Data.sensitivity. Field ini hanya untuk migrasi dari PlayerPrefs lama.")]
    public const string SENSITIVITY_KEY  = "MouseSensitivity";

    [System.Obsolete("Gunakan SaveFile.Data.keyBindings. Field ini hanya untuk migrasi dari PlayerPrefs lama.")]
    public const string KeyBINDINGS_KEY  = "KeyBindings";
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Updates the existing MainMenu credits text through Unity's scene API.
/// The Ending credit copy is runtime-owned by EndingScreenController.
/// </summary>
public static class PatchBreakCreditsSetup
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string MenuFontAssetPath =
        "Assets/Resources/NotoSansKRMenu SDF.asset";
    private const string MenuSourceFontPath =
        "Assets/Resources/NotoSansCJKkr-Regular.otf";
    private const string CreditObjectName = "CreditsText";
    private const string CreditCopy =
        "CODE / DEVELOPMENT\n" +
        "정우빈\n\n" +
        "ASSET DESIGN\n" +
        "김윤지";

    [MenuItem("Tools/PATCH BREAK/Credits/Setup MainMenu")]
    public static void SetupMainMenu()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        Font menuSourceFont = LoadValidatedMenuSourceFont();
        TMP_FontAsset menuFont = LoadValidatedMenuFont();

        Scene scene = EditorSceneManager.OpenScene(
            MainMenuScenePath,
            OpenSceneMode.Single
        );
        TextMeshProUGUI creditsText = FindCreditsTextOrThrow(scene);

        creditsText.text = CreditCopy;
        creditsText.font = menuFont;
        creditsText.fontSharedMaterial = menuFont.material;
        creditsText.SetAllDirty();

        string requiredMenuText = CollectRequiredMenuFontText(scene);
        EnsureMenuFontSource(menuFont, menuSourceFont);
        RebuildMenuGlyphs(menuFont, menuSourceFont, requiredMenuText);

        EditorUtility.SetDirty(creditsText);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        ValidateMainMenuScene(scene, menuFont, menuSourceFont);
        Debug.Log("PATCH//BREAK credits: MainMenu setup complete.");
    }

    [MenuItem("Tools/PATCH BREAK/Credits/Validate MainMenu")]
    public static void ValidateMainMenu()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        Font menuSourceFont = LoadValidatedMenuSourceFont();
        TMP_FontAsset menuFont = LoadValidatedMenuFont();
        ValidateMenuFontSource(menuFont, menuSourceFont);
        Scene scene = EditorSceneManager.OpenScene(
            MainMenuScenePath,
            OpenSceneMode.Single
        );
        ValidateMainMenuScene(scene, menuFont, menuSourceFont);
        Debug.Log("PATCH//BREAK credits: MainMenu validation complete.");
    }

    private static void ValidateMainMenuScene(
        Scene scene,
        TMP_FontAsset menuFont,
        Font menuSourceFont
    )
    {
        TextMeshProUGUI creditsText = FindCreditsTextOrThrow(scene);
        ValidateCreditsText(creditsText, menuFont);
        ValidateMainMenuRuntimeFontTarget(scene, creditsText, menuFont);
        ValidateGlyphCoverage(
            menuFont,
            CollectRequiredMenuFontText(scene)
        );
        ValidateGlyphGenerationSource(
            menuFont,
            menuSourceFont,
            CollectRequiredMenuFontText(scene)
        );
        CollectMissingComponentErrors(scene);
    }

    private static bool PrepareEditorOperation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "PATCH//BREAK credits setup cannot run while Play Mode " +
                "is active or changing."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static TMP_FontAsset LoadValidatedMenuFont()
    {
        TMP_FontAsset menuFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            MenuFontAssetPath
        );

        if (menuFont == null)
        {
            throw new InvalidOperationException(
                $"Could not load MainMenu Korean font at {MenuFontAssetPath}."
            );
        }

        if (menuFont.atlasPopulationMode != AtlasPopulationMode.Dynamic)
        {
            throw new InvalidOperationException(
                "NotoSansKRMenu SDF must remain a Dynamic TMP Font Asset."
            );
        }

        return menuFont;
    }

    private static Font LoadValidatedMenuSourceFont()
    {
        Font menuSourceFont = AssetDatabase.LoadAssetAtPath<Font>(
            MenuSourceFontPath
        );
        if (menuSourceFont == null)
        {
            throw new InvalidOperationException(
                $"Could not load MainMenu Korean source font at " +
                $"{MenuSourceFontPath}."
            );
        }

        List<char> unsupportedGlyphs = GetRequiredCreditGlyphs().FindAll(
            character => !menuSourceFont.HasCharacter(character)
        );
        if (unsupportedGlyphs.Count > 0)
        {
            throw new InvalidOperationException(
                "NotoSansCJKkr-Regular.otf is missing Credits glyph(s): " +
                new string(unsupportedGlyphs.ToArray())
            );
        }

        return menuSourceFont;
    }

    private static void EnsureMenuFontSource(
        TMP_FontAsset menuFont,
        Font menuSourceFont
    )
    {
        if (menuFont.sourceFontFile == menuSourceFont)
        {
            return;
        }

        SerializedObject serializedFont = new SerializedObject(menuFont);
        SerializedProperty sourceFontProperty = serializedFont.FindProperty(
            "m_SourceFontFile"
        );
        SerializedProperty sourceFontGuidProperty = serializedFont.FindProperty(
            "m_SourceFontFileGUID"
        );
        if (sourceFontProperty == null || sourceFontGuidProperty == null)
        {
            throw new InvalidOperationException(
                "NotoSansKRMenu SDF source font serialization is missing."
            );
        }

        Undo.RecordObject(menuFont, "Assign MainMenu Korean source font");
        sourceFontProperty.objectReferenceValue = menuSourceFont;
        sourceFontGuidProperty.stringValue = AssetDatabase.AssetPathToGUID(
            MenuSourceFontPath
        );
        serializedFont.ApplyModifiedProperties();
        EditorUtility.SetDirty(menuFont);
        AssetDatabase.SaveAssetIfDirty(menuFont);

        ValidateMenuFontSource(menuFont, menuSourceFont);
    }

    private static void ValidateMenuFontSource(
        TMP_FontAsset menuFont,
        Font menuSourceFont
    )
    {
        if (menuFont.sourceFontFile != menuSourceFont)
        {
            throw new InvalidOperationException(
                "NotoSansKRMenu SDF does not reference " +
                "NotoSansCJKkr-Regular.otf."
            );
        }
    }

    private static void RebuildMenuGlyphs(
        TMP_FontAsset menuFont,
        Font menuSourceFont,
        string requiredMenuText
    )
    {
        if (string.IsNullOrEmpty(requiredMenuText))
        {
            throw new InvalidOperationException(
                "MainMenu/Prologue glyph collection produced no characters."
            );
        }

        // The current source font is the full Korean OTF. Clear the Dynamic
        // atlas before repopulating so no glyph baked from the old subset
        // font can remain alongside a glyph baked from the full source.
        Undo.RecordObject(menuFont, "Rebuild MainMenu Korean glyph atlas");
        menuFont.ClearFontAssetData(true);
        menuFont.TryAddCharacters(requiredMenuText, out string missingCharacters);

        if (!string.IsNullOrEmpty(missingCharacters))
        {
            throw new InvalidOperationException(
                "Failed to populate MainMenu/Prologue glyph(s) in " +
                "NotoSansKRMenu SDF: " + missingCharacters
            );
        }

        ValidateGlyphCoverage(menuFont, requiredMenuText);
        ValidateGlyphGenerationSource(
            menuFont,
            menuSourceFont,
            requiredMenuText
        );
        EditorUtility.SetDirty(menuFont);
        AssetDatabase.SaveAssetIfDirty(menuFont);
    }

    private static void ValidateGlyphCoverage(
        TMP_FontAsset menuFont,
        string requiredMenuText
    )
    {
        List<char> missingFromFont = GetDistinctGlyphCharacters(
            requiredMenuText
        ).FindAll(character =>
            !menuFont.HasCharacter(character, false, false)
        );

        if (missingFromFont.Count > 0)
        {
            throw new InvalidOperationException(
                "MainMenu/Prologue glyph validation failed. Font atlas " +
                "missing='" +
                new string(missingFromFont.ToArray()) +
                "'. Run Credits Setup MainMenu to rebuild the Dynamic " +
                "NotoSansKRMenu SDF atlas."
            );
        }
    }

    private static void ValidateGlyphGenerationSource(
        TMP_FontAsset menuFont,
        Font menuSourceFont,
        string requiredMenuText
    )
    {
        FontEngineError initializeError = FontEngine.InitializeFontEngine();
        if (initializeError != FontEngineError.Success)
        {
            throw new InvalidOperationException(
                "Could not initialize the TextCore FontEngine for glyph " +
                "generation validation: " + initializeError + "."
            );
        }

        int pointSize = Mathf.RoundToInt(menuFont.faceInfo.pointSize);
        FontEngineError loadError = FontEngine.LoadFontFace(
            menuSourceFont,
            pointSize
        );
        if (loadError != FontEngineError.Success)
        {
            throw new InvalidOperationException(
                "Could not load NotoSansCJKkr-Regular.otf for glyph " +
                "generation validation: " + loadError + "."
            );
        }

        List<char> mismatchedCharacters = new List<char>();
        List<char> unavailableSourceCharacters = new List<char>();
        foreach (char character in GetDistinctGlyphCharacters(requiredMenuText))
        {
            uint unicode = character;
            if (!FontEngine.TryGetGlyphIndex(
                    unicode,
                    out uint expectedGlyphIndex
                ))
            {
                unavailableSourceCharacters.Add(character);
                continue;
            }

            if (!menuFont.characterLookupTable.TryGetValue(
                    unicode,
                    out TMP_Character currentCharacter
                ) || currentCharacter.glyphIndex != expectedGlyphIndex)
            {
                mismatchedCharacters.Add(character);
            }
        }

        if (unavailableSourceCharacters.Count > 0)
        {
            throw new InvalidOperationException(
                "NotoSansCJKkr-Regular.otf does not contain the required " +
                "MainMenu/Prologue glyph(s): " +
                new string(unavailableSourceCharacters.ToArray()) + "."
            );
        }

        if (mismatchedCharacters.Count > 0)
        {
            throw new InvalidOperationException(
                "NotoSansKRMenu SDF contains glyphs not generated from " +
                "NotoSansCJKkr-Regular.otf: " +
                new string(mismatchedCharacters.ToArray()) +
                ". Run Credits Setup MainMenu to rebuild the Dynamic atlas."
            );
        }
    }

    private static List<char> GetRequiredCreditGlyphs()
    {
        return GetDistinctGlyphCharacters(CreditCopy).FindAll(character =>
            character >= '\uAC00' && character <= '\uD7A3'
        );
    }

    private static void ValidateCreditsText(
        TextMeshProUGUI creditsText,
        TMP_FontAsset menuFont
    )
    {
        if (creditsText.text != CreditCopy)
        {
            throw new InvalidOperationException(
                "MainMenu CreditsText has an unexpected value."
            );
        }

        if (creditsText.font != menuFont ||
            creditsText.fontSharedMaterial != menuFont.material)
        {
            throw new InvalidOperationException(
                "MainMenu CreditsText must use NotoSansKRMenu SDF and " +
                "its shared material."
            );
        }
    }

    private static void ValidateMainMenuRuntimeFontTarget(
        Scene scene,
        TextMeshProUGUI creditsText,
        TMP_FontAsset menuFont
    )
    {
        MainMenuController controller = FindMainMenuControllerOrThrow(scene);

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty fontProperty = serializedController.FindProperty(
            "koreanFontAsset"
        );
        SerializedProperty targetsProperty = serializedController.FindProperty(
            "koreanTextTargets"
        );

        if (fontProperty == null ||
            fontProperty.objectReferenceValue != menuFont)
        {
            throw new InvalidOperationException(
                "MainMenuController does not reference NotoSansKRMenu SDF."
            );
        }

        bool creditsIsRuntimeKoreanTarget = false;
        if (targetsProperty != null && targetsProperty.isArray)
        {
            for (int index = 0; index < targetsProperty.arraySize; index++)
            {
                if (targetsProperty
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue == creditsText)
                {
                    creditsIsRuntimeKoreanTarget = true;
                    break;
                }
            }
        }

        if (!creditsIsRuntimeKoreanTarget)
        {
            throw new InvalidOperationException(
                "CreditsText is missing from " +
                "MainMenuController.koreanTextTargets."
            );
        }
    }

    private static string CollectRequiredMenuFontText(Scene scene)
    {
        MainMenuController controller = FindMainMenuControllerOrThrow(scene);
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty targetsProperty = serializedController.FindProperty(
            "koreanTextTargets"
        );
        SerializedProperty pagesProperty = serializedController.FindProperty(
            "prologuePages"
        );
        if (targetsProperty == null || !targetsProperty.isArray ||
            pagesProperty == null || !pagesProperty.isArray)
        {
            throw new InvalidOperationException(
                "MainMenu Korean text serialization is missing."
            );
        }

        HashSet<char> characters = new HashSet<char>();
        AddGlyphCharacters(CreditCopy, characters);

        for (int index = 0; index < targetsProperty.arraySize; index++)
        {
            TMP_Text target = targetsProperty
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as TMP_Text;
            if (target == null)
            {
                throw new InvalidOperationException(
                    "MainMenuController has a missing Korean text target."
                );
            }

            AddGlyphCharacters(target.text, characters);
        }

        for (int index = 0; index < pagesProperty.arraySize; index++)
        {
            SerializedProperty page = pagesProperty.GetArrayElementAtIndex(index);
            SerializedProperty bodyProperty = page.FindPropertyRelative("body");
            if (bodyProperty == null)
            {
                throw new InvalidOperationException(
                    $"Prologue page {index + 1} has no body property."
                );
            }

            AddGlyphCharacters(bodyProperty.stringValue, characters);
        }

        List<char> orderedCharacters = new List<char>(characters);
        orderedCharacters.Sort();
        return new string(orderedCharacters.ToArray());
    }

    private static List<char> GetDistinctGlyphCharacters(string text)
    {
        HashSet<char> characters = new HashSet<char>();
        AddGlyphCharacters(text, characters);
        List<char> result = new List<char>(characters);
        result.Sort();
        return result;
    }

    private static void AddGlyphCharacters(
        string text,
        HashSet<char> characters
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (char character in text)
        {
            if (!char.IsControl(character))
            {
                characters.Add(character);
            }
        }
    }

    private static MainMenuController FindMainMenuControllerOrThrow(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MainMenuController controller = root.GetComponentInChildren<
                MainMenuController
            >(true);
            if (controller != null)
            {
                return controller;
            }
        }

        throw new InvalidOperationException("MainMenuController is missing.");
    }

    private static TextMeshProUGUI FindCreditsTextOrThrow(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TextMeshProUGUI text in
                     root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.gameObject.name == CreditObjectName)
                {
                    return text;
                }
            }
        }

        throw new InvalidOperationException(
            "MainMenu CreditsText TMP component is missing."
        );
    }

    private static void CollectMissingComponentErrors(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    transform.gameObject
                ) > 0)
                {
                    throw new InvalidOperationException(
                        $"{transform.name} has a missing MonoBehaviour."
                    );
                }
            }
        }
    }
}

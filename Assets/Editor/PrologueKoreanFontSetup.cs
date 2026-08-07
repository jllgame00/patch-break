using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrologueKoreanFontSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Prologue Korean Font/";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string MenuFontAssetPath =
        "Assets/Resources/NotoSansKRMenu SDF.asset";

    [MenuItem(MenuRoot + "Setup MainMenu")]
    public static void SetupMainMenu()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        TMP_FontAsset menuFont = LoadValidatedMenuFont();
        Scene scene = OpenMainMenuScene();
        MainMenuController controller = FindSingleController(scene);
        ValidatePrologueGlyphCoverage(controller, menuFont);

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty fontProperty = serializedController.FindProperty(
            "koreanFontAsset"
        );

        if (fontProperty == null)
        {
            throw new InvalidOperationException(
                "MainMenuController.koreanFontAsset was not found."
            );
        }

        if (fontProperty.objectReferenceValue != menuFont)
        {
            Undo.RecordObject(controller, "Assign Prologue Korean font");
            fontProperty.objectReferenceValue = menuFont;
            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        EditorSceneManager.CloseScene(scene, true);

        Scene reopenedScene = OpenMainMenuScene();
        ValidateMainMenuFontSetup(reopenedScene, menuFont);
        EditorSceneManager.CloseScene(reopenedScene, true);

        Debug.Log(
            "Prologue Korean font setup completed. " +
            "MainMenuController now uses NotoSansKRMenu SDF."
        );
    }

    [MenuItem(MenuRoot + "Validate MainMenu")]
    public static void ValidateMainMenu()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        TMP_FontAsset menuFont = LoadValidatedMenuFont();
        Scene scene = OpenMainMenuScene();
        ValidateMainMenuFontSetup(scene, menuFont);
        EditorSceneManager.CloseScene(scene, true);

        Debug.Log("Prologue Korean font validation completed.");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "Prologue Korean font setup cannot run while Play Mode is active."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static Scene OpenMainMenuScene()
    {
        return EditorSceneManager.OpenScene(
            MainMenuScenePath,
            OpenSceneMode.Single
        );
    }

    private static TMP_FontAsset LoadValidatedMenuFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            MenuFontAssetPath
        );

        if (font == null)
        {
            throw new InvalidOperationException(
                $"Could not load {MenuFontAssetPath}."
            );
        }

        if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
            font.sourceFontFile == null)
        {
            throw new InvalidOperationException(
                "NotoSansKRMenu SDF must remain a Dynamic TMP Font Asset " +
                "with its source font file assigned."
            );
        }

        return font;
    }

    private static MainMenuController FindSingleController(Scene scene)
    {
        MainMenuController[] controllers = scene
            .GetRootGameObjects()
            .SelectMany(root =>
                root.GetComponentsInChildren<MainMenuController>(true)
            )
            .ToArray();

        if (controllers.Length != 1)
        {
            throw new InvalidOperationException(
                $"MainMenu: expected one MainMenuController, found " +
                $"{controllers.Length}."
            );
        }

        return controllers[0];
    }

    private static void ValidateMainMenuFontSetup(
        Scene scene,
        TMP_FontAsset expectedFont)
    {
        MainMenuController controller = FindSingleController(scene);
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty fontProperty = serializedController.FindProperty(
            "koreanFontAsset"
        );

        if (fontProperty == null ||
            fontProperty.objectReferenceValue != expectedFont)
        {
            throw new InvalidOperationException(
                "MainMenuController does not reference NotoSansKRMenu SDF."
            );
        }

        ValidatePrologueGlyphCoverage(controller, expectedFont);
        ValidateKoreanTargetReferences(serializedController);
        CollectMissingComponentErrors(scene);
    }

    private static void ValidatePrologueGlyphCoverage(
        MainMenuController controller,
        TMP_FontAsset font)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty pages = serializedController.FindProperty(
            "prologuePages"
        );

        if (pages == null || !pages.isArray || pages.arraySize == 0)
        {
            throw new InvalidOperationException(
                "MainMenuController has no serialized prologue pages."
            );
        }

        HashSet<char> missingCharacters = new HashSet<char>();

        for (int index = 0; index < pages.arraySize; index++)
        {
            SerializedProperty page = pages.GetArrayElementAtIndex(index);
            SerializedProperty body = page.FindPropertyRelative("body");

            if (body == null)
            {
                throw new InvalidOperationException(
                    $"Prologue page {index + 1} has no body property."
                );
            }

            foreach (char character in body.stringValue)
            {
                if (IsHangulSyllable(character) &&
                    !font.sourceFontFile.HasCharacter(character))
                {
                    missingCharacters.Add(character);
                }
            }
        }

        if (missingCharacters.Count > 0)
        {
            throw new InvalidOperationException(
                "NotoSansKRMenu.ttf is missing Prologue glyph(s): " +
                string.Join(
                    ", ",
                    missingCharacters.OrderBy(character => character)
                )
            );
        }
    }

    private static void ValidateKoreanTargetReferences(
        SerializedObject serializedController)
    {
        SerializedProperty targets = serializedController.FindProperty(
            "koreanTextTargets"
        );

        if (targets == null || !targets.isArray || targets.arraySize == 0)
        {
            throw new InvalidOperationException(
                "MainMenuController has no Korean text targets."
            );
        }

        bool hasPrologueBody = false;

        for (int index = 0; index < targets.arraySize; index++)
        {
            TMP_Text target = targets
                .GetArrayElementAtIndex(index)
                .objectReferenceValue as TMP_Text;

            if (target == null)
            {
                throw new InvalidOperationException(
                    "MainMenuController has a missing Korean text target."
                );
            }

            if (target.name == "PrologueBody")
            {
                hasPrologueBody = true;
            }
        }

        if (!hasPrologueBody)
        {
            throw new InvalidOperationException(
                "PrologueBody is missing from koreanTextTargets."
            );
        }
    }

    private static bool IsHangulSyllable(char character)
    {
        return character >= '\uAC00' && character <= '\uD7A3';
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

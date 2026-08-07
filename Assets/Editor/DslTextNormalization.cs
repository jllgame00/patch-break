using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DslTextNormalization
{
    private const string MenuRoot = "Tools/PATCH BREAK/Normalize DSL Text/";

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec("Assets/Scenes/Battle.unity", "Battle"),
        new SceneSpec("Assets/Scenes/KnightBattle.unity", "KnightBattle"),
        new SceneSpec("Assets/Scenes/DebuggerBattle.unity", "DebuggerBattle"),
        new SceneSpec("Assets/Scenes/MainMenu.unity", "MainMenu")
    };

    [MenuItem(MenuRoot + "Apply to Player-Facing Scenes")]
    public static void ApplyToPlayerFacingScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        int totalChanges = 0;

        foreach (SceneSpec spec in SceneSpecs)
        {
            Scene scene = OpenScene(spec);
            int changes = NormalizeSceneText(scene);
            ValidateSceneText(scene, spec);

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            EditorSceneManager.CloseScene(scene, true);

            Scene reopenedScene = OpenScene(spec);
            ValidateSceneText(reopenedScene, spec);
            EditorSceneManager.CloseScene(reopenedScene, true);

            totalChanges += changes;
        }

        Debug.Log(
            $"DSL text normalization completed. Updated {totalChanges} " +
            "serialized player-facing text field(s)."
        );
    }

    [MenuItem(MenuRoot + "Validate Player-Facing Scenes")]
    public static void ValidatePlayerFacingScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        foreach (SceneSpec spec in SceneSpecs)
        {
            Scene scene = OpenScene(spec);
            ValidateSceneText(scene, spec);
            EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log("DSL text validation completed.");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "DSL text normalization cannot run while Play Mode is active."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static Scene OpenScene(SceneSpec spec)
    {
        return EditorSceneManager.OpenScene(spec.Path, OpenSceneMode.Single);
    }

    private static int NormalizeSceneText(Scene scene)
    {
        int changes = 0;

        foreach (RuntimeConsoleUI runtimeConsole in
            FindComponents<RuntimeConsoleUI>(scene))
        {
            changes += NormalizeSerializedString(
                runtimeConsole,
                "defaultProgram"
            );
        }

        foreach (BattleBriefingController briefing in
            FindComponents<BattleBriefingController>(scene))
        {
            changes += NormalizeSerializedString(briefing, "descriptionCopy");
            changes += NormalizeSerializedString(briefing, "rulesCopy");
            changes += NormalizeSerializedString(briefing, "controlsCopy");
        }

        foreach (TMP_Text text in FindComponents<TMP_Text>(scene))
        {
            changes += NormalizeSerializedString(text, "m_text");
        }

        return changes;
    }

    private static int NormalizeSerializedString(
        UnityEngine.Object target,
        string propertyName)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || property.propertyType != SerializedPropertyType.String)
        {
            return 0;
        }

        string normalized = NormalizeDslExamples(property.stringValue);

        if (string.Equals(normalized, property.stringValue, StringComparison.Ordinal))
        {
            return 0;
        }

        Undo.RecordObject(target, "Normalize DSL text");
        property.stringValue = normalized;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return 1;
    }

    private static string NormalizeDslExamples(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        string[] lines = source.Replace("\r\n", "\n").Split('\n');
        List<string> normalizedLines = new List<string>(lines.Length);

        for (int index = 0; index < lines.Length; index++)
        {
            string current = lines[index];
            string currentTrimmed = current.Trim();

            if (IsIfKeywordOnly(currentTrimmed) && index + 1 < lines.Length)
            {
                string nextTrimmed = lines[index + 1].Trim();
                if (TryCanonicalizeDslLine(
                    $"if {nextTrimmed}",
                    out string canonical))
                {
                    normalizedLines.Add(GetLeadingWhitespace(current) + canonical);
                    index++;
                    continue;
                }
            }

            if (TryGetConditionWithoutAction(currentTrimmed, out string condition) &&
                index + 1 < lines.Length)
            {
                string nextTrimmed = lines[index + 1].Trim();
                if (nextTrimmed.StartsWith("=>", StringComparison.Ordinal) &&
                    TryCanonicalizeDslLine(
                        $"if {condition} {nextTrimmed}",
                        out string canonical))
                {
                    normalizedLines.Add(GetLeadingWhitespace(current) + canonical);
                    index++;
                    continue;
                }
            }

            if (TryCanonicalizeDslLine(currentTrimmed, out string normalizedLine))
            {
                normalizedLines.Add(GetLeadingWhitespace(current) + normalizedLine);
                continue;
            }

            normalizedLines.Add(current);
        }

        return string.Join("\n", normalizedLines);
    }

    private static bool IsIfKeywordOnly(string value)
    {
        return string.Equals(value, "if", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetConditionWithoutAction(
        string value,
        out string condition)
    {
        condition = string.Empty;

        if (!value.StartsWith("if ", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("=>") ||
            value.IndexOf(" then ", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        string candidate = value.Substring(3).Trim();
        if (!IsSupportedCondition(candidate))
        {
            return false;
        }

        condition = candidate;
        return true;
    }

    private static bool TryCanonicalizeDslLine(
        string value,
        out string canonical)
    {
        canonical = string.Empty;

        if (!value.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string condition;
        string action;
        int arrowIndex = value.IndexOf("=>", StringComparison.Ordinal);

        if (arrowIndex >= 0)
        {
            if (value.IndexOf("=>", arrowIndex + 2, StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            condition = value.Substring(3, arrowIndex - 3).Trim();
            action = value.Substring(arrowIndex + 2).Trim();
        }
        else
        {
            const string legacySeparator = " then ";
            int thenIndex = value.IndexOf(
                legacySeparator,
                StringComparison.OrdinalIgnoreCase
            );

            if (thenIndex < 0)
            {
                return false;
            }

            condition = value.Substring(3, thenIndex - 3).Trim();
            action = value.Substring(
                thenIndex + legacySeparator.Length
            ).Trim();
        }

        action = StripLegacyActionPunctuation(action);

        if (!IsSupportedCondition(condition) || !IsSupportedAction(action))
        {
            return false;
        }

        canonical = $"if {condition.ToLowerInvariant()} => " +
            action.ToLowerInvariant();
        return true;
    }

    private static string StripLegacyActionPunctuation(string action)
    {
        string normalized = action.Trim();

        if (normalized.EndsWith(";", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 1).Trim();
        }

        if (normalized.EndsWith("()", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - 2).Trim();
        }

        return normalized;
    }

    private static bool IsSupportedCondition(string value)
    {
        return string.Equals(value, "enemy.near", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "enemy.far", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   value,
                   "enemy.attacking",
                   StringComparison.OrdinalIgnoreCase
               ) ||
               string.Equals(
                   value,
                   "enemy.guarding",
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private static bool IsSupportedAction(string value)
    {
        return string.Equals(value, "slash", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "approach", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   value,
                   "dash.back",
                   StringComparison.OrdinalIgnoreCase
               ) ||
               string.Equals(
                   value,
                   "dash.forward",
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private static string GetLeadingWhitespace(string value)
    {
        int index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return value.Substring(0, index);
    }

    private static void ValidateSceneText(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();

        foreach (RuntimeConsoleUI runtimeConsole in
            FindComponents<RuntimeConsoleUI>(scene))
        {
            ValidateSerializedString(
                runtimeConsole,
                "defaultProgram",
                errors
            );
        }

        foreach (BattleBriefingController briefing in
            FindComponents<BattleBriefingController>(scene))
        {
            ValidateSerializedString(briefing, "descriptionCopy", errors);
            ValidateSerializedString(briefing, "rulesCopy", errors);
            ValidateSerializedString(briefing, "controlsCopy", errors);
        }

        foreach (TMP_Text text in FindComponents<TMP_Text>(scene))
        {
            ValidateSerializedString(text, "m_text", errors);
        }

        CollectMissingComponentErrors(scene, errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: DSL text validation failed.\n" +
                string.Join("\n", errors)
            );
        }
    }

    private static void ValidateSerializedString(
        UnityEngine.Object target,
        string propertyName,
        ICollection<string> errors)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || property.propertyType != SerializedPropertyType.String)
        {
            return;
        }

        if (ContainsLegacyOrSplitDslSyntax(property.stringValue))
        {
            errors.Add(
                $"{target.name}.{propertyName} contains non-canonical DSL text."
            );
        }
    }

    private static bool ContainsLegacyOrSplitDslSyntax(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        string[] lines = source.Replace("\r\n", "\n").Split('\n');

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            string lowerCaseLine = line.ToLowerInvariant();

            if (lowerCaseLine.Contains(" then ") &&
                lowerCaseLine.Contains("if enemy."))
            {
                return true;
            }

            if (ContainsLegacyActionPunctuation(lowerCaseLine))
            {
                return true;
            }

            if (IsIfKeywordOnly(line) && index + 1 < lines.Length &&
                lines[index + 1].TrimStart().StartsWith(
                    "enemy.",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return true;
            }

            if (TryGetConditionWithoutAction(line, out _) &&
                index + 1 < lines.Length &&
                lines[index + 1].TrimStart().StartsWith(
                    "=>",
                    StringComparison.Ordinal
                ))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLegacyActionPunctuation(string value)
    {
        return value.Contains("slash()") ||
               value.Contains("approach()") ||
               value.Contains("dash.back()") ||
               value.Contains("dash.forward()") ||
               value.Contains("slash();") ||
               value.Contains("approach();") ||
               value.Contains("dash.back();") ||
               value.Contains("dash.forward();");
    }

    private static void CollectMissingComponentErrors(
        Scene scene,
        ICollection<string> errors)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    transform.gameObject
                ) > 0)
                {
                    errors.Add(
                        $"{transform.name} has a missing MonoBehaviour."
                    );
                }
            }
        }
    }

    private static IEnumerable<T> FindComponents<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(
            root => root.GetComponentsInChildren<T>(true)
        );
    }

    private sealed class SceneSpec
    {
        public string Path { get; }
        public string Name { get; }

        public SceneSpec(string path, string name)
        {
            Path = path;
            Name = name;
        }
    }
}

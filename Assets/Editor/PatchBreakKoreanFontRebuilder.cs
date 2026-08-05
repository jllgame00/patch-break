#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class PatchBreakKoreanFontRebuilder
{
    // This is intentionally only a preferred candidate. A Font.HasCharacter
    // result is not sufficient evidence that TextMesh Pro can rasterize a
    // glyph, so the actual source is selected only after a temporary TMP
    // font-asset validation has passed for the complete mandatory set.
    private const string PreferredSourceFontPath =
        "Assets/Resources/NotoSansKRFull.otf";
    private const string FontAssetPath =
        "Assets/Resources/NotoSansKRGame SDF.asset";
    private const string TempFontAssetPath =
        "Assets/Resources/NotoSansKRGame Temp SDF.asset";
    private const string BackupFontAssetPath =
        "Assets/Resources/NotoSansKRGame Backup SDF.asset";
    private const string FontValidationDirectory =
        "Assets/Editor/Generated/FontValidation";
    private const string FullKoreanSourceFontRequired =
        "FULL_KOREAN_SOURCE_FONT_REQUIRED";
    private const string CharacterSetPath =
        "Assets/Editor/Generated/PatchBreakKoreanCharacterSet.txt";
    private const string LiberationFallbackPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/" +
        "LiberationSans SDF.asset";
    private const int AtlasSize = 4096;
    private const int AtlasPadding = 9;
    private const int CharacterBatchSize = 192;

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Battle.unity",
        "Assets/Scenes/KnightBattle.unity",
        "Assets/Scenes/DebuggerBattle.unity",
        "Assets/Scenes/Ending.unity"
    };

    private static readonly string[] RuntimeDisplayScriptPaths =
    {
        "Assets/Scripts/UI/MainMenuController.cs",
        "Assets/Scripts/UI/BattleBriefingController.cs",
        "Assets/Scripts/UI/RuntimeConsoleUI.cs",
        "Assets/Scripts/UI/LivePatchUI.cs",
        "Assets/Scripts/UI/EndingScreenController.cs",
        "Assets/Scripts/battle/BattleManager.cs"
    };

    [MenuItem("Tools/PATCH//BREAK/Rebuild Korean TMP Font")]
    public static void RebuildFromMenu()
    {
        try
        {
            Rebuild();
            Debug.Log("PATCH//BREAK Korean TMP font rebuild completed.");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "PATCH//BREAK Korean TMP font rebuild failed.\n" +
                exception
            );
            throw;
        }
    }

    public static void RebuildFromCommandLine()
    {
        try
        {
            Rebuild();
            Debug.Log("PATCH//BREAK Korean TMP font rebuild completed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "PATCH//BREAK Korean TMP font rebuild failed.\n" +
                exception
            );
            EditorApplication.Exit(1);
        }
    }

    private static void Rebuild()
    {
        CharacterCollection collection = CollectDisplayCharacters();
        WriteCharacterSetFile(collection);

        List<FontCandidateValidation> candidateResults =
            ValidateProjectFontCandidates(collection.MandatoryKorean);
        FontCandidateValidation selectedCandidate = candidateResults
            .FirstOrDefault(candidate => candidate.IsQualified);

        if (selectedCandidate == null)
        {
            throw new InvalidOperationException(
                FullKoreanSourceFontRequired + "\n" +
                "No project-contained TTF or OTF passed the temporary TMP " +
                "TryAddCharacters validation. Existing Font Assets and Scene " +
                "references were left unchanged.\n" +
                FormatCandidateResults(candidateResults)
            );
        }

        Font sourceFont = selectedCandidate.SourceFont;
        List<char> unsupportedOptional = GetUnsupportedCharacters(
            sourceFont,
            collection.OptionalUiCharacters.Where(RequiresFontGlyph)
        );

        TMP_FontAsset fontAsset = CreateTemporarySubmissionFontAsset(sourceFont);
        PopulateMandatoryKorean(fontAsset, collection.MandatoryKorean);
        List<char> optionalMissing = new List<char>(unsupportedOptional);
        optionalMissing.AddRange(PopulateSupportedOptionalCharacters(
            fontAsset,
            sourceFont,
            collection.OptionalUiCharacters
        ));
        optionalMissing = optionalMissing
            .Distinct()
            .OrderBy(character => character)
            .ToList();
        LogFontAssetState(
            "B. Mandatory and optional character population complete",
            fontAsset,
            TempFontAssetPath
        );

        List<char> mandatoryMissing = VerifyMandatoryKorean(
            fontAsset,
            collection.MandatoryKorean
        );

        if (mandatoryMissing.Count > 0)
        {
            throw new InvalidOperationException(
                "NotoSansKRGame SDF is missing mandatory Korean " +
                "characters. Scene references were not changed.\n" +
                FormatCharacters(mandatoryMissing)
            );
        }

        ValidateFontStructure(fontAsset, true);
        PersistFontSubAssets(fontAsset, TempFontAssetPath);

        TMP_FontAsset reloadedDynamicTemp =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TempFontAssetPath);

        LogFontAssetState(
            "D. Temp Asset reloaded after Dynamic sub-asset persistence",
            reloadedDynamicTemp,
            TempFontAssetPath
        );
        ValidateReloadedFontAsset(
            reloadedDynamicTemp,
            TempFontAssetPath,
            collection.MandatoryKorean,
            true
        );

        reloadedDynamicTemp.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(reloadedDynamicTemp);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            TempFontAssetPath,
            ImportAssetOptions.ForceUpdate
        );
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        TMP_FontAsset reloadedFontAsset =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TempFontAssetPath);

        LogFontAssetState(
            "D. Temp Asset reloaded after Static finalization",
            reloadedFontAsset,
            TempFontAssetPath
        );
        ValidateReloadedFontAsset(
            reloadedFontAsset,
            TempFontAssetPath,
            collection.MandatoryKorean
        );

        bool finalPromoted = false;
        bool hadExistingFinal = false;
        TMP_FontAsset finalFontAsset;

        try
        {
            hadExistingFinal = PromoteTemporaryFontAsset();
            finalPromoted = true;
            AssetDatabase.ImportAsset(
                FontAssetPath,
                ImportAssetOptions.ForceUpdate
            );
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            finalFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                FontAssetPath
            );

            LogFontAssetState(
                "F. Final Asset reloaded after promotion",
                finalFontAsset,
                FontAssetPath
            );
            ValidateReloadedFontAsset(
                finalFontAsset,
                FontAssetPath,
                collection.MandatoryKorean
            );
        }
        catch
        {
            if (finalPromoted)
            {
                RestoreBackupAfterFailedFinalValidation(hadExistingFinal);
            }

            throw;
        }

        DeleteBackupAfterFinalValidation(hadExistingFinal);
        ConfigureFallback(finalFontAsset);
        ReplaceSceneAndPrefabReferences(finalFontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        VerifySceneReferences(finalFontAsset);

        Debug.Log(
            "PATCH//BREAK Korean TMP Font verification passed.\n" +
            $"sourceFontPath={selectedCandidate.AssetPath}\n" +
            $"sourceFontGuid=" +
            $"{AssetDatabase.AssetPathToGUID(selectedCandidate.AssetPath)}\n" +
            $"sourceFontName={selectedCandidate.FontName}\n" +
            $"displayStringCount={collection.DisplayStrings.Count}\n" +
            $"mandatoryKoreanCount={collection.MandatoryKorean.Count}\n" +
            $"optionalUiCharacterCount=" +
            $"{collection.OptionalUiCharacters.Count}\n" +
            $"atlasCount={finalFontAsset.atlasTextures.Length}\n" +
            $"atlasSize={finalFontAsset.atlasWidth}x" +
            $"{finalFontAsset.atlasHeight}\n" +
            $"characterTableCount={finalFontAsset.characterTable.Count}\n" +
            $"glyphTableCount={finalFontAsset.glyphTable.Count}\n" +
            "mandatoryKoreanMissingCount=0\n" +
            $"optionalMissingCharacters={FormatInline(optionalMissing)}"
        );
    }

    private static CharacterCollection CollectDisplayCharacters()
    {
        CharacterCollection collection = new CharacterCollection();

        CollectRuntimeDisplayStringLiterals(collection);

        foreach (string scenePath in ScenePaths)
        {
            CollectSceneDisplayStrings(scenePath, collection);
        }

        foreach (string prefabGuid in AssetDatabase.FindAssets("t:Prefab"))
        {
            CollectPrefabDisplayStrings(
                AssetDatabase.GUIDToAssetPath(prefabGuid),
                collection
            );
        }

        return collection;
    }

    private static void CollectRuntimeDisplayStringLiterals(
        CharacterCollection collection
    )
    {
        foreach (string assetPath in RuntimeDisplayScriptPaths)
        {
            string absolutePath = Path.GetFullPath(assetPath);

            if (!File.Exists(absolutePath))
            {
                continue;
            }

            foreach (StringLiteral literal in ExtractCSharpStringLiterals(
                         File.ReadAllText(absolutePath)
                     ))
            {
                if (!literal.IsDiagnostic)
                {
                    collection.Add(literal.Value);
                }
            }
        }
    }

    private static IEnumerable<StringLiteral> ExtractCSharpStringLiterals(
        string source
    )
    {
        int index = 0;

        while (index < source.Length)
        {
            if (IsLineComment(source, index))
            {
                index = SkipLineComment(source, index + 2);
                continue;
            }

            if (IsBlockComment(source, index))
            {
                index = SkipBlockComment(source, index + 2);
                continue;
            }

            if (TryReadCSharpStringLiteral(
                    source,
                    index,
                    out string value,
                    out int endIndex
                ))
            {
                yield return new StringLiteral(
                    value,
                    IsDiagnosticCall(source, index)
                );
                index = endIndex;
                continue;
            }

            index++;
        }
    }

    private static bool TryReadCSharpStringLiteral(
        string source,
        int startIndex,
        out string value,
        out int endIndex
    )
    {
        value = null;
        endIndex = startIndex;
        int index = startIndex;
        bool verbatim = false;
        bool interpolated = false;

        if (source[index] == '$' || source[index] == '@')
        {
            verbatim = source[index] == '@';
            interpolated = source[index] == '$';
            index++;

            if (index < source.Length &&
                (source[index] == '$' || source[index] == '@'))
            {
                verbatim |= source[index] == '@';
                interpolated |= source[index] == '$';
                index++;
            }
        }

        if (index >= source.Length || source[index] != '"')
        {
            return false;
        }

        StringBuilder builder = new StringBuilder();
        index++;

        while (index < source.Length)
        {
            char character = source[index++];

            if (interpolated && character == '{')
            {
                if (index < source.Length && source[index] == '{')
                {
                    builder.Append('{');
                    index++;
                    continue;
                }

                index = SkipInterpolatedExpression(source, index);
                continue;
            }

            if (interpolated && character == '}' &&
                index < source.Length && source[index] == '}')
            {
                builder.Append('}');
                index++;
                continue;
            }

            if (character == '"')
            {
                if (verbatim && index < source.Length && source[index] == '"')
                {
                    builder.Append('"');
                    index++;
                    continue;
                }

                value = builder.ToString();
                endIndex = index;
                return true;
            }

            if (!verbatim && character == '\\' && index < source.Length)
            {
                builder.Append(ReadEscapeSequence(source, ref index));
                continue;
            }

            builder.Append(character);
        }

        return false;
    }

    // Interpolation expressions are code, not player-facing literal text. Do
    // not let identifiers or Unicode constants inside { ... } enter the set.
    private static int SkipInterpolatedExpression(string source, int index)
    {
        int depth = 1;

        while (index < source.Length && depth > 0)
        {
            if (source[index] == '"' || source[index] == '$' ||
                source[index] == '@')
            {
                if (TryReadCSharpStringLiteral(
                        source,
                        index,
                        out _,
                        out int stringEnd
                    ))
                {
                    index = stringEnd;
                    continue;
                }
            }

            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
            }

            index++;
        }

        return index;
    }

    private static char ReadEscapeSequence(string source, ref int index)
    {
        char escaped = source[index++];

        switch (escaped)
        {
            case 'n': return '\n';
            case 'r': return '\r';
            case 't': return '\t';
            case '\\': return '\\';
            case '"': return '"';
            case '\'': return '\'';
            case 'u':
                if (index + 4 <= source.Length &&
                    int.TryParse(
                        source.Substring(index, 4),
                        System.Globalization.NumberStyles.HexNumber,
                        null,
                        out int unicode
                    ))
                {
                    index += 4;
                    return (char)unicode;
                }
                break;
        }

        return escaped;
    }

    private static bool IsLineComment(string source, int index)
    {
        return index + 1 < source.Length && source[index] == '/' &&
               source[index + 1] == '/';
    }

    private static bool IsBlockComment(string source, int index)
    {
        return index + 1 < source.Length && source[index] == '/' &&
               source[index + 1] == '*';
    }

    private static int SkipLineComment(string source, int index)
    {
        while (index < source.Length && source[index] != '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipBlockComment(string source, int index)
    {
        while (index + 1 < source.Length &&
               !(source[index] == '*' && source[index + 1] == '/'))
        {
            index++;
        }

        return Mathf.Min(index + 2, source.Length);
    }

    private static bool IsDiagnosticCall(string source, int literalIndex)
    {
        int start = Mathf.Max(0, source.LastIndexOf(';', literalIndex) + 1);
        string statement = source.Substring(start, literalIndex - start);
        return statement.Contains("Debug.Log") ||
               statement.Contains("LogBriefing") ||
               statement.Contains("LogKorean") ||
               statement.Contains("Diagnostics");
    }

    private static void CollectSceneDisplayStrings(
        string scenePath,
        CharacterCollection collection
    )
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        CollectLoadedObjectsDisplayStrings(collection);
    }

    private static void CollectPrefabDisplayStrings(
        string prefabPath,
        CharacterCollection collection
    )
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            foreach (TMP_Text text in prefabRoot.GetComponentsInChildren<TMP_Text>(
                         true
                     ))
            {
                collection.Add(text.text);
            }

            foreach (MonoBehaviour component in
                     prefabRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                CollectSerializedDisplayStrings(component, collection);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void CollectLoadedObjectsDisplayStrings(
        CharacterCollection collection
    )
    {
        foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None
                 ))
        {
            collection.Add(text.text);
        }

        foreach (MonoBehaviour component in
                 UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None
                 ))
        {
            CollectSerializedDisplayStrings(component, collection);
        }
    }

    private static void CollectSerializedDisplayStrings(
        MonoBehaviour component,
        CharacterCollection collection
    )
    {
        if (component == null || !IsRuntimeDisplayComponent(component))
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyType == SerializedPropertyType.String &&
                IsDisplayStringProperty(component, property.propertyPath))
            {
                collection.Add(property.stringValue);
            }
        }
    }

    // Only serialized copy that is assigned to player-facing TMP UI belongs in
    // the font character set. Scene names, diagnostics switches, and other
    // configuration strings are intentionally excluded.
    private static bool IsDisplayStringProperty(
        MonoBehaviour component,
        string propertyPath
    )
    {
        if (component is MainMenuController)
        {
            return propertyPath.StartsWith("prologuePages.Array.data[",
                       StringComparison.Ordinal) &&
                   (propertyPath.EndsWith(".header", StringComparison.Ordinal) ||
                    propertyPath.EndsWith(".body", StringComparison.Ordinal) ||
                    propertyPath.EndsWith(
                        ".buttonLabel",
                        StringComparison.Ordinal
                    ));
        }

        if (component is BattleBriefingController)
        {
            return propertyPath == "missionText" ||
                   propertyPath == "titleCopy" ||
                   propertyPath == "descriptionCopy" ||
                   propertyPath == "rulesCopy" ||
                   propertyPath == "controlsCopy" ||
                   propertyPath == "startButtonText";
        }

        if (component is RuntimeConsoleUI)
        {
            return propertyPath == "defaultProgram";
        }

        if (component is LivePatchUI)
        {
            return propertyPath == "adaptiveHintMessage";
        }

        if (component is EndingScreenController)
        {
            return propertyPath == "title" || propertyPath == "subtitle";
        }

        return component.GetType().Name == "BattleManager" &&
               (propertyPath == "enemyDisplayName" ||
                propertyPath == "victoryBodyOverride");
    }

    private static bool IsRuntimeDisplayComponent(MonoBehaviour component)
    {
        return component is MainMenuController ||
               component is BattleBriefingController ||
               component is RuntimeConsoleUI ||
               component is LivePatchUI ||
               component is EndingScreenController ||
               component.GetType().Name == "BattleManager";
    }

    private static void WriteCharacterSetFile(CharacterCollection collection)
    {
        string absolutePath = Path.Combine(
            Application.dataPath,
            "Editor",
            "Generated",
            "PatchBreakKoreanCharacterSet.txt"
        );
        string directory = Path.GetDirectoryName(absolutePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder output = new StringBuilder();
        output.AppendLine("# Mandatory Korean");
        output.AppendLine(new string(collection.MandatoryKorean.ToArray()));
        output.AppendLine("# Optional UI Characters");

        foreach (char character in collection.OptionalUiCharacters)
        {
            output.Append("U+")
                .Append(((int)character).ToString("X4"))
                .Append('\t')
                .AppendLine(EscapeForReport(character));
        }

        File.WriteAllText(
            absolutePath,
            output.ToString(),
            new UTF8Encoding(false)
        );
        AssetDatabase.ImportAsset(
            CharacterSetPath,
            ImportAssetOptions.ForceSynchronousImport
        );
    }

    private static List<char> GetUnsupportedCharacters(
        Font sourceFont,
        IEnumerable<char> characters
    )
    {
        return characters
            .Where(RequiresFontGlyph)
            .Where(character => !sourceFont.HasCharacter(character))
            .Distinct()
            .OrderBy(character => character)
            .ToList();
    }

    private static List<FontCandidateValidation> ValidateProjectFontCandidates(
        IEnumerable<char> mandatoryKorean
    )
    {
        List<string> candidatePaths = AssetDatabase.FindAssets(
                "t:Font",
                new[] { "Assets" }
            )
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsProjectTtfOrOtf)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => string.Equals(
                path,
                PreferredSourceFontPath,
                StringComparison.Ordinal
            ) ? 0 : 1)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();

        List<FontCandidateValidation> results = candidatePaths
            .Select(path => ValidateFontCandidate(path, mandatoryKorean))
            .ToList();

        if (results.Count == 0)
        {
            Debug.LogWarning(
                "PATCH//BREAK found no TTF or OTF candidates under Assets."
            );
        }

        foreach (FontCandidateValidation result in results)
        {
            Debug.Log(result.FormatForLog());
        }

        return results;
    }

    private static bool IsProjectTtfOrOtf(string assetPath)
    {
        return assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
               (assetPath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                assetPath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));
    }

    private static FontCandidateValidation ValidateFontCandidate(
        string assetPath,
        IEnumerable<char> mandatoryKorean
    )
    {
        FontCandidateValidation result = new FontCandidateValidation(assetPath);
        string validationPath = null;
        TMP_FontAsset validationAsset = null;
        bool validationAssetCreated = false;

        try
        {
            result.SourceFont = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
            result.FontName = result.SourceFont != null
                ? result.SourceFont.name
                : "<load failed>";
            result.FileSizeBytes = GetFileSize(assetPath);
            result.ExistingUses = FindExistingUses(assetPath);

            if (result.SourceFont == null)
            {
                result.FailureReason = "Font importer could not load the source file.";
                return result;
            }

            EnsureValidationDirectory();
            validationPath = GetValidationAssetPath(result.FontName);
            DeleteOwnedTemporaryAsset(validationPath);
            validationAsset = CreateDynamicFontAsset(result.SourceFont);

            if (validationAsset == null)
            {
                result.FailureReason = "TMP_FontAsset.CreateFontAsset returned null.";
                return result;
            }

            validationAsset.name = Path.GetFileNameWithoutExtension(validationPath);
            AssetDatabase.CreateAsset(validationAsset, validationPath);
            validationAssetCreated = true;
            result.TempAssetCreated = true;
            AssetDatabase.SaveAssets();

            string mandatoryText = new string(
                mandatoryKorean
                    .Where(RequiresFontGlyph)
                    .Distinct()
                    .OrderBy(character => character)
                    .ToArray()
            );
            result.TryAddResult = validationAsset.TryAddCharacters(
                mandatoryText,
                out string missingCharacters
            );
            result.MissingCharacters = missingCharacters ?? string.Empty;
            result.AllMandatoryCharactersPresent = mandatoryText.All(character =>
                validationAsset.HasCharacter(character, false, false)
            );
            result.HasRequiredStructure =
                validationAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic &&
                validationAsset.atlasWidth == AtlasSize &&
                validationAsset.atlasHeight == AtlasSize &&
                validationAsset.isMultiAtlasTexturesEnabled &&
                validationAsset.atlasTextures != null &&
                validationAsset.atlasTextures.Length >= 1 &&
                validationAsset.material != null &&
                validationAsset.sourceFontFile != null;

            if (!result.IsTryAddAccepted)
            {
                result.FailureReason =
                    "TryAddCharacters returned false and reported missing characters.";
            }
            else if (!result.AllMandatoryCharactersPresent)
            {
                result.FailureReason =
                    "TMP font asset did not contain every mandatory character.";
            }
            else if (!result.HasRequiredStructure)
            {
                result.FailureReason =
                    "TMP font asset did not create the required atlas, material, or source reference.";
            }
        }
        catch (Exception exception)
        {
            result.FailureReason = exception.GetType().Name + ": " + exception.Message;
        }
        finally
        {
            if (validationAssetCreated && !string.IsNullOrEmpty(validationPath))
            {
                DeleteOwnedTemporaryAsset(validationPath);
            }
        }

        return result;
    }

    private static void EnsureValidationDirectory()
    {
        string absolutePath = Path.GetFullPath(FontValidationDirectory);

        if (!Directory.Exists(absolutePath))
        {
            Directory.CreateDirectory(absolutePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static string GetValidationAssetPath(string fontName)
    {
        StringBuilder safeName = new StringBuilder(fontName.Length);

        foreach (char character in fontName)
        {
            safeName.Append(char.IsLetterOrDigit(character) || character == '_' ||
                            character == '-' ? character : '_');
        }

        return FontValidationDirectory + "/" + safeName + " Test SDF.asset";
    }

    private static long GetFileSize(string assetPath)
    {
        string absolutePath = Path.GetFullPath(assetPath);
        return File.Exists(absolutePath) ? new FileInfo(absolutePath).Length : 0L;
    }

    private static List<string> FindExistingUses(string assetPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);

        if (string.IsNullOrEmpty(guid))
        {
            return new List<string>();
        }

        return AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
            .Where(path => !string.Equals(path, assetPath, StringComparison.Ordinal))
            .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => AssetTextContains(path, guid))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool AssetTextContains(string assetPath, string text)
    {
        try
        {
            return File.ReadAllText(Path.GetFullPath(assetPath)).Contains(text);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DeleteOwnedTemporaryAsset(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null &&
            !AssetDatabase.DeleteAsset(assetPath))
        {
            throw new InvalidOperationException(
                $"Could not clean up temporary font asset: {assetPath}"
            );
        }
    }

    private static TMP_FontAsset CreateDynamicFontAsset(Font sourceFont)
    {
        return TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            AtlasPadding,
            GlyphRenderMode.SDFAA,
            AtlasSize,
            AtlasSize,
            AtlasPopulationMode.Dynamic,
            true
        );
    }

    private static TMP_FontAsset CreateTemporarySubmissionFontAsset(Font sourceFont)
    {
        DeleteOwnedTemporaryAsset(TempFontAssetPath);

        TMP_FontAsset fontAsset = CreateDynamicFontAsset(sourceFont);

        if (fontAsset == null)
        {
            throw new InvalidOperationException(
                "TextMeshPro failed to create the temporary NotoSansKRGame SDF."
            );
        }

        LogFontAssetState(
            "A. TMP_FontAsset.CreateFontAsset completed",
            fontAsset,
            TempFontAssetPath
        );
        fontAsset.name = "NotoSansKRGame Temp SDF";
        AssetDatabase.CreateAsset(fontAsset, TempFontAssetPath);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static bool PromoteTemporaryFontAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TempFontAssetPath) == null)
        {
            throw new InvalidOperationException(
                "Validated temporary font asset is missing before promotion."
            );
        }

        bool hadExistingFinal =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(FontAssetPath) != null;

        if (hadExistingFinal)
        {
            DeleteOwnedTemporaryAsset(BackupFontAssetPath);
            string moveToBackupError = AssetDatabase.MoveAsset(
                FontAssetPath,
                BackupFontAssetPath
            );

            if (!string.IsNullOrEmpty(moveToBackupError))
            {
                throw new InvalidOperationException(
                    "Could not protect the existing final font asset before " +
                    "promotion: " + moveToBackupError
                );
            }
        }

        string promoteError = AssetDatabase.MoveAsset(
            TempFontAssetPath,
            FontAssetPath
        );

        if (!string.IsNullOrEmpty(promoteError))
        {
            if (hadExistingFinal)
            {
                string restoreError = AssetDatabase.MoveAsset(
                    BackupFontAssetPath,
                    FontAssetPath
                );
                promoteError += string.IsNullOrEmpty(restoreError)
                    ? " Existing final asset was restored."
                    : " Existing final asset could not be restored: " + restoreError;
            }

            throw new InvalidOperationException(
                "Could not promote validated temporary font asset: " + promoteError
            );
        }

        try
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            LogFontAssetState(
                "E. Final Asset promoted with AssetDatabase.MoveAsset",
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath),
                FontAssetPath
            );
            return hadExistingFinal;
        }
        catch
        {
            RestoreBackupAfterFailedFinalValidation(hadExistingFinal);
            throw;
        }
    }

    private static void RestoreBackupAfterFailedFinalValidation(
        bool hadExistingFinal
    )
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(FontAssetPath) != null &&
            !AssetDatabase.DeleteAsset(FontAssetPath))
        {
            throw new InvalidOperationException(
                "Could not remove the failed final font asset before backup " +
                "restoration."
            );
        }

        if (hadExistingFinal)
        {
            string restoreError = AssetDatabase.MoveAsset(
                BackupFontAssetPath,
                FontAssetPath
            );

            if (!string.IsNullOrEmpty(restoreError))
            {
                throw new InvalidOperationException(
                    "Could not restore the previous final font asset: " +
                    restoreError
                );
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void DeleteBackupAfterFinalValidation(bool hadExistingFinal)
    {
        if (hadExistingFinal &&
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(BackupFontAssetPath) !=
                null &&
            !AssetDatabase.DeleteAsset(BackupFontAssetPath))
        {
            throw new InvalidOperationException(
                "The validated final font asset was created, but its backup " +
                "could not be removed: " + BackupFontAssetPath
            );
        }
    }

    private static void PersistFontSubAssets(
        TMP_FontAsset fontAsset,
        string assetPath
    )
    {
        if (fontAsset == null)
        {
            throw new InvalidOperationException(
                "Cannot persist sub-assets for a null TMP Font Asset."
            );
        }

        string rootPath = AssetDatabase.GetAssetPath(fontAsset);

        if (string.IsNullOrEmpty(rootPath))
        {
            AssetDatabase.CreateAsset(fontAsset, assetPath);
        }
        else if (!string.Equals(rootPath, assetPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "TMP Font Asset is already stored at an unexpected path: " +
                rootPath
            );
        }

        if (fontAsset.atlasTextures == null ||
            fontAsset.atlasTextures.Length == 0)
        {
            throw new InvalidOperationException(
                "TMP Font Asset has no Atlas Texture to persist."
            );
        }

        Texture2D primaryAtlas = fontAsset.atlasTextures[0];

        if (primaryAtlas == null)
        {
            throw new InvalidOperationException(
                "TMP Font Asset primary Atlas Texture is null."
            );
        }

        for (int index = 0; index < fontAsset.atlasTextures.Length; index++)
        {
            Texture2D atlasTexture = fontAsset.atlasTextures[index];

            if (atlasTexture == null)
            {
                throw new InvalidOperationException(
                    $"TMP Font Asset Atlas Texture {index} is null."
                );
            }

            if (string.IsNullOrEmpty(atlasTexture.name))
            {
                atlasTexture.name = $"NotoSansKRGame Atlas {index}";
            }

            if (!AssetDatabase.Contains(atlasTexture))
            {
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }

            EnsureSubAssetBelongsToFontAsset(atlasTexture, assetPath);
            EditorUtility.SetDirty(atlasTexture);
        }

        Material material = fontAsset.material;

        if (material == null)
        {
            throw new InvalidOperationException(
                "TMP Font Asset has no Material to persist."
            );
        }

        if (string.IsNullOrEmpty(material.name))
        {
            material.name = "NotoSansKRGame Material";
        }

        if (!AssetDatabase.Contains(material))
        {
            AssetDatabase.AddObjectToAsset(material, fontAsset);
        }

        EnsureSubAssetBelongsToFontAsset(material, assetPath);

        if (material.mainTexture != primaryAtlas)
        {
            throw new InvalidOperationException(
                "TMP Font Asset Material does not reference its primary Atlas " +
                "Texture before persistence."
            );
        }

        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        LogFontAssetState(
            "C. Temp Asset saved after Material and Atlas sub-asset persistence",
            fontAsset,
            assetPath
        );
    }

    private static void EnsureSubAssetBelongsToFontAsset(
        UnityEngine.Object subAsset,
        string fontAssetPath
    )
    {
        string subAssetPath = AssetDatabase.GetAssetPath(subAsset);

        if (!string.Equals(subAssetPath, fontAssetPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"TMP Font sub-asset '{subAsset.name}' is stored at " +
                $"'{subAssetPath}', not '{fontAssetPath}'."
            );
        }
    }

    private static void ValidateReloadedFontAsset(
        TMP_FontAsset fontAsset,
        string assetPath,
        IEnumerable<char> mandatoryKorean,
        bool requireSourceFontFile = false
    )
    {
        if (fontAsset == null)
        {
            throw new InvalidOperationException(
                "TMP Font Asset could not be reloaded from: " + assetPath
            );
        }

        ValidateFontStructure(fontAsset, requireSourceFontFile);
        List<char> mandatoryMissing = VerifyMandatoryKorean(
            fontAsset,
            mandatoryKorean
        );

        if (mandatoryMissing.Count > 0)
        {
            throw new InvalidOperationException(
                "Reloaded TMP Font Asset is missing mandatory Korean " +
                "characters.\n" + FormatCharacters(mandatoryMissing)
            );
        }

        ValidatePersistedSubAssets(fontAsset, assetPath);
    }

    private static void ValidatePersistedSubAssets(
        TMP_FontAsset fontAsset,
        string assetPath
    )
    {
        UnityEngine.Object[] subAssets =
            AssetDatabase.LoadAllAssetsAtPath(assetPath);
        int fontAssetCount = subAssets.Count(asset => asset is TMP_FontAsset);
        int materialCount = subAssets.Count(asset => asset is Material);
        int textureCount = subAssets.Count(asset => asset is Texture2D);

        if (fontAssetCount != 1 || materialCount < 1 || textureCount < 1)
        {
            throw new InvalidOperationException(
                "TMP Font Asset sub-asset validation failed at " + assetPath +
                $". TMP_FontAsset={fontAssetCount} Material={materialCount} " +
                $"Texture2D={textureCount}"
            );
        }

        EnsureSubAssetBelongsToFontAsset(fontAsset.material, assetPath);

        foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
        {
            if (atlasTexture == null)
            {
                throw new InvalidOperationException(
                    "Reloaded TMP Font Asset has a null Atlas Texture."
                );
            }

            EnsureSubAssetBelongsToFontAsset(atlasTexture, assetPath);
        }
    }

    private static void LogFontAssetState(
        string stage,
        TMP_FontAsset fontAsset,
        string assetPath
    )
    {
        StringBuilder report = new StringBuilder();
        report.Append("PATCH//BREAK TMP Font persistence state\n")
            .Append("stage=").AppendLine(stage)
            .Append("requestedPath=").AppendLine(assetPath)
            .Append("assetPath=").AppendLine(
                fontAsset != null ? AssetDatabase.GetAssetPath(fontAsset) : "<null>"
            )
            .Append("fontAssetNull=").AppendLine((fontAsset == null).ToString());

        if (fontAsset != null)
        {
            Material material = fontAsset.material;
            report.Append("materialNull=").AppendLine((material == null).ToString())
                .Append("materialName=").AppendLine(
                    material != null ? material.name : "<null>"
                )
                .Append("materialAssetDatabaseContains=").AppendLine(
                    (material != null && AssetDatabase.Contains(material)).ToString()
                )
                .Append("atlasPopulationMode=").AppendLine(
                    fontAsset.atlasPopulationMode.ToString()
                )
                .Append("characterTableCount=").AppendLine(
                    fontAsset.characterTable.Count.ToString()
                )
                .Append("glyphTableCount=").AppendLine(
                    fontAsset.glyphTable.Count.ToString()
                );

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            int atlasCount = atlasTextures != null ? atlasTextures.Length : 0;
            report.Append("atlasTextureCount=").AppendLine(atlasCount.ToString());

            for (int index = 0; index < atlasCount; index++)
            {
                Texture2D atlasTexture = atlasTextures[index];
                report.Append("atlas[").Append(index).Append("]Name=").AppendLine(
                    atlasTexture != null ? atlasTexture.name : "<null>"
                ).Append("atlas[").Append(index).Append("]AssetDatabaseContains=")
                    .AppendLine(
                        (atlasTexture != null && AssetDatabase.Contains(atlasTexture))
                            .ToString()
                    );
            }
        }

        Debug.Log(report.ToString());
        LogFontSubAssetComposition(stage, assetPath);
    }

    private static void LogFontSubAssetComposition(string stage, string assetPath)
    {
        StringBuilder report = new StringBuilder();
        report.Append("PATCH//BREAK TMP Font sub-assets\n")
            .Append("stage=").AppendLine(stage)
            .Append("assetPath=").AppendLine(assetPath);

        foreach (UnityEngine.Object asset in
                 AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            bool hasIdentifier = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                asset,
                out string guid,
                out long localIdentifier
            );
            report.Append("name=").Append(asset.name)
                .Append(" type=").Append(asset.GetType().FullName)
                .Append(" hasLocalFileIdentifier=").Append(hasIdentifier)
                .Append(" guid=").Append(guid ?? string.Empty)
                .Append(" localFileIdentifier=").AppendLine(
                    hasIdentifier ? localIdentifier.ToString() : "<unavailable>"
                );
        }

        Debug.Log(report.ToString());
    }

    private static string FormatCandidateResults(
        IEnumerable<FontCandidateValidation> results
    )
    {
        return string.Join(
            "\n",
            results.Select(result => result.FormatForLog())
        );
    }

    private static void PopulateMandatoryKorean(
        TMP_FontAsset fontAsset,
        IEnumerable<char> mandatoryKorean
    )
    {
        List<char> requestedCharacters = mandatoryKorean
            .Where(RequiresFontGlyph)
            .Distinct()
            .OrderBy(character => character)
            .ToList();
        string mandatoryText = new string(requestedCharacters.ToArray());
        bool result = fontAsset.TryAddCharacters(
            mandatoryText,
            out string missingCharacters
        );

        missingCharacters ??= string.Empty;
        Debug.Log(
            "PATCH//BREAK TMP Font batch group=mandatory Korean " +
            $"requested={requestedCharacters.Count} result={result} " +
            $"missing='{missingCharacters}'"
        );

        // TMP may return false even though it reports no missing characters.
        // The character table is the authoritative second check in that case.
        if (!string.IsNullOrEmpty(missingCharacters))
        {
            throw new InvalidOperationException(
                "Failed to add mandatory Korean characters.\n" +
                FormatCharacters(missingCharacters)
            );
        }

        List<char> missingFromTable = VerifyMandatoryKorean(
            fontAsset,
            requestedCharacters
        );

        if (missingFromTable.Count > 0)
        {
            throw new InvalidOperationException(
                "TMP character table is missing mandatory Korean characters " +
                "after TryAddCharacters.\n" +
                FormatCharacters(missingFromTable)
            );
        }
    }

    private static List<char> PopulateSupportedOptionalCharacters(
        TMP_FontAsset fontAsset,
        Font sourceFont,
        IEnumerable<char> optionalCharacters
    )
    {
        List<char> sourceSupported = optionalCharacters
            .Where(RequiresFontGlyph)
            .Where(character => sourceFont.HasCharacter(character))
            .ToList();
        return AddCharactersInBatches(
            fontAsset,
            sourceSupported,
            "optional UI"
        );
    }

    private static List<char> AddCharactersInBatches(
        TMP_FontAsset fontAsset,
        IEnumerable<char> characters,
        string groupName
    )
    {
        List<char> requestedCharacters = characters
            .Where(RequiresFontGlyph)
            .Distinct()
            .OrderBy(character => character)
            .ToList();
        List<char> missingCharacters = new List<char>();

        for (int start = 0;
             start < requestedCharacters.Count;
             start += CharacterBatchSize)
        {
            List<char> batch = requestedCharacters
                .Skip(start)
                .Take(CharacterBatchSize)
                .ToList();
            string batchText = new string(batch.ToArray());
            bool added = fontAsset.TryAddCharacters(
                batchText,
                out string batchMissing
            );

            Debug.Log(
                "PATCH//BREAK TMP Font batch " +
                $"group={groupName} index={start / CharacterBatchSize} " +
                $"requested={batch.Count} result={added} " +
                $"missing='{batchMissing}'"
            );

            if (!string.IsNullOrEmpty(batchMissing))
            {
                missingCharacters.AddRange(batchMissing);
            }

            if (!added && string.IsNullOrEmpty(batchMissing))
            {
                missingCharacters.AddRange(batch.Where(character =>
                    !fontAsset.HasCharacter(character, false, false)
                ));
            }
        }

        return missingCharacters
            .Distinct()
            .OrderBy(character => character)
            .ToList();
    }

    private static void ConfigureFallback(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
        {
            Debug.LogWarning(
                "PATCH//BREAK Korean TMP font fallback was not assigned: " +
                "the generated font asset is null."
            );
            return;
        }

        TMP_FontAsset liberationFallback =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFallbackPath);

        if (liberationFallback == null)
        {
            Debug.LogWarning(
                "PATCH//BREAK Korean TMP font fallback was not assigned: " +
                "LiberationSans SDF asset is missing."
            );
            return;
        }

        if (fontAsset.fallbackFontAssetTable == null)
        {
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }

        if (!fontAsset.fallbackFontAssetTable.Contains(liberationFallback))
        {
            fontAsset.fallbackFontAssetTable.Add(liberationFallback);
            EditorUtility.SetDirty(fontAsset);
        }
    }

    private static List<char> VerifyMandatoryKorean(
        TMP_FontAsset fontAsset,
        IEnumerable<char> mandatoryKorean
    )
    {
        return mandatoryKorean
            .Where(character =>
                !fontAsset.HasCharacter(character, false, false)
            )
            .OrderBy(character => character)
            .ToList();
    }

    private static void ValidateFontStructure(
        TMP_FontAsset fontAsset,
        bool requireSourceFontFile
    )
    {
        if (fontAsset == null)
        {
            throw new InvalidOperationException(
                "Generated font asset is null."
            );
        }

        if (requireSourceFontFile &&
            fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
        {
            throw new InvalidOperationException(
                "Expected a Dynamic font asset before character population."
            );
        }

        if (!requireSourceFontFile &&
            fontAsset.atlasPopulationMode != AtlasPopulationMode.Static)
        {
            throw new InvalidOperationException(
                "Expected a Static font asset after finalization."
            );
        }

        if (requireSourceFontFile && fontAsset.sourceFontFile == null)
        {
            throw new InvalidOperationException(
                "Dynamic font asset has no Source Font File."
            );
        }

        if (fontAsset.material == null)
        {
            throw new InvalidOperationException(
                "Generated font asset has no Material."
            );
        }

        if (fontAsset.atlasTextures == null ||
            fontAsset.atlasTextures.Length == 0 ||
            fontAsset.atlasTextures.Any(texture => texture == null))
        {
            throw new InvalidOperationException(
                "Generated font asset has a missing Atlas Texture."
            );
        }

        if (fontAsset.material.mainTexture == null ||
            Array.IndexOf(
                fontAsset.atlasTextures,
                fontAsset.material.mainTexture
            ) < 0)
        {
            throw new InvalidOperationException(
                "Generated font material does not reference one of its " +
                "Atlas Textures."
            );
        }

        if (fontAsset.characterTable.Count == 0 ||
            fontAsset.glyphTable.Count == 0)
        {
            throw new InvalidOperationException(
                "Generated font asset has an empty Character or Glyph Table."
            );
        }
    }

    private static void ReplaceSceneAndPrefabReferences(TMP_FontAsset fontAsset)
    {
        foreach (string scenePath in ScenePaths)
        {
            ReplaceSceneReferences(scenePath, fontAsset);
        }

        foreach (string prefabGuid in AssetDatabase.FindAssets("t:Prefab"))
        {
            ReplacePrefabReferences(
                AssetDatabase.GUIDToAssetPath(prefabGuid),
                fontAsset
            );
        }
    }

    private static void ReplaceSceneReferences(
        string scenePath,
        TMP_FontAsset fontAsset
    )
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        bool changed = ApplyLoadedSceneFontReferences(fontAsset);

        if (string.Equals(
                scenePath,
                "Assets/Scenes/DebuggerBattle.unity",
                StringComparison.Ordinal
            ))
        {
            changed |= ApplyDebuggerBriefingLayout();
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static bool ApplyLoadedSceneFontReferences(TMP_FontAsset fontAsset)
    {
        bool changed = false;

        foreach (BattleBriefingController controller in
                 UnityEngine.Object.FindObjectsByType<BattleBriefingController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None
                 ))
        {
            changed |= SetFontReference(
                controller,
                "koreanFontAsset",
                fontAsset
            );
        }

        foreach (MainMenuController controller in
                 UnityEngine.Object.FindObjectsByType<MainMenuController>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None
                 ))
        {
            changed |= SetFontReference(
                controller,
                "koreanFontAsset",
                fontAsset
            );
        }

        foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None
                 ))
        {
            if (ContainsActualKorean(text.text))
            {
                changed |= ApplyFont(text, fontAsset);
            }
        }

        return changed;
    }

    private static void ReplacePrefabReferences(
        string prefabPath,
        TMP_FontAsset fontAsset
    )
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;

        try
        {
            foreach (TMP_Text text in prefabRoot.GetComponentsInChildren<TMP_Text>(
                         true
                     ))
            {
                if (ContainsActualKorean(text.text))
                {
                    changed |= ApplyFont(text, fontAsset);
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool SetFontReference(
        UnityEngine.Object target,
        string propertyName,
        TMP_FontAsset fontAsset
    )
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || property.objectReferenceValue == fontAsset)
        {
            return false;
        }

        property.objectReferenceValue = fontAsset;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool ApplyFont(TMP_Text text, TMP_FontAsset fontAsset)
    {
        if (text.font == fontAsset &&
            text.fontSharedMaterial == fontAsset.material)
        {
            return false;
        }

        text.font = fontAsset;
        text.fontSharedMaterial = fontAsset.material;
        text.SetAllDirty();
        EditorUtility.SetDirty(text);
        return true;
    }

    private static bool ApplyDebuggerBriefingLayout()
    {
        TMP_Text description = FindBriefingText("DescriptionText");
        TMP_Text rules = FindBriefingText("RulesText");
        TMP_Text controls = FindBriefingText("ControlsText");
        RectTransform button = FindBriefingButtonRect();

        if (description == null || rules == null || controls == null ||
            button == null)
        {
            throw new InvalidOperationException(
                "Debugger briefing layout references are missing."
            );
        }

        const float contentTop = 200f;
        const float sectionGap = 16f;
        const float safeButtonGap = 24f;

        description.ForceMeshUpdate(true, true);
        rules.ForceMeshUpdate(true, true);
        controls.ForceMeshUpdate(true, true);

        float descriptionHeight = Mathf.Max(
            150f,
            Mathf.Ceil(description.preferredHeight) + 8f
        );
        float descriptionBottom = contentTop - descriptionHeight;
        float rulesHeight = Mathf.Max(
            160f,
            Mathf.Ceil(rules.preferredHeight) + 8f
        );
        float rulesTop = descriptionBottom - sectionGap;
        float rulesBottom = rulesTop - rulesHeight;
        float controlsHeight = Mathf.Max(
            80f,
            Mathf.Ceil(controls.preferredHeight) + 8f
        );
        float controlsTop = rulesBottom - sectionGap;
        float controlsBottom = controlsTop - controlsHeight;
        float buttonTop = button.anchoredPosition.y + button.rect.height * 0.5f;

        if (controlsBottom < buttonTop + safeButtonGap)
        {
            throw new InvalidOperationException(
                "Debugger briefing content does not fit above the ENTER " +
                "CORE button at 1280x720."
            );
        }

        bool changed = SetRectTransform(
            description.rectTransform,
            new Vector2(0f, contentTop - descriptionHeight * 0.5f),
            new Vector2(760f, descriptionHeight)
        );
        changed |= SetRectTransform(
            rules.rectTransform,
            new Vector2(0f, rulesTop - rulesHeight * 0.5f),
            new Vector2(760f, rulesHeight)
        );
        changed |= SetRectTransform(
            controls.rectTransform,
            new Vector2(0f, controlsTop - controlsHeight * 0.5f),
            new Vector2(760f, controlsHeight)
        );

        description.ForceMeshUpdate(true, true);
        rules.ForceMeshUpdate(true, true);
        controls.ForceMeshUpdate(true, true);

        if (description.isTextOverflowing || rules.isTextOverflowing ||
            controls.isTextOverflowing)
        {
            throw new InvalidOperationException(
                "Debugger briefing text still overflows after layout " +
                "adjustment."
            );
        }

        RectTransform card = description.rectTransform.parent as RectTransform;

        if (card == null ||
            !IsFullyInsideCard(description.rectTransform, card) ||
            !IsFullyInsideCard(rules.rectTransform, card) ||
            !IsFullyInsideCard(controls.rectTransform, card) ||
            !IsFullyInsideCard(button, card) ||
            RectTransformsOverlap(description.rectTransform, rules.rectTransform) ||
            RectTransformsOverlap(rules.rectTransform, controls.rectTransform) ||
            RectTransformsOverlap(controls.rectTransform, button))
        {
            throw new InvalidOperationException(
                "Debugger briefing text or controls overlap, or do not fit " +
                "inside the 1280x720 briefing card."
            );
        }

        return changed;
    }

    private static bool IsFullyInsideCard(
        RectTransform child,
        RectTransform card
    )
    {
        Rect childRect = GetWorldRect(child);
        Rect cardRect = GetWorldRect(card);
        const float tolerance = 0.01f;

        return childRect.xMin >= cardRect.xMin - tolerance &&
               childRect.xMax <= cardRect.xMax + tolerance &&
               childRect.yMin >= cardRect.yMin - tolerance &&
               childRect.yMax <= cardRect.yMax + tolerance;
    }

    private static bool RectTransformsOverlap(
        RectTransform first,
        RectTransform second
    )
    {
        return GetWorldRect(first).Overlaps(GetWorldRect(second), true);
    }

    private static Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float xMin = corners.Min(corner => corner.x);
        float xMax = corners.Max(corner => corner.x);
        float yMin = corners.Min(corner => corner.y);
        float yMax = corners.Max(corner => corner.y);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static TMP_Text FindBriefingText(string objectName)
    {
        return UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            )
            .FirstOrDefault(text =>
                text.name == objectName &&
                text.transform.parent != null &&
                text.transform.parent.name == "BriefingCard"
            );
    }

    private static RectTransform FindBriefingButtonRect()
    {
        return UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            )
            .Where(button => button.name == "StartMissionButton")
            .Select(button => button.GetComponent<RectTransform>())
            .FirstOrDefault(rect =>
                rect != null && rect.parent != null &&
                rect.parent.name == "BriefingCard"
            );
    }

    private static bool SetRectTransform(
        RectTransform rectTransform,
        Vector2 anchoredPosition,
        Vector2 sizeDelta
    )
    {
        if (rectTransform.anchoredPosition == anchoredPosition &&
            rectTransform.sizeDelta == sizeDelta)
        {
            return false;
        }

        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        EditorUtility.SetDirty(rectTransform);
        return true;
    }

    private static void VerifySceneReferences(TMP_FontAsset fontAsset)
    {
        foreach (string scenePath in ScenePaths)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            foreach (BattleBriefingController controller in
                     UnityEngine.Object.FindObjectsByType<
                         BattleBriefingController
                     >(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None
                     ))
            {
                VerifyFontReference(controller, "koreanFontAsset", fontAsset);
            }

            foreach (MainMenuController controller in
                     UnityEngine.Object.FindObjectsByType<MainMenuController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None
                     ))
            {
                VerifyFontReference(controller, "koreanFontAsset", fontAsset);
            }

            foreach (TMP_Text text in
                     UnityEngine.Object.FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None
                     ))
            {
                if (ContainsActualKorean(text.text) && text.font != fontAsset)
                {
                    throw new InvalidOperationException(
                        $"Korean TMP font reference did not persist for " +
                        $"{text.name} in {scenePath}."
                    );
                }
            }
        }
    }

    private static void VerifyFontReference(
        UnityEngine.Object target,
        string propertyName,
        TMP_FontAsset expectedFontAsset
    )
    {
        SerializedProperty property =
            new SerializedObject(target).FindProperty(propertyName);

        if (property == null ||
            property.objectReferenceValue != expectedFontAsset)
        {
            throw new InvalidOperationException(
                $"Font reference did not persist for {target.name}."
            );
        }
    }

    private static bool ContainsActualKorean(string text)
    {
        return !string.IsNullOrEmpty(text) && text.Any(IsKoreanCharacter);
    }

    private static bool IsKoreanCharacter(char character)
    {
        return (character >= '\u1100' && character <= '\u11FF') ||
               (character >= '\u3130' && character <= '\u318F') ||
               (character >= '\uA960' && character <= '\uA97F') ||
               (character >= '\uAC00' && character <= '\uD7A3') ||
               (character >= '\uD7B0' && character <= '\uD7FF');
    }

    private static bool RequiresFontGlyph(char character)
    {
        return character != '\r' && character != '\n';
    }

    private static string FormatCharacters(IEnumerable<char> characters)
    {
        StringBuilder report = new StringBuilder();

        foreach (char character in characters.Distinct().OrderBy(value => value))
        {
            report.Append("character='")
                .Append(EscapeForReport(character))
                .Append("' unicode=U+")
                .Append(((int)character).ToString("X4"))
                .AppendLine();
        }

        return report.ToString();
    }

    private static string FormatInline(IEnumerable<char> characters)
    {
        return string.Concat(
            characters
                .Distinct()
                .OrderBy(character => character)
                .Select(EscapeForReport)
        );
    }

    private static string EscapeForReport(char character)
    {
        switch (character)
        {
            case '\n': return "\\n";
            case '\r': return "\\r";
            case '\t': return "\\t";
            case ' ': return "<space>";
            default: return character.ToString();
        }
    }

    private sealed class FontCandidateValidation
    {
        public FontCandidateValidation(string assetPath)
        {
            AssetPath = assetPath;
            MissingCharacters = string.Empty;
            FontName = "<not loaded>";
        }

        public string AssetPath { get; }
        public Font SourceFont { get; set; }
        public string FontName { get; set; }
        public long FileSizeBytes { get; set; }
        public List<string> ExistingUses { get; set; }
        public bool TempAssetCreated { get; set; }
        public bool TryAddResult { get; set; }
        public string MissingCharacters { get; set; }
        public bool AllMandatoryCharactersPresent { get; set; }
        public bool HasRequiredStructure { get; set; }
        public string FailureReason { get; set; }

        public bool IsTryAddAccepted
        {
            get
            {
                return TryAddResult || string.IsNullOrEmpty(MissingCharacters);
            }
        }

        public bool IsQualified
        {
            get
            {
                return SourceFont != null && IsTryAddAccepted &&
                       AllMandatoryCharactersPresent && HasRequiredStructure &&
                       string.IsNullOrEmpty(FailureReason);
            }
        }

        public string FormatForLog()
        {
            return "PATCH//BREAK Korean font candidate\n" +
                   $"path={AssetPath}\n" +
                   $"fontName={FontName}\n" +
                   $"fileSizeBytes={FileSizeBytes}\n" +
                   $"existingUses={string.Join(",", ExistingUses ?? new List<string>())}\n" +
                   $"tmpAssetCreated={TempAssetCreated}\n" +
                   $"tmpAssetStructureValid={HasRequiredStructure}\n" +
                   $"mandatoryTryAddResult={TryAddResult}\n" +
                   $"mandatoryMissingCharacters={MissingCharacters}\n" +
                   $"mandatoryAllHasCharacter={AllMandatoryCharactersPresent}\n" +
                   $"qualified={IsQualified}\n" +
                   $"failureReason={FailureReason ?? "<none>"}";
        }
    }

    private sealed class CharacterCollection
    {
        public readonly SortedSet<char> MandatoryKorean = new SortedSet<char>();
        public readonly SortedSet<char> OptionalUiCharacters =
            new SortedSet<char>();
        public readonly HashSet<string> DisplayStrings = new HashSet<string>();

        public void Add(string value)
        {
            if (string.IsNullOrEmpty(value) || !DisplayStrings.Add(value))
            {
                return;
            }

            foreach (char character in value)
            {
                if (character == '\u25A1' || character == '\r' ||
                    (char.IsControl(character) && character != '\n'))
                {
                    continue;
                }

                if (IsKoreanCharacter(character))
                {
                    MandatoryKorean.Add(character);
                }
                else
                {
                    OptionalUiCharacters.Add(character);
                }
            }
        }
    }

    private readonly struct StringLiteral
    {
        public StringLiteral(string value, bool isDiagnostic)
        {
            Value = value;
            IsDiagnostic = isDiagnostic;
        }

        public string Value { get; }
        public bool IsDiagnostic { get; }
    }
}
#endif

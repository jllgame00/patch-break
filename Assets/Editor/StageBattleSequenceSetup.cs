using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StageBattleSequenceSetup
{
    private const string MenuRoot = "Tools/PATCH BREAK/Stage Sequence/";

    private static readonly SceneSpec[] SceneSpecs =
    {
        new SceneSpec(
            "Assets/Scenes/Battle.unity",
            "Battle",
            typeof(GolemController)
        ),
        new SceneSpec(
            "Assets/Scenes/KnightBattle.unity",
            "KnightBattle",
            typeof(KnightController)
        ),
        new SceneSpec(
            "Assets/Scenes/DebuggerBattle.unity",
            "DebuggerBattle",
            typeof(DebuggerController)
        )
    };

    [MenuItem(MenuRoot + "Setup Battle First")]
    public static void SetupBattleFirst()
    {
        SetupScenes(new[] { SceneSpecs[0] });
    }

    [MenuItem(MenuRoot + "Setup All Scenes")]
    public static void SetupAllScenes()
    {
        SetupScenes(SceneSpecs);
    }

    [MenuItem(MenuRoot + "Validate All Scenes")]
    public static void ValidateAllScenes()
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        foreach (SceneSpec spec in SceneSpecs)
        {
            Scene scene = EditorSceneManager.OpenScene(
                spec.Path,
                OpenSceneMode.Single
            );

            ValidateScene(scene, spec, true);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);

            Scene reopenedScene = EditorSceneManager.OpenScene(
                spec.Path,
                OpenSceneMode.Single
            );
            ValidateScene(reopenedScene, spec, true);
        }

        Debug.Log("Stage battle sequence validation completed.");
    }

    private static void SetupScenes(IEnumerable<SceneSpec> specs)
    {
        if (!PrepareEditorOperation())
        {
            return;
        }

        foreach (SceneSpec spec in specs)
        {
            Scene scene = EditorSceneManager.OpenScene(
                spec.Path,
                OpenSceneMode.Single
            );
            SetupScene(scene, spec);
            ValidateScene(scene, spec, true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);

            Scene reopenedScene = EditorSceneManager.OpenScene(
                spec.Path,
                OpenSceneMode.Single
            );
            ValidateScene(reopenedScene, spec, true);
        }

        Debug.Log("Stage battle sequence setup completed.");
    }

    private static bool PrepareEditorOperation()
    {
        if (Application.isPlaying)
        {
            Debug.LogError(
                "Stage sequence setup cannot run while Play Mode is active."
            );
            return false;
        }

        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    private static void SetupScene(Scene scene, SceneSpec spec)
    {
        ThrowIfMissingScripts(scene, spec);

        HeroController heroController = FindSingleComponent<HeroController>(
            scene,
            spec
        );
        Component enemyAiController = FindSingleComponent(
            scene,
            spec,
            spec.EnemyAiType
        );
        BattleBriefingController briefingController =
            FindSingleComponent<BattleBriefingController>(scene, spec);
        BattleManager battleManager = FindSingleComponent<BattleManager>(
            scene,
            spec
        );
        RuntimeConsoleUI runtimeConsoleUI =
            FindSingleComponent<RuntimeConsoleUI>(scene, spec);
        ProgramRuntime programRuntime = FindSingleComponent<ProgramRuntime>(
            scene,
            spec
        );

        GameObject sequenceRoot = GetOrCreateSequenceRoot(scene, spec);
        StageBattleSequenceController sequenceController =
            sequenceRoot.GetComponent<StageBattleSequenceController>();

        if (sequenceController == null)
        {
            sequenceController = Undo.AddComponent<StageBattleSequenceController>(
                sequenceRoot
            );
        }

        Transform pointsRoot = GetOrCreateDirectChild(
            sequenceRoot.transform,
            "StageSequencePoints"
        );
        float heroY = heroController.transform.position.y;
        float enemyY = enemyAiController.transform.position.y;
        float heroZ = heroController.transform.position.z;
        float enemyZ = enemyAiController.transform.position.z;

        Transform heroEntranceStart = GetOrCreateMarker(
            pointsRoot,
            "HeroEntranceStart",
            new Vector3(-10f, heroY, heroZ)
        );
        Transform heroBattlePosition = GetOrCreateMarker(
            pointsRoot,
            "HeroBattlePosition",
            heroController.transform.position
        );
        Transform heroExitPoint = GetOrCreateMarker(
            pointsRoot,
            "HeroExitPoint",
            new Vector3(10f, heroY, heroZ)
        );
        Transform enemyEntranceStart = GetOrCreateMarker(
            pointsRoot,
            "EnemyEntranceStart",
            new Vector3(10f, enemyY, enemyZ)
        );
        Transform enemyBattlePosition = GetOrCreateMarker(
            pointsRoot,
            "EnemyBattlePosition",
            enemyAiController.transform.position
        );

        SerializedObject sequenceProperties = new SerializedObject(
            sequenceController
        );
        SetReference(sequenceProperties, "hero", heroController.transform);
        SetReference(
            sequenceProperties,
            "enemy",
            enemyAiController.transform
        );
        SetReference(
            sequenceProperties,
            "enemyAiController",
            enemyAiController
        );
        SetReference(
            sequenceProperties,
            "briefingController",
            briefingController
        );
        SetReference(sequenceProperties, "battleManager", battleManager);
        SetReference(sequenceProperties, "runtimeConsoleUI", runtimeConsoleUI);
        SetReference(sequenceProperties, "programRuntime", programRuntime);
        SetReference(
            sequenceProperties,
            "heroEntranceStart",
            heroEntranceStart
        );
        SetReference(
            sequenceProperties,
            "heroBattlePosition",
            heroBattlePosition
        );
        SetReference(sequenceProperties, "heroExitPoint", heroExitPoint);
        SetReference(
            sequenceProperties,
            "enemyEntranceStart",
            enemyEntranceStart
        );
        SetReference(
            sequenceProperties,
            "enemyBattlePosition",
            enemyBattlePosition
        );
        sequenceProperties.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject briefingProperties = new SerializedObject(
            briefingController
        );
        SerializedProperty beginOnAwake = briefingProperties.FindProperty(
            "beginBriefingOnAwake"
        );

        if (beginOnAwake == null)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: BattleBriefingController does not expose " +
                "beginBriefingOnAwake."
            );
        }

        beginOnAwake.boolValue = false;
        briefingProperties.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject GetOrCreateSequenceRoot(
        Scene scene,
        SceneSpec spec
    )
    {
        StageBattleSequenceController[] existingControllers =
            FindComponents<StageBattleSequenceController>(scene).ToArray();

        if (existingControllers.Length > 1)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: expected at most one " +
                "StageBattleSequenceController, found " +
                $"{existingControllers.Length}."
            );
        }

        if (existingControllers.Length == 1)
        {
            return existingControllers[0].gameObject;
        }

        GameObject namedRoot = scene.GetRootGameObjects().FirstOrDefault(
            root => root.name == "StageBattleSequence"
        );

        if (namedRoot != null)
        {
            return namedRoot;
        }

        GameObject sequenceRoot = new GameObject("StageBattleSequence");
        Undo.RegisterCreatedObjectUndo(
            sequenceRoot,
            "Create Stage Battle Sequence"
        );
        SceneManager.MoveGameObjectToScene(sequenceRoot, scene);
        return sequenceRoot;
    }

    private static Transform GetOrCreateDirectChild(
        Transform parent,
        string name
    )
    {
        Transform child = parent.Find(name);

        if (child != null && child.parent == parent)
        {
            return child;
        }

        GameObject childObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {name}");
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Transform GetOrCreateMarker(
        Transform parent,
        string name,
        Vector3 position
    )
    {
        Transform marker = GetOrCreateDirectChild(parent, name);
        marker.position = position;
        return marker;
    }

    private static void SetReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                $"Missing serialized property: {propertyName}."
            );
        }

        property.objectReferenceValue = value;
    }

    private static void ValidateScene(
        Scene scene,
        SceneSpec spec,
        bool logErrors
    )
    {
        List<string> errors = new List<string>();
        CollectMissingScriptErrors(scene, errors);

        StageBattleSequenceController[] controllers =
            FindComponents<StageBattleSequenceController>(scene).ToArray();
        if (controllers.Length != 1)
        {
            errors.Add(
                $"expected one StageBattleSequenceController, found " +
                $"{controllers.Length}"
            );
        }
        else
        {
            ValidateSequenceReferences(controllers[0], errors);
            ValidateMarkerHierarchy(controllers[0], errors);
        }

        BattleBriefingController[] briefings =
            FindComponents<BattleBriefingController>(scene).ToArray();
        if (briefings.Length != 1)
        {
            errors.Add(
                $"expected one BattleBriefingController, found {briefings.Length}"
            );
        }
        else
        {
            ValidateBriefing(briefings[0], errors);
        }

        if (errors.Count > 0)
        {
            string message = $"{spec.Name} validation failed:\n- " +
                             string.Join("\n- ", errors);
            if (logErrors)
            {
                Debug.LogError(message);
            }

            throw new InvalidOperationException(message);
        }

        Debug.Log($"{spec.Name}: Stage sequence serialization is valid.");
    }

    private static void ValidateSequenceReferences(
        StageBattleSequenceController controller,
        ICollection<string> errors
    )
    {
        SerializedObject properties = new SerializedObject(controller);
        string[] requiredReferences =
        {
            "hero",
            "enemy",
            "enemyAiController",
            "briefingController",
            "battleManager",
            "runtimeConsoleUI",
            "programRuntime",
            "heroEntranceStart",
            "heroBattlePosition",
            "heroExitPoint",
            "enemyEntranceStart",
            "enemyBattlePosition"
        };

        foreach (string propertyName in requiredReferences)
        {
            SerializedProperty property = properties.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                errors.Add($"missing sequence reference: {propertyName}");
            }
        }
    }

    private static void ValidateMarkerHierarchy(
        StageBattleSequenceController controller,
        ICollection<string> errors
    )
    {
        Transform sequenceRoot = controller.transform;
        Transform pointsRoot = sequenceRoot.Find("StageSequencePoints");
        if (pointsRoot == null)
        {
            errors.Add("missing StageSequencePoints child");
            return;
        }

        string[] names =
        {
            "HeroEntranceStart",
            "HeroBattlePosition",
            "HeroExitPoint",
            "EnemyEntranceStart",
            "EnemyBattlePosition"
        };

        foreach (string markerName in names)
        {
            if (pointsRoot.Find(markerName) == null)
            {
                errors.Add($"missing marker: {markerName}");
            }
        }
    }

    private static void ValidateBriefing(
        BattleBriefingController briefing,
        ICollection<string> errors
    )
    {
        SerializedObject properties = new SerializedObject(briefing);
        SerializedProperty bubbleEnabled = properties.FindProperty(
            "useBubbleBriefing"
        );
        SerializedProperty koreanFont = properties.FindProperty(
            "koreanFontAsset"
        );
        SerializedProperty briefingRoot = properties.FindProperty(
            "briefingRoot"
        );

        if (bubbleEnabled == null || !bubbleEnabled.boolValue)
        {
            errors.Add("useBubbleBriefing is disabled");
        }

        if (koreanFont == null || koreanFont.objectReferenceValue == null)
        {
            errors.Add("missing Korean TMP font reference");
        }

        GameObject oldBriefingRoot = briefingRoot == null
            ? null
            : briefingRoot.objectReferenceValue as GameObject;
        if (oldBriefingRoot == null || oldBriefingRoot.activeSelf)
        {
            errors.Add("legacy BattleBriefingRoot is not disabled");
        }
    }

    private static void ThrowIfMissingScripts(Scene scene, SceneSpec spec)
    {
        List<string> errors = new List<string>();
        CollectMissingScriptErrors(scene, errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: {string.Join(", ", errors)}"
            );
        }
    }

    private static void CollectMissingScriptErrors(
        Scene scene,
        ICollection<string> errors
    )
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(
                         true
                     ))
            {
                int missingCount =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject
                    );
                if (missingCount > 0)
                {
                    errors.Add(
                        $"{transform.name} has {missingCount} missing script(s)"
                    );
                }
            }
        }
    }

    private static T FindSingleComponent<T>(Scene scene, SceneSpec spec)
        where T : Component
    {
        T[] components = FindComponents<T>(scene).ToArray();
        if (components.Length != 1)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: expected one {typeof(T).Name}, found " +
                $"{components.Length}."
            );
        }

        return components[0];
    }

    private static Component FindSingleComponent(
        Scene scene,
        SceneSpec spec,
        Type componentType
    )
    {
        Component[] components = FindComponents(scene, componentType).ToArray();
        if (components.Length != 1)
        {
            throw new InvalidOperationException(
                $"{spec.Name}: expected one {componentType.Name}, found " +
                $"{components.Length}."
            );
        }

        return components[0];
    }

    private static IEnumerable<T> FindComponents<T>(Scene scene)
        where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(
            root => root.GetComponentsInChildren<T>(true)
        );
    }

    private static IEnumerable<Component> FindComponents(
        Scene scene,
        Type componentType
    )
    {
        return scene.GetRootGameObjects().SelectMany(
            root => root.GetComponentsInChildren(componentType, true)
        );
    }

    private readonly struct SceneSpec
    {
        public SceneSpec(string path, string name, Type enemyAiType)
        {
            Path = path;
            Name = name;
            EnemyAiType = enemyAiType;
        }

        public string Path { get; }
        public string Name { get; }
        public Type EnemyAiType { get; }
    }
}

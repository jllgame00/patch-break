using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WorldHealthBarSetup
{
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string PrefabPath =
        PrefabFolder + "/WorldHealthBar.prefab";
    private static readonly string[] BattleScenePaths =
    {
        "Assets/Scenes/Battle.unity",
        "Assets/Scenes/KnightBattle.unity",
        "Assets/Scenes/DebuggerBattle.unity"
    };

    private static readonly Color HeroColor =
        new Color32(0, 216, 224, 255);
    private static readonly Color EnemyColor =
        new Color32(232, 70, 70, 255);
    private static readonly Color BackgroundColor =
        new Color32(12, 18, 22, 225);

    [MenuItem("Tools/PATCH BREAK/Setup World Health Bars")]
    public static void SetupWorldHealthBars()
    {
        if (HasDirtyOpenScene())
        {
            Debug.LogError(
                "World health bar setup stopped because an open " +
                "scene has unsaved changes. Save or revert it, " +
                "then run the setup menu again."
            );
            return;
        }

        SceneSetup[] originalSetup =
            EditorSceneManager.GetSceneManagerSetup();

        try
        {
            GameObject prefab = CreateHealthBarPrefab();

            foreach (string scenePath in BattleScenePaths)
            {
                SetupScene(scenePath, prefab);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("WORLD_HEALTH_BAR_SETUP_COMPLETE");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(
                    originalSetup
                );
            }
        }
    }

    private static bool HasDirtyOpenScene()
    {
        for (int i = 0;
             i < EditorSceneManager.sceneCount;
             i++)
        {
            if (EditorSceneManager.GetSceneAt(i).isDirty)
                return true;
        }

        return false;
    }

    private static GameObject CreateHealthBarPrefab()
    {
        EnsureFolder("Assets/Prefabs", "UI");

        GameObject root = new(
            "WorldHealthBar",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(WorldHealthBarUI)
        );

        try
        {
            root.layer = LayerMask.NameToLayer("UI");

            RectTransform rootRect =
                root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(96f, 10f);

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;

            Image background = CreateImage(
                "Background",
                root.transform,
                BackgroundColor
            );
            Stretch(background.rectTransform, Vector2.zero);

            Image fill = CreateImage(
                "Fill",
                root.transform,
                HeroColor
            );
            Stretch(fill.rectTransform, new Vector2(2f, 2f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            WorldHealthBarUI controller =
                root.GetComponent<WorldHealthBarUI>();
            controller.Configure(
                null,
                null,
                new Vector3(0f, 0.85f, 0f),
                fill,
                HeroColor,
                null,
                null,
                group,
                null
            );

            return PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabPath
            );
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color)
    {
        GameObject imageObject = new(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        imageObject.layer = LayerMask.NameToLayer("UI");
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd"
        );
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = inset;
        rect.offsetMax = -inset;
    }

    private static void SetupScene(
        string scenePath,
        GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(
            scenePath,
            OpenSceneMode.Single
        );

        BattleManager manager =
            FindComponentInScene<BattleManager>(scene);
        Canvas canvas = FindBattleCanvas(scene);
        Camera worldCamera = FindMainCamera(scene);

        if (manager == null ||
            canvas == null ||
            worldCamera == null)
        {
            throw new InvalidOperationException(
                $"{scenePath}: Required battle references " +
                "could not be found."
            );
        }

        SerializedObject managerData =
            new SerializedObject(manager);
        Health heroHealth =
            managerData.FindProperty("heroHealth")
                .objectReferenceValue as Health;
        Health enemyHealth =
            managerData.FindProperty("enemyHealth")
                .objectReferenceValue as Health;
        GameObject resultPanel =
            managerData.FindProperty("resultPanel")
                .objectReferenceValue as GameObject;

        if (heroHealth == null || enemyHealth == null)
        {
            throw new InvalidOperationException(
                $"{scenePath}: BattleManager health " +
                "references are incomplete."
            );
        }

        Transform existingRoot =
            canvas.transform.Find("WorldHealthBars");
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot.gameObject);
        }

        GameObject worldBars = new(
            "WorldHealthBars",
            typeof(RectTransform)
        );
        worldBars.layer = LayerMask.NameToLayer("UI");
        worldBars.transform.SetParent(canvas.transform, false);

        RectTransform worldBarsRect =
            worldBars.GetComponent<RectTransform>();
        Stretch(worldBarsRect, Vector2.zero);
        worldBarsRect.SetAsLastSibling();

        CreateSceneBar(
            prefab,
            scene,
            worldBarsRect,
            "HeroWorldHealthBar",
            heroHealth,
            HeroColor,
            worldCamera,
            canvas,
            resultPanel
        );
        CreateSceneBar(
            prefab,
            scene,
            worldBarsRect,
            "EnemyWorldHealthBar",
            enemyHealth,
            EnemyColor,
            worldCamera,
            canvas,
            resultPanel
        );

        DisableFixedHealthBar(canvas, "HeroHealthBar");
        DisableFixedHealthBar(canvas, "EnemyHealthBar");

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException(
                $"{scenePath}: Failed to save the configured scene."
            );
        }
    }

    private static void CreateSceneBar(
        GameObject prefab,
        Scene scene,
        RectTransform parent,
        string objectName,
        Health health,
        Color color,
        Camera worldCamera,
        Canvas canvas,
        GameObject resultPanel)
    {
        GameObject instance =
            PrefabUtility.InstantiatePrefab(prefab, scene)
                as GameObject;

        if (instance == null)
        {
            throw new InvalidOperationException(
                "Failed to instantiate WorldHealthBar prefab."
            );
        }

        instance.name = objectName;
        instance.transform.SetParent(parent, false);

        WorldHealthBarUI controller =
            instance.GetComponent<WorldHealthBarUI>();
        Image fill =
            instance.transform.Find("Fill")?.GetComponent<Image>();
        CanvasGroup group = instance.GetComponent<CanvasGroup>();

        if (controller == null || fill == null || group == null)
        {
            throw new InvalidOperationException(
                "WorldHealthBar prefab structure is invalid."
            );
        }

        float offsetY = GetHeadOffset(health);
        controller.Configure(
            health.transform,
            health,
            new Vector3(0f, offsetY, 0f),
            fill,
            color,
            worldCamera,
            canvas,
            group,
            resultPanel
        );

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(fill);
        EditorUtility.SetDirty(group);
    }

    private static float GetHeadOffset(Health health)
    {
        SpriteRenderer renderer =
            health.GetComponent<SpriteRenderer>();

        return renderer != null
            ? renderer.bounds.extents.y + 0.35f
            : 0.85f;
    }

    private static void DisableFixedHealthBar(
        Canvas canvas,
        string objectName)
    {
        for (int i = 0;
             i < canvas.transform.childCount;
             i++)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child.name != objectName ||
                child.GetComponent<HealthBarUI>() == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            EditorUtility.SetDirty(child.gameObject);
            return;
        }

        throw new InvalidOperationException(
            $"Fixed health bar '{objectName}' was not found."
        );
    }

    private static T FindComponentInScene<T>(Scene scene)
        where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static Canvas FindBattleCanvas(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Canvas[] canvases =
                root.GetComponentsInChildren<Canvas>(true);

            foreach (Canvas canvas in canvases)
            {
                if (canvas.isRootCanvas &&
                    (canvas.renderMode ==
                         RenderMode.ScreenSpaceOverlay ||
                     canvas.renderMode ==
                         RenderMode.ScreenSpaceCamera))
                {
                    return canvas;
                }
            }
        }

        return null;
    }

    private static Camera FindMainCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera[] cameras =
                root.GetComponentsInChildren<Camera>(true);

            foreach (Camera camera in cameras)
            {
                if (camera.CompareTag("MainCamera"))
                    return camera;
            }
        }

        return null;
    }

    private static void EnsureFolder(
        string parent,
        string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}

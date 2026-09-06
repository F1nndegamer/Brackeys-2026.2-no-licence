using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class LayeredMonkeyArtImporter
{
    private const int FrameCount = 68;
    private const int Columns = 10;
    private const int CellSize = 192;
    private const string ActorPrefabPath = "Assets/brackeys_2026_GJ/prefabs/NPC/Actor.prefab";

    private readonly struct Sheet
    {
        public Sheet(string path, string spritePrefix, string prefabProperty)
        {
            Path = path;
            SpritePrefix = spritePrefix;
            PrefabProperty = prefabProperty;
        }

        public string Path { get; }
        public string SpritePrefix { get; }
        public string PrefabProperty { get; }
    }

    private static readonly Sheet[] Sheets =
    {
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_body.png",
            "monkey_body",
            "bodyFrames"
        ),
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_face.png",
            "monkey_face",
            "faceFrames"
        ),
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_hair.png",
            "monkey_hair",
            "hairFrames"
        ),
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_hair2.png",
            "monkey_hair2",
            "hair2Frames"
        ),
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_hair3.png",
            "monkey_hair3",
            "hair3Frames"
        ),
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_bowtie.png",
            "monkey_bowtie",
            "bowtieFrames"
        ),
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_hat_behind.png",
            "monkey_hat_behind",
            "hatBehindFrames"
        ),
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_sunglasses.png",
            "monkey_sunglasses",
            "sunglassesFrames"
        ),
        new Sheet(
            "Assets/brackeys_2026_GJ/art/monkeys/Layered/monkey_shine.png",
            "monkey_shine",
            "shineFrames"
        )
    };

    private static bool refreshQueued;

    [InitializeOnLoadMethod]
    private static void QueueRefreshIfNeeded()
    {
        if (refreshQueued)
            return;

        refreshQueued = true;
        EditorApplication.delayCall += RefreshIfNeeded;
    }

    [MenuItem("Tools/Daycare/Refresh Layered Monkey Art")]
    public static void RefreshAll()
    {
        AssetDatabase.Refresh();

        foreach (Sheet sheet in Sheets)
            ConfigureSheet(sheet);

        WireActorPrefab();
        AssetDatabase.SaveAssets();
    }

    private static void RefreshIfNeeded()
    {
        refreshQueued = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueRefreshIfNeeded();
            return;
        }

        bool needsRefresh = Sheets.Any(sheet =>
            AssetDatabase.LoadAllAssetRepresentationsAtPath(sheet.Path)
                .OfType<Sprite>()
                .Count() != FrameCount
        );

        if (needsRefresh)
            RefreshAll();
        else if (ActorPrefabNeedsRefresh())
        {
            WireActorPrefab();
            AssetDatabase.SaveAssets();
        }
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        QueueRefreshIfNeeded();
    }

    private static void ConfigureSheet(Sheet sheet)
    {
        TextureImporter importer = AssetImporter.GetAtPath(sheet.Path) as TextureImporter;

        if (importer == null)
        {
            Debug.LogError($"Missing layered monkey sheet: {sheet.Path}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = CellSize;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Dictionary<string, SpriteRect> existing = provider.GetSpriteRects()
            .ToDictionary(rect => rect.name, rect => rect);
        SpriteRect[] rects = new SpriteRect[FrameCount];

        for (int index = 0; index < FrameCount; index++)
        {
            string spriteName = $"{sheet.SpritePrefix}_{index}";
            int row = index / Columns;
            int column = index % Columns;
            SpriteRect rect = existing.TryGetValue(spriteName, out SpriteRect previous)
                ? previous
                : new SpriteRect { spriteID = GUID.Generate() };

            rect.name = spriteName;
            rect.rect = new Rect(
                column * CellSize,
                2048 - (row + 1) * CellSize,
                CellSize,
                CellSize
            );
            rect.alignment = SpriteAlignment.Center;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.border = Vector4.zero;
            rects[index] = rect;
        }

        provider.SetSpriteRects(rects);
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static void WireActorPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPrefabPath);

        if (prefab == null)
        {
            Debug.LogError($"Missing monkey actor prefab: {ActorPrefabPath}");
            return;
        }

        MonkeySpriteAnimator animator = prefab.GetComponent<MonkeySpriteAnimator>();

        if (animator == null)
        {
            Debug.LogError("Actor prefab has no MonkeySpriteAnimator component.");
            return;
        }

        SerializedObject serializedAnimator = new SerializedObject(animator);

        foreach (Sheet sheet in Sheets)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheet.Path)
                .OfType<Sprite>()
                .OrderBy(sprite => ParseFrameIndex(sprite.name))
                .ToArray();

            if (sprites.Length != FrameCount)
            {
                Debug.LogError(
                    $"Expected {FrameCount} frames in {sheet.Path}, found {sprites.Length}."
                );
                continue;
            }

            SerializedProperty frames =
                serializedAnimator.FindProperty(sheet.PrefabProperty);

            if (frames == null)
            {
                Debug.LogError(
                    $"MonkeySpriteAnimator is missing {sheet.PrefabProperty}."
                );
                continue;
            }

            frames.arraySize = sprites.Length;

            for (int index = 0; index < sprites.Length; index++)
                frames.GetArrayElementAtIndex(index).objectReferenceValue = sprites[index];
        }

        serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
    }

    private static bool ActorPrefabNeedsRefresh()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPrefabPath);
        MonkeySpriteAnimator animator = prefab != null
            ? prefab.GetComponent<MonkeySpriteAnimator>()
            : null;

        if (animator == null)
            return true;

        SerializedObject serializedAnimator = new SerializedObject(animator);

        foreach (Sheet sheet in Sheets)
        {
            SerializedProperty frames =
                serializedAnimator.FindProperty(sheet.PrefabProperty);

            if (frames == null || frames.arraySize != FrameCount)
                return true;
        }

        return false;
    }

    private static int ParseFrameIndex(string spriteName)
    {
        int separator = spriteName.LastIndexOf('_');
        return separator >= 0 && int.TryParse(spriteName[(separator + 1)..], out int index)
            ? index
            : int.MaxValue;
    }
}

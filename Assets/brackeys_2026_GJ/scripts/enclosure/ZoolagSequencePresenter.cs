using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ZoolagSequencePresenter : MonoBehaviour
{
    private const string ResourcePath = "ZoolagSequence";

    private enum MonkeySequenceVariant
    {
        Normal,
        WaterMiss,
        SpaceLaunch
    }

    [Header("Layers (bottom to top)")]
    [SerializeField] private Image background;
    [SerializeField] private Image monkey;
    [SerializeField] private Image cannon;
    [SerializeField] private Image nearIsland;
    [SerializeField] private Image distantIsland;
    [SerializeField] private Image waterRipples;

    [Header("Monkey variation chances")]
    [SerializeField, Range(0f, 100f)] private float normalChance = 80f;
    [SerializeField, Range(0f, 100f)] private float waterMissChance = 10f;
    [SerializeField, Range(0f, 100f)] private float spaceLaunchChance = 10f;
    [SerializeField, Min(0)] private int normalEndingStartFrame = 25;
    [SerializeField, Min(0)] private int waterMissEndingStartFrame = 25;
    [SerializeField, Min(0)] private int spaceLaunchEndingStartFrame = 22;

    [Header("Playback")]
    [SerializeField, Min(0.01f)] private float secondsPerFrame = 0.12f;
    [SerializeField, Min(1)] private int totalPlaybackFrames = 46;
    [SerializeField, Min(1)] private int tailLoopFrameCount = 4;
    [SerializeField] private AudioSource scoreSource;
    [SerializeField] private AudioClip score;
    [SerializeField] private AudioClip waterMissScore;
    [SerializeField] private AudioClip spaceLaunchScore;

    private Sprite[] normalMonkeyFrames = Array.Empty<Sprite>();
    private Sprite[] normalMonkeyEndingFrames = Array.Empty<Sprite>();
    private Sprite[] waterMissMonkeyFrames = Array.Empty<Sprite>();
    private Sprite[] waterMissMonkeyEndingFrames = Array.Empty<Sprite>();
    private Sprite[] spaceLaunchMonkeyFrames = Array.Empty<Sprite>();
    private Sprite[] spaceLaunchMonkeyEndingFrames = Array.Empty<Sprite>();
    private Sprite[] selectedMonkeyFrames = Array.Empty<Sprite>();
    private Sprite[] selectedMonkeyEndingFrames = Array.Empty<Sprite>();
    private Sprite[] cannonFrames = Array.Empty<Sprite>();
    private Sprite[] rippleFrames = Array.Empty<Sprite>();
    private MonkeySequenceVariant selectedVariant;
    private Color selectedMonkeyColor = Color.white;
    private int selectedEndingStartFrame;
    private int frameCount;

    public float Duration => Mathf.Max(1, totalPlaybackFrames) *
        Mathf.Max(0.01f, secondsPerFrame);

    private void Awake()
    {
        if (scoreSource == null)
            scoreSource = GetComponent<AudioSource>();

        if (scoreSource != null)
            scoreSource.playOnAwake = false;

        LoadArtwork();
    }

    public void Begin(Color monkeyColor)
    {
        if (normalMonkeyFrames.Length == 0)
            LoadArtwork();

        SelectMonkeyVariation(PickMonkeyVariation());
        selectedMonkeyColor = monkeyColor;

        ShowFrame(0, 0);

        AudioClip selectedScore = GetSelectedScore();
        if (scoreSource != null && selectedScore != null)
        {
            scoreSource.playOnAwake = false;
            scoreSource.enabled = true;
            scoreSource.Stop();
            scoreSource.clip = selectedScore;
            scoreSource.loop = false;

            if (scoreSource.gameObject.activeInHierarchy)
                scoreSource.Play();
        }
        else if (scoreSource != null)
            scoreSource.Stop();
    }

    public void ShowAtTime(float elapsedSeconds)
    {
        float safeFrameDuration = Mathf.Max(0.01f, secondsPerFrame);
        int absoluteFrame = Mathf.FloorToInt(Mathf.Max(0f, elapsedSeconds) / safeFrameDuration);
        int index = GetFrameIndex(absoluteFrame);
        ShowFrame(index, absoluteFrame);
    }

    public void Stop()
    {
        if (scoreSource != null)
            scoreSource.Stop();
    }

    private void LoadArtwork()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>(ResourcePath);
        normalMonkeyFrames = FindSequence(sprites, "zoolagMonkey");
        normalMonkeyEndingFrames = FindSequence(sprites, "zoolagMonkey_end");
        waterMissMonkeyFrames = FindSequence(sprites, "zoolagMonkeyWater");
        waterMissMonkeyEndingFrames = FindSequence(sprites, "zoolagMonkeyWater_end");
        spaceLaunchMonkeyFrames = FindSequence(sprites, "zoolagMonkeySky");
        spaceLaunchMonkeyEndingFrames = FindSequence(sprites, "zoolagMonkeySky_end");
        cannonFrames = FindSequence(sprites, "zoolagCanon");
        rippleFrames = FindSequence(sprites, "zoolag_island");
        SelectMonkeyVariation(MonkeySequenceVariant.Normal);

        ApplyStatic(background, FindSprite(sprites, "water"));
        ApplyStatic(nearIsland, FindSprite(sprites, "island1"));
        ApplyStatic(distantIsland, FindSprite(sprites, "island2"));
    }

    private void ShowFrame(int index, int absoluteFrame)
    {
        if (absoluteFrame < frameCount)
        {
            ApplyMonkeyTint(absoluteFrame);
            Sprite[] frames = index >= selectedEndingStartFrame &&
                selectedMonkeyEndingFrames.Length > 0
                ? selectedMonkeyEndingFrames
                : selectedMonkeyFrames;
            ApplyFrame(monkey, frames, index, false);
        }
        else if (monkey != null)
            monkey.enabled = false;

        ApplyFrame(cannon, cannonFrames, index, false);
        ApplyFrame(waterRipples, rippleFrames, absoluteFrame, true);
    }

    private int GetFrameIndex(int absoluteFrame)
    {
        if (frameCount <= 1)
            return 0;

        if (absoluteFrame < frameCount)
            return absoluteFrame;

        int loopCount = Mathf.Clamp(tailLoopFrameCount, 1, frameCount);
        int loopStart = frameCount - loopCount;
        return loopStart + (absoluteFrame - frameCount) % loopCount;
    }

    private static Sprite[] FindSequence(Sprite[] sprites, string prefix)
    {
        return sprites
            .Where(sprite => sprite != null && IsSequenceFrame(sprite.name, prefix))
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsSequenceFrame(string spriteName, string prefix)
    {
        return spriteName.StartsWith(prefix, StringComparison.Ordinal) &&
            spriteName.Length > prefix.Length &&
            char.IsDigit(spriteName[prefix.Length]);
    }

    private MonkeySequenceVariant PickMonkeyVariation()
    {
        float normalWeight = Mathf.Max(0f, normalChance);
        float waterWeight = Mathf.Max(0f, waterMissChance);
        float spaceWeight = Mathf.Max(0f, spaceLaunchChance);
        float totalWeight = normalWeight + waterWeight + spaceWeight;

        if (totalWeight <= 0f)
            return MonkeySequenceVariant.Normal;

        float roll = UnityEngine.Random.value * totalWeight;

        if (roll < normalWeight)
            return MonkeySequenceVariant.Normal;

        if (roll < normalWeight + waterWeight)
            return MonkeySequenceVariant.WaterMiss;

        return MonkeySequenceVariant.SpaceLaunch;
    }

    private void SelectMonkeyVariation(MonkeySequenceVariant variant)
    {
        selectedVariant = variant;

        switch (variant)
        {
            case MonkeySequenceVariant.WaterMiss:
                selectedMonkeyFrames = waterMissMonkeyFrames;
                selectedMonkeyEndingFrames = waterMissMonkeyEndingFrames;
                selectedEndingStartFrame = waterMissEndingStartFrame;
                break;
            case MonkeySequenceVariant.SpaceLaunch:
                selectedMonkeyFrames = spaceLaunchMonkeyFrames;
                selectedMonkeyEndingFrames = spaceLaunchMonkeyEndingFrames;
                selectedEndingStartFrame = spaceLaunchEndingStartFrame;
                break;
            default:
                selectedMonkeyFrames = normalMonkeyFrames;
                selectedMonkeyEndingFrames = normalMonkeyEndingFrames;
                selectedEndingStartFrame = normalEndingStartFrame;
                break;
        }

        if (selectedMonkeyFrames.Length == 0 && variant != MonkeySequenceVariant.Normal)
        {
            selectedVariant = MonkeySequenceVariant.Normal;
            selectedMonkeyFrames = normalMonkeyFrames;
            selectedMonkeyEndingFrames = normalMonkeyEndingFrames;
            selectedEndingStartFrame = normalEndingStartFrame;
        }

        frameCount = Mathf.Max(
            cannonFrames.Length,
            Mathf.Max(selectedMonkeyFrames.Length, selectedMonkeyEndingFrames.Length)
        );
    }

    private AudioClip GetSelectedScore()
    {
        switch (selectedVariant)
        {
            case MonkeySequenceVariant.WaterMiss:
                return waterMissScore != null ? waterMissScore : score;
            case MonkeySequenceVariant.SpaceLaunch:
                return spaceLaunchScore != null ? spaceLaunchScore : score;
            default:
                return score;
        }
    }

    private void ApplyMonkeyTint(int absoluteFrame)
    {
        if (monkey == null)
            return;

        bool showingUntintedFx = absoluteFrame >= selectedEndingStartFrame;
        monkey.color = showingUntintedFx ? Color.white : selectedMonkeyColor;
    }

    private static Sprite FindSprite(Sprite[] sprites, string name)
    {
        return sprites.FirstOrDefault(sprite =>
            sprite != null && (string.Equals(sprite.name, name, StringComparison.Ordinal) ||
                string.Equals(sprite.name, name + "_0", StringComparison.Ordinal)));
    }

    private static void ApplyStatic(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.color = Color.white;
        image.enabled = sprite != null;
    }

    private static void ApplyFrame(Image image, Sprite[] frames, int index, bool loop)
    {
        if (image == null)
            return;

        if (frames == null || frames.Length == 0)
        {
            image.enabled = false;
            return;
        }

        int frameIndex = loop
            ? index % frames.Length
            : Mathf.Clamp(index, 0, frames.Length - 1);
        image.sprite = frames[frameIndex];
        image.enabled = true;
    }
}

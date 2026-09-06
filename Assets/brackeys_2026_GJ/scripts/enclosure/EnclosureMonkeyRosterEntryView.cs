using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnclosureMonkeyRosterEntryView : MonoBehaviour
{
    [SerializeField] private Image portraitBehind;
    [SerializeField] private Image portrait;
    [SerializeField] private Image portraitFace;
    [SerializeField] private Image portraitFront;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text activityText;
    [SerializeField] private TMP_Text truthLabel;
    [SerializeField] private Image truthFill;
    [SerializeField] private TMP_Text naughtinessLabel;
    [SerializeField] private Image naughtinessFill;
    [SerializeField] private TMP_Text historyText;
    [SerializeField] private TMP_Text recentText;

    public void Bind(MonkeyActor monkey)
    {
        if (monkey == null || portrait == null)
            return;

        MonkeySpriteAnimator animator = monkey.GetComponent<MonkeySpriteAnimator>();
        SpriteRenderer renderer = monkey.GetComponent<SpriteRenderer>();

        if (animator != null)
        {
            SetPortraitLayer(portraitBehind, animator.PortraitBehindSprite, Color.white);
            SetPortraitLayer(portrait, animator.PortraitSprite, monkey.DisplayColor);
            SetPortraitLayer(portraitFace, animator.PortraitFaceSprite, Color.white);
            SetPortraitLayer(
                portraitFront,
                animator.PortraitFrontAccessorySprite,
                animator.PortraitFrontAccessoryColor
            );
        }
        else
        {
            SetPortraitLayer(portraitBehind, null, Color.white);
            SetPortraitLayer(portrait, renderer != null ? renderer.sprite : null, monkey.DisplayColor);
            SetPortraitLayer(portraitFace, null, Color.white);
            SetPortraitLayer(portraitFront, null, Color.white);
        }

        nameText.text = monkey.DisplayName.ToUpperInvariant();
        activityText.text = FormatActivity(monkey.Activity);
        if (truthLabel != null)
            truthLabel.text = $"TRUTH  <color=#FF9D20>{monkey.Trust:0}</color>";
        if (naughtinessLabel != null)
            naughtinessLabel.text = $"NAUGHTY  <color=#FF9D20>{monkey.Naughtiness:0}</color>";
        SetMeter(truthFill, monkey.Trust);
        SetMeter(naughtinessFill, monkey.Naughtiness);
        historyText.text =
            $"TIME-OUTS  <color=#FF9D20>{monkey.TimeoutCount}</color>\n" +
            $"FIGHTS  <color=#FF9D20>{monkey.FightCount}</color>\n" +
            $"CRIMES  <color=#FF9D20>{monkey.IncidentCount}</color>\n" +
            $"MESSES  <color=#FF9D20>{monkey.MessCount}</color>";
        recentText.text = $"LAST TROUBLE: <color=#FF9D20>{monkey.RecentTrouble.ToUpperInvariant()}</color>";
    }

    private static void SetMeter(Image fill, float percentage)
    {
        if (fill == null)
            return;

        // The authored bars have no sprite, so Image.fillAmount is ignored and they
        // always render as a full quad. Scale the left-pivoted rect instead, the same
        // way EnclosureSelectedMonkeyPresenter.SetBar does.
        fill.enabled = true;
        RectTransform rect = fill.rectTransform;
        Vector3 scale = rect.localScale;
        scale.x = Mathf.Clamp01(percentage / 100f);
        rect.localScale = scale;
    }

    private static void SetPortraitLayer(Image image, Sprite sprite, Color color)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.color = color;
        image.material = null;
        image.enabled = sprite != null;
        image.preserveAspect = true;
    }

    private static string FormatActivity(MonkeyActivity activity)
    {
        return activity.ToString().ToUpperInvariant();
    }
}

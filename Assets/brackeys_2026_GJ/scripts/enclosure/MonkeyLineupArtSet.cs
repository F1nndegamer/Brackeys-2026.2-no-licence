using UnityEngine;

[CreateAssetMenu(menuName = "Jungle Enclosure/Monkey Lineup Art Set")]
public class MonkeyLineupArtSet : ScriptableObject
{
    [Header("Neutral pose")]
    public Sprite idle;

    [Header("Right-facing point poses")]
    public Sprite pointUp;
    public Sprite pointUpRight;
    public Sprite pointRight;
    public Sprite pointDownRight;
    public Sprite pointDown;

    public bool TryResolve(
        MonkeyPointDirection direction,
        out Sprite sprite,
        out bool flipX
    )
    {
        flipX = false;

        switch (direction)
        {
            case MonkeyPointDirection.Up:
                sprite = pointUp;
                break;
            case MonkeyPointDirection.UpRight:
                sprite = pointUpRight;
                break;
            case MonkeyPointDirection.Right:
                sprite = pointRight;
                break;
            case MonkeyPointDirection.DownRight:
                sprite = pointDownRight;
                break;
            case MonkeyPointDirection.Down:
                sprite = pointDown;
                break;
            case MonkeyPointDirection.DownLeft:
                sprite = pointDownRight;
                flipX = true;
                break;
            case MonkeyPointDirection.Left:
                sprite = pointRight;
                flipX = true;
                break;
            case MonkeyPointDirection.UpLeft:
                sprite = pointUpRight;
                flipX = true;
                break;
            default:
                sprite = idle;
                break;
        }

        return sprite != null;
    }
}

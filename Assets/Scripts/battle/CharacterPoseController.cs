using UnityEngine;

/// <summary>
/// Holds the static character-pose sprites used by the stage sequence.
/// This intentionally swaps only the existing root SpriteRenderer.sprite;
/// it does not own animation, facing, color feedback, or gameplay state.
/// </summary>
public sealed class CharacterPoseController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite baseSprite;
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite phaseSprite;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public Sprite BaseSprite => baseSprite;
    public Sprite ReadySprite => readySprite;
    public Sprite PhaseSprite => phaseSprite;

    public void SetBasePose()
    {
        SetSprite(baseSprite);
    }

    public void SetReadyPose()
    {
        // Debugger deliberately has no Ready asset. Keeping its Base pose is
        // the intended combat-ready behavior until an existing phase hook is
        // available to request its Phase pose.
        SetSprite(readySprite != null ? readySprite : baseSprite);
    }

    public void SetPhasePose()
    {
        SetSprite(phaseSprite != null ? phaseSprite : baseSprite);
    }

    private void SetSprite(Sprite sprite)
    {
        if (targetRenderer == null || sprite == null)
        {
            return;
        }

        targetRenderer.sprite = sprite;
    }
}

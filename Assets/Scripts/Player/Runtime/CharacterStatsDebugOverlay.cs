using System.Text;
using UnityEngine;

/// <summary>
/// Shows the character's core stats (HP, ATK, SPD, CRT, HASTE) in the bottom-left corner.
/// Only active in the Editor and development builds.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterStatsDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool visible = true;

    [Tooltip("Pixel font from the project (PixelOperator8-Bold). Sizes that are multiples of 8 stay sharp.")]
    [SerializeField] private Font font;

    [SerializeField, Min(8)] private int fontSize = 16;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // The text is rebuilt a few times per second, not every frame (string formatting allocates).
    private const float RefreshInterval = 0.25f;

    private readonly StringBuilder text = new();
    private GUIContent content;
    private Vector2 contentSize;
    private float nextRefreshTime;

    private CharacterCombatSystem combatStats;
    private PlayerHealth health;
    private GUIStyle style;

    private void Awake()
    {
        combatStats = GetComponentInChildren<CharacterCombatSystem>(true);
        health = GetComponent<PlayerHealth>();
    }

    private void OnGUI()
    {
        if (!visible || combatStats == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8),
                font = font,
            };
            style.normal.textColor = Color.white;
            if (font != null && font.material != null && font.material.mainTexture != null)
            {
                font.material.mainTexture.filterMode = FilterMode.Point;
            }
        }

        style.fontSize = fontSize;

        if (content == null || Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            content ??= new GUIContent();
            content.text = BuildText();
            contentSize = style.CalcSize(content);
        }

        // Bottom-left corner: the top of the screen belongs to the gameplay HUD.
        GUI.Box(new Rect(8f, Screen.height - contentSize.y - 8f, contentSize.x, contentSize.y), content, style);
    }

    private string BuildText()
    {
        text.Clear();
        if (health != null)
        {
            text.AppendLine($"HP     {health.CurrentHealth}/{health.MaxHealth}");
        }

        text.AppendLine($"ATK    {combatStats.AttackPercent:0.#}%");
        text.AppendLine($"SPD    {combatStats.MovementSpeedPercent:0.#}%");
        text.AppendLine($"CRT    {combatStats.CriticalHitPercent:0.#}%");
        text.Append($"HASTE  {combatStats.HastePercent:0.#}%");
        return text.ToString();
    }
#endif
}

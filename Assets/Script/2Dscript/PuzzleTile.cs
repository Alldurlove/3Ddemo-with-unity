using UnityEngine;

/// <summary>
/// 单个拼图格子，仅负责状态与显示。
/// 玩法输入（点行/点列）由 PuzzleManager + PuzzleLineButton 处理。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class PuzzleTile : MonoBehaviour
{
    [Header("外观")]
    [SerializeField] Sprite spriteOn;
    [SerializeField] Sprite spriteOff;

    [Header("目标状态")]
    [SerializeField] bool targetIsOn = true;
    [SerializeField] bool isOn;

    SpriteRenderer _spriteRenderer;

    public int Row { get; private set; }
    public int Col { get; private set; }
    public bool IsCorrect => isOn == targetIsOn;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        ApplySprite();
    }

    public void Configure(int row, int col, bool targetOn, bool startOn)
    {
        Row = row;
        Col = col;
        targetIsOn = targetOn;
        isOn = startOn;
        ApplySprite();
    }

    public void SetSprites(Sprite onSprite, Sprite offSprite)
    {
        spriteOn = onSprite;
        spriteOff = offSprite;
        ApplySprite();
    }

    public void Toggle()
    {
        isOn = !isOn;
        ApplySprite();
    }

    public void SetState(bool on)
    {
        isOn = on;
        ApplySprite();
    }

    void ApplySprite()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
            return;
        _spriteRenderer.sprite = isOn ? spriteOn : spriteOff;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && _spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
        if (!Application.isPlaying && _spriteRenderer != null)
            ApplySprite();
    }
#endif
}

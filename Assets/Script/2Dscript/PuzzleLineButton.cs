using UnityEngine;

public enum PuzzleLineType
{
    Row = 0,
    Column = 1
}

/// <summary>
/// 行/列按钮：点击后通知 PuzzleManager 翻转整行或整列。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class PuzzleLineButton : MonoBehaviour
{
    [SerializeField] PuzzleLineType lineType;
    [SerializeField] int lineIndex;

    public void Configure(PuzzleLineType type, int index)
    {
        lineType = type;
        lineIndex = index;
        name = type == PuzzleLineType.Row ? $"RowButton_{index}" : $"ColumnButton_{index}";
    }

    void OnMouseDown()
    {
        if (PuzzleManager.Instance == null)
            return;
        PuzzleManager.Instance.NotifyLineButtonClicked(lineType, lineIndex);
    }
}

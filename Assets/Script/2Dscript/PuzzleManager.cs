using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 拼图关卡管理：生成网格、接收点击、判赢、加载主场景。
/// 单例便于其它系统（如 UI、存档）访问；多关卡可复制不同场景或使用不同 Manager 预制参数。
/// </summary>
[DisallowMultipleComponent]
public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    [Header("过关")]
    [SerializeField] string winSceneName = "SampleScene";
    [SerializeField] float loadDelaySeconds = 1.2f;
    [SerializeField] bool showWinPrompt = true;
    [SerializeField] string winPromptText = "Puzzle Solved!";
    [Header("Cursor (2D Puzzle)")]
    [Tooltip("解谜场景中强制显示并解锁鼠标，避免被主场景准星脚本锁定")]
    [SerializeField] bool forceUnlockedCursor = true;

    [Header("网格生成")]
    [SerializeField] bool generateGridFromPrefab = true;
    [SerializeField] int rows = 4;
    [SerializeField] int columns = 4;
    [SerializeField] float cellSpacing = 1f;
    [SerializeField] Transform tilesRoot;
    [SerializeField] PuzzleTile puzzleTilePrefab;

    [Header("拼图贴图（切片）")]
    [Tooltip("ON 图切成 rows*columns 份，按左到右、下到上顺序放入")]
    [SerializeField] Sprite[] onSlices;
    [Tooltip("OFF 图切片（可选）。为空时使用 offFallbackSprite 统一贴图")]
    [SerializeField] Sprite[] offSlices;
    [SerializeField] Sprite offFallbackSprite;
    [Tooltip("ON/OFF 切片列表的行顺序是否从“上到下”。Unity 自动 Slice 常见为 true。")]
    [SerializeField] bool slicesRowOrderTopToBottom = true;
    [Tooltip("切片列表每一行内是否从“右到左”读取。默认 false（左到右）。")]
    [SerializeField] bool slicesColumnOrderRightToLeft = false;

    [Header("初始谜面")]
    [SerializeField] bool winStateAllOn = true;
    [Tooltip("从已解状态开始，随机执行多少次“合法点击”来打乱（包含行/列按钮点击）")]
    [SerializeField] int scrambleMinMoves = 10;
    [SerializeField] int scrambleMaxMoves = 20;

    [Header("8个按钮（4行 + 4列）")]
    [SerializeField] bool generateLineButtons = true;
    [SerializeField] Transform buttonsRoot;
    [SerializeField] PuzzleLineButton lineButtonPrefab;
    [SerializeField] float buttonOffset = 1.25f;
    [SerializeField] Sprite rowButtonSprite;
    [SerializeField] Sprite columnButtonSprite;

    [Header("手动模式（可选）")]
    [SerializeField] List<PuzzleTile> manualTiles = new List<PuzzleTile>();

    PuzzleTile[,] _grid;
    bool _solved;
    float _loadTimer = -1f;
    GUIStyle _promptStyle;

    public int Rows => rows;
    public int Columns => columns;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (tilesRoot == null)
            tilesRoot = transform;
        if (buttonsRoot == null)
            buttonsRoot = transform;

        if (generateGridFromPrefab)
            BuildGrid();
        else
            RegisterManualTilesForCheckOnly();

        EnsureCursorUnlocked();
    }

    void Update()
    {
        EnsureCursorUnlocked();

        if (_loadTimer < 0f)
            return;
        _loadTimer -= Time.deltaTime;
        if (_loadTimer <= 0f)
        {
            _loadTimer = -1f;
            SceneManager.LoadScene(winSceneName);
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            EnsureCursorUnlocked();
    }

    void EnsureCursorUnlocked()
    {
        if (!forceUnlockedCursor)
            return;
        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible)
            Cursor.visible = true;
    }
    
    public void NotifyLineButtonClicked(PuzzleLineType lineType, int lineIndex)
    {
        if (_solved || _grid == null)
            return;

        ApplyLineClick(lineType, lineIndex);
    }

    void ApplyLineClick(PuzzleLineType lineType, int lineIndex, bool evaluateWin = true)
    {
        if (lineType == PuzzleLineType.Row)
            ToggleRow(lineIndex);
        else
            ToggleColumn(lineIndex);

        if (evaluateWin)
            EvaluateWin();
    }

    void ToggleRow(int row)
    {
        if (row < 0 || row >= rows)
            return;
        for (int c = 0; c < columns; c++)
            _grid[row, c]?.Toggle();
    }

    void ToggleColumn(int col)
    {
        if (col < 0 || col >= columns)
            return;
        for (int r = 0; r < rows; r++)
            _grid[r, col]?.Toggle();
    }

    void BuildGrid()
    {
        if (puzzleTilePrefab == null)
        {
            Debug.LogError("[PuzzleManager] 未指定 puzzleTilePrefab，无法生成网格。");
            return;
        }

        rows = Mathf.Max(1, rows);
        columns = Mathf.Max(1, columns);
        _grid = new PuzzleTile[rows, columns];

        // 以原为中心：中心落在 (0,0,0)，方便 Orthographic 相机对准
        float ox = -(columns - 1) * 0.5f * cellSpacing;
        float oy = -(rows - 1) * 0.5f * cellSpacing;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                float x = ox + c * cellSpacing;
                float y = oy + r * cellSpacing;
                // 使用 Z=0 的平面，与 2D 默认一致
                Vector3 pos = new Vector3(x, y, 0f);
                PuzzleTile tile = Instantiate(puzzleTilePrefab, pos, Quaternion.identity, tilesRoot);
                tile.name = $"Tile_{r}_{c}";

                bool targetOn = winStateAllOn;
                // 初始化先放在“已解状态”，再通过合法点击打乱，保证一定可解
                bool startOn = targetOn;
                tile.Configure(r, c, targetOn, startOn);
                int index = GetSliceIndex(r, c);
                Sprite onSlice = GetOnSlice(index);
                Sprite offSlice = GetOffSlice(index);
                tile.SetSprites(onSlice, offSlice);
                _grid[r, c] = tile;
            }
        }

        BuildLineButtons();
        ScrambleFromSolvedState();
    }

    Sprite GetOnSlice(int index)
    {
        if (onSlices != null && index >= 0 && index < onSlices.Length)
            return onSlices[index];
        return null;
    }

    Sprite GetOffSlice(int index)
    {
        if (offSlices != null && index >= 0 && index < offSlices.Length && offSlices[index] != null)
            return offSlices[index];
        return offFallbackSprite;
    }

    int GetSliceIndex(int row, int col)
    {
        // 网格 row=0 在底部；若切片列表从顶部开始，需要翻转行。
        int mappedRow = slicesRowOrderTopToBottom ? (rows - 1 - row) : row;
        int mappedCol = slicesColumnOrderRightToLeft ? (columns - 1 - col) : col;
        return mappedRow * columns + mappedCol;
    }

    void BuildLineButtons()
    {
        if (!generateLineButtons || lineButtonPrefab == null)
            return;

        float leftX = -(columns - 1) * 0.5f * cellSpacing - buttonOffset;
        float topY = (rows - 1) * 0.5f * cellSpacing + buttonOffset;
        float oy = -(rows - 1) * 0.5f * cellSpacing;
        float ox = -(columns - 1) * 0.5f * cellSpacing;

        for (int r = 0; r < rows; r++)
        {
            Vector3 p = new Vector3(leftX, oy + r * cellSpacing, 0f);
            PuzzleLineButton b = Instantiate(lineButtonPrefab, p, Quaternion.identity, buttonsRoot);
            b.Configure(PuzzleLineType.Row, r);
            ApplyButtonSprite(b, rowButtonSprite);
        }

        for (int c = 0; c < columns; c++)
        {
            Vector3 p = new Vector3(ox + c * cellSpacing, topY, 0f);
            PuzzleLineButton b = Instantiate(lineButtonPrefab, p, Quaternion.identity, buttonsRoot);
            b.Configure(PuzzleLineType.Column, c);
            ApplyButtonSprite(b, columnButtonSprite);
        }
    }

    static void ApplyButtonSprite(PuzzleLineButton button, Sprite sprite)
    {
        if (button == null || sprite == null)
            return;
        SpriteRenderer r = button.GetComponent<SpriteRenderer>();
        if (r != null)
            r.sprite = sprite;
    }

    void ScrambleFromSolvedState()
    {
        if (_grid == null)
            return;

        int minMoves = Mathf.Max(0, scrambleMinMoves);
        int maxMoves = Mathf.Max(minMoves, scrambleMaxMoves);
        int moveCount = Random.Range(minMoves, maxMoves + 1);

        // 用“和玩家一致的操作”打乱：随机点击某一行或某一列按钮
        for (int i = 0; i < moveCount; i++)
        {
            bool clickRow = Random.value > 0.5f;
            if (clickRow)
            {
                int row = Random.Range(0, rows);
                ApplyLineClick(PuzzleLineType.Row, row, false);
            }
            else
            {
                int col = Random.Range(0, columns);
                ApplyLineClick(PuzzleLineType.Column, col, false);
            }
        }
    }

    void RegisterManualTilesForCheckOnly()
    {
        _grid = null;
    }

    void EvaluateWin()
    {
        if (!IsPuzzleSolved())
            return;

        _solved = true;
        Debug.Log($"[PuzzleManager] 拼图完成，{loadDelaySeconds:0.##} 秒后加载场景：{winSceneName}");
        _loadTimer = loadDelaySeconds;
    }

    /// <summary>
    /// 对外可调用：判断是否全部达标（手动摆放模式也可用）。
    /// </summary>
    public bool IsPuzzleSolved()
    {
        if (generateGridFromPrefab && _grid != null)
        {
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    PuzzleTile t = _grid[r, c];
                    if (t == null || !t.IsCorrect)
                        return false;
                }
            }

            return true;
        }

        foreach (PuzzleTile t in manualTiles)
        {
            if (t == null || !t.IsCorrect)
                return false;
        }

        return manualTiles != null && manualTiles.Count > 0;
    }

    /// <summary>
    /// 重置当前关卡初始状态（仅生成模式且持有 _grid 时有效）。
    /// </summary>
    public void RegenerateGrid()
    {
        _solved = false;
        _loadTimer = -1f;

        if (!generateGridFromPrefab)
            return;

        if (tilesRoot == null)
            tilesRoot = transform;
        if (buttonsRoot == null)
            buttonsRoot = transform;

        for (int i = tilesRoot.childCount - 1; i >= 0; i--)
            Destroy(tilesRoot.GetChild(i).gameObject);
        for (int i = buttonsRoot.childCount - 1; i >= 0; i--)
            Destroy(buttonsRoot.GetChild(i).gameObject);

        BuildGrid();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!generateGridFromPrefab)
            return;

        int r = Mathf.Max(1, rows);
        int c = Mathf.Max(1, columns);
        float ox = -(c - 1) * 0.5f * cellSpacing;
        float oy = -(r - 1) * 0.5f * cellSpacing;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        for (int y = 0; y < r; y++)
        {
            for (int x = 0; x < c; x++)
                Gizmos.DrawWireCube(transform.position + new Vector3(ox + x * cellSpacing, oy + y * cellSpacing, 0f), Vector3.one * 0.85f);
        }
    }
#endif

    void OnGUI()
    {
        if (!_solved || !showWinPrompt)
            return;

        if (_promptStyle == null)
        {
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 36,
                normal = { textColor = Color.white }
            };
        }

        Rect r = new Rect(0f, Screen.height * 0.5f - 40f, Screen.width, 80f);
        GUI.Label(r, winPromptText, _promptStyle);
    }
}

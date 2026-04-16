using UnityEngine;

[DisallowMultipleComponent]
public class GameState : MonoBehaviour
{
    // 全局静态状态：是否已获得能源块（默认 false）
    public static bool hasBattery = false;
    // 全局静态状态：是否已击败 Boss（默认 false）
    public static bool bossDefeated = false;
    // 全局静态状态：门首次交互是否已经进入过 Boss 场景
    public static bool bossSceneEntered = false;

    // 可选：进入 Play 后首次加载 GameState 时自动重置一次。
    [SerializeField] bool resetOnPlayStart = true;
    static bool _initialized;

    void Awake()
    {
        if (_initialized)
            return;
        _initialized = true;

        if (resetOnPlayStart)
        {
            hasBattery = false;
            bossDefeated = false;
            bossSceneEntered = false;
        }
    }
}

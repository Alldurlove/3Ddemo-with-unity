using UnityEngine;
using UnityEngine.SceneManagement;

public class BossHealth : MonoBehaviour
{
    [Tooltip("Boss 初始 / 最大血量")]
    [SerializeField] int health = 3;
    [Tooltip("死亡时要销毁的目标。不填则默认销毁根物体。")]
    [SerializeField] Transform destroyTarget;
    [SerializeField] bool logCurrentHealthOnHit = true;
    [Header("On Boss Defeated")]
    [SerializeField] bool loadMainSceneOnDefeat = true;
    [SerializeField] string mainSceneName = "MainScene";

    int _current;

    public int CurrentHealth => _current;

    void Awake()
    {
        _current = health;
        if (destroyTarget == null)
            destroyTarget = transform.root;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || _current <= 0)
            return;

        _current -= amount;
        if (logCurrentHealthOnHit)
            Debug.Log($"Boss当前血量: {Mathf.Max(0, _current)}/{health}");
        if (_current <= 0)
        {
            _current = 0;
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Boss当前血量: 0/" + health);
        Debug.Log("Boss died");
        GameState.bossDefeated = true;
        GameObject target = destroyTarget != null ? destroyTarget.gameObject : gameObject;
        Destroy(target);
        if (loadMainSceneOnDefeat && !string.IsNullOrWhiteSpace(mainSceneName))
            SceneManager.LoadScene(mainSceneName);
    }
}

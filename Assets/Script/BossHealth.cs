using UnityEngine;

public class BossHealth : MonoBehaviour
{
    [Tooltip("Boss 初始 / 最大血量")]
    [SerializeField] int health = 3;

    int _current;

    public int CurrentHealth => _current;

    void Awake()
    {
        _current = health;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || _current <= 0)
            return;

        _current -= amount;
        if (_current <= 0)
        {
            _current = 0;
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Boss died");
    }
}

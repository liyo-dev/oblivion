using UnityEngine;
using UnityEngine.UI;

public class PlayerDamageScreenEffects : MonoBehaviour
{
    [SerializeField] private Image damageImage;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    private PlayerHealthSystem _playerHealthSystem;
    private Coroutine _flashCoroutine;

    private void Awake()
    {
        FindPlayerHealthSystem();
    }

    private void FindPlayerHealthSystem()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerHealthSystem = player.GetComponent<PlayerHealthSystem>();
        }
        
        if (_playerHealthSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            _playerHealthSystem = FindFirstObjectByType<PlayerHealthSystem>();
#else
            _playerHealthSystem = FindObjectOfType<PlayerHealthSystem>();
#endif
        }
        
        if (_playerHealthSystem != null)
        {
            _playerHealthSystem.OnDamageTaken.AddListener((damage, health) => OnPlayerDamageTaken(damage));
            _playerHealthSystem.OnHealthChanged.AddListener(OnPlayerHealthChanged);
        }
    }

    private void OnPlayerDamageTaken(float damage)
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }

        _flashCoroutine = StartCoroutine(FlashDamageImage());
    }

    private void OnPlayerHealthChanged(float healthPercentage)
    {
        // Implement any health change logic here if needed
    }

    private System.Collections.IEnumerator FlashDamageImage()
    {
        damageImage.color = damageColor;
        yield return new WaitForSecondsRealtime(flashDuration);
        damageImage.color = Color.clear;

        _flashCoroutine = null;
    }

    private void OnDisable()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        if (damageImage != null)
        {
            damageImage.color = Color.clear;
        }
    }
}

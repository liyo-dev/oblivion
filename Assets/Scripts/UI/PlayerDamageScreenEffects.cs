using UnityEngine;
using UnityEngine.UI;

public class PlayerDamageScreenEffects : MonoBehaviour
{
    [SerializeField] private Image damageImage;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    private PlayerHealthSystem _playerHealthSystem;

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
            _playerHealthSystem = ServiceLocator.Get<PlayerHealthSystem>(false);
        }
        
        if (_playerHealthSystem != null)
        {
            _playerHealthSystem.OnDamageTaken.AddListener((damage, health) => OnPlayerDamageTaken(damage));
            _playerHealthSystem.OnHealthChanged.AddListener(OnPlayerHealthChanged);
        }
    }

    private void OnPlayerDamageTaken(float damage)
    {
        StartCoroutine(FlashDamageImage());
    }

    private void OnPlayerHealthChanged(float healthPercentage)
    {
        // Implement any health change logic here if needed
    }

    private System.Collections.IEnumerator FlashDamageImage()
    {
        damageImage.color = damageColor;
        yield return new WaitForSeconds(flashDuration);
        damageImage.color = Color.clear;
    }
}

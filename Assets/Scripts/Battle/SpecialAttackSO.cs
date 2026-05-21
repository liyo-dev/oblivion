using UnityEngine;

public enum DuoCompanion { Estela, Liam }

/// <summary>
/// Define un ataque especial dúo entre Will y un compañero.
/// Asignar uno a LT (Estela) y otro a RT (Liam) en DuoSpecialAttackSystem.
/// </summary>
[CreateAssetMenu(fileName = "SpecialAttack_", menuName = "Battle/Duo Special Attack")]
public class SpecialAttackSO : ScriptableObject
{
    [Header("Identidad")]
    public string        displayName = "Ataque Especial";
    public DuoCompanion  companion   = DuoCompanion.Estela;
    public Sprite        icon;

    [Header("Requisito de proximidad")]
    [Tooltip("Distancia máxima a la que debe estar el compañero para activarse")]
    public float companionMaxDistance = 10f;

    [Header("Animaciones")]
    [Tooltip("Trigger del Animator para el jugador (Will)")]
    public string playerAnimationTrigger;
    [Tooltip("Trigger del Animator para el compañero")]
    public string companionAnimationTrigger;

    [Header("Visual y Audio")]
    public GameObject vfxPrefab;
    [Tooltip("Clave de SFX en AudioService")]
    public string     castSFXKey;
    [Tooltip("Duración del VFX antes de destruirse")]
    public float      vfxLifetime = 3f;
    [Tooltip("Segundos de delay entre animación y aplicación de daño")]
    public float      damageDelay = 0.4f;

    [Header("Daño")]
    public float damage        = 80f;
    public float aoeRadius     = 4f;
    public float knockbackForce = 18f;

    [Header("Cámara")]
    public bool shakeCameraOnCast = true;
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SpecialChargeMeterUI : MonoBehaviour
{
    [Header("Slot Estela")]
    [SerializeField] private Image estelaImage;
    [SerializeField] private Image estelaGlow;

    [Header("Slot Liam")]
    [SerializeField] private Image liamImage;
    [SerializeField] private Image liamGlow;

    [Header("Colores")]
    [SerializeField] private Color readyColor = new Color(1f, 0.92f, 0.2f, 1f);
    [SerializeField] private Color idleColor  = new Color(1f, 1f, 1f, 0.35f);

    [Header("Animación glow")]
    [SerializeField] private float pulseSpeed    = 2.5f;
    [SerializeField] private float glowMinAlpha  = 0.25f;
    [SerializeField] private float glowMaxAlpha  = 1f;

    private DuoSpecialAttackSystem _duo;
    private SpecialChargeMeter     _estelaM, _liamM;
    private Coroutine              _estelaRoutine, _liamRoutine;

    void OnEnable()
    {
        Connect();
        RefreshState();
    }

    void OnDisable()
    {
        Disconnect();
        StopAllCoroutines();
        _estelaRoutine = _liamRoutine = null;
    }

    private void Connect()
    {
        var player = PlayerService.Player;
        if (player != null)
            _duo = player.GetComponentInChildren<DuoSpecialAttackSystem>();
        if (_duo == null)
            _duo = FindFirstObjectByType<DuoSpecialAttackSystem>();

        if (_duo == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SpecialChargeMeterUI] No se encontró DuoSpecialAttackSystem.");
#endif
            return;
        }

        _estelaM = _duo.EstelaChargeMeter;
        _liamM   = _duo.LiamChargeMeter;

        if (_estelaM != null) _estelaM.OnChargeUpdated += OnEstelaCharge;
        if (_liamM   != null) _liamM.OnChargeUpdated   += OnLiamCharge;
    }

    private void Disconnect()
    {
        if (_estelaM != null) _estelaM.OnChargeUpdated -= OnEstelaCharge;
        if (_liamM   != null) _liamM.OnChargeUpdated   -= OnLiamCharge;
        _estelaM = _liamM = null;
        _duo = null;
    }

    private void RefreshState()
    {
        SetReady(estelaImage, estelaGlow, ref _estelaRoutine, _estelaM != null && _estelaM.IsFullyCharged);
        SetReady(liamImage,   liamGlow,   ref _liamRoutine,   _liamM   != null && _liamM.IsFullyCharged);
    }

    private void OnEstelaCharge(float _) =>
        SetReady(estelaImage, estelaGlow, ref _estelaRoutine, _estelaM != null && _estelaM.IsFullyCharged);

    private void OnLiamCharge(float _) =>
        SetReady(liamImage, liamGlow, ref _liamRoutine, _liamM != null && _liamM.IsFullyCharged);

    private void SetReady(Image img, Image glow, ref Coroutine routine, bool ready)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (img != null)
            img.color = ready ? readyColor : idleColor;

        if (glow != null)
        {
            if (ready)
                routine = StartCoroutine(Co_GlowPulse(img, glow));
            else
                SetAlpha(glow, 0f);
        }
    }

    private IEnumerator Co_GlowPulse(Image img, Image glow)
    {
        while (true)
        {
            float t     = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, t);
            SetAlpha(glow, alpha);

            // el icono también pulsa ligeramente en brillo
            if (img != null)
            {
                Color c = readyColor;
                c.a = Mathf.Lerp(0.75f, 1f, t);
                img.color = c;
            }

            yield return null;
        }
    }

    private static void SetAlpha(Image img, float alpha)
    {
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    public void Reconnect()
    {
        Disconnect();
        Connect();
        RefreshState();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MenuNavigator : MonoBehaviour
{
    [Header("Items (auto si vacío)")]
    public List<Button> items = new();

    [Header("Comportamiento")]
    public bool wrapAround = true;           // subir desde el primero -> último, y viceversa
    [Min(0.05f)] public float repeatDelay = 0.18f;  // anti-rebote del stick/dpad

    [Header("Animación selección (sutil, opcional)")]
    public float nudge = 6f;                 // pequeño empujoncito horizontal en el item activo
    public float nudgeTime = 0.08f;

    EventSystem _es;
    int _idx = -1;
    float _cooldown;
    readonly List<RectTransform> _nudged = new();
    Vector2 _queuedMove;
    Vector2 _heldNav;
    float _navEventExpiry;
    bool _submitRequested;

    void Awake()
    {
        _es = EventSystem.current;
        AutoPopulateIfNeeded();
        DisableUnityNavigation();
    }

    void OnEnable()
    {
        AutoPopulateIfNeeded();
        SelectFirstInteractable();

        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
    }

    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleGamepadInput;
    }

    void Update()
    {
        _cooldown -= Time.unscaledDeltaTime;

        if (_heldNav != Vector2.zero && Time.unscaledTime > _navEventExpiry)
            _heldNav = Vector2.zero;

        Vector2 move = Vector2.zero;
        if (_queuedMove != Vector2.zero && _cooldown <= 0f)
        {
            move = _queuedMove;
            _queuedMove = Vector2.zero;
        }
        else if (_cooldown <= 0f)
        {
            move = ReadHeldMoveFromEvents();
        }

        if (move != Vector2.zero && _cooldown <= 0f)
        {
            MoveSelection(move);
            _cooldown = repeatDelay;
        }

        if (_submitRequested)
        {
            _submitRequested = false;
            var btn = CurrentButton();
            if (btn && btn.interactable) btn.onClick.Invoke();
        }

        // Si perdemos focus (por animaciones), lo recuperamos al actual
        if (_es && (_es.currentSelectedGameObject == null))
        {
            var b = CurrentButton();
            if (b) _es.SetSelectedGameObject(b.gameObject);
        }
    }

    void AutoPopulateIfNeeded(bool force = false)
    {
        if (!force && items.Count > 0) return;
        items.Clear();
        var btns = GetComponentsInChildren<Button>();
        // Ordenar por posición vertical (arriba -> abajo)
        System.Array.Sort(btns, (a,b) =>
            -a.transform.position.y.CompareTo(b.transform.position.y));
        items.AddRange(btns);

        DisableUnityNavigation();
    }

    void DisableUnityNavigation()
    {
        foreach (var b in items)
        {
            if (!b) continue;
            b.transition = Selectable.Transition.None;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            var img = b.GetComponent<Image>();
            if (img) img.enabled = false; // “solo texto”
        }
    }

    void SelectFirstInteractable()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] && items[i].gameObject.activeInHierarchy && items[i].interactable)
            { SetSelection(i); return; }
        }
    }

    void MoveSelection(Vector2 dir)
    {
        if (items.Count == 0) return;
        AutoPopulateIfNeeded();

        if (_idx < 0 || _idx >= items.Count)
            SelectFirstInteractable();

        dir = dir.normalized;
        if (dir == Vector2.zero) return;

        var current = CurrentButton();
        if (current == null)
        {
            SelectFirstInteractable();
            current = CurrentButton();
            if (current == null) return;
        }

        float bestScore = float.NegativeInfinity;
        int bestIndex = -1;
        var currentPos = (Vector2)current.transform.position;

        for (int i = 0; i < items.Count; i++)
        {
            var candidate = items[i];
            if (candidate == null || candidate == current ||
                !candidate.gameObject.activeInHierarchy || !candidate.interactable)
                continue;

            var toCandidate = (Vector2)candidate.transform.position - currentPos;
            if (toCandidate.sqrMagnitude < 0.0001f) continue;

            var toDir = toCandidate.normalized;
            float alignment = Vector2.Dot(dir, toDir);
            if (alignment <= 0.1f) continue; // debe estar razonablemente en la dirección pedida

            // favorece elementos en la dirección solicitada y cercanos en distancia/perpendicularidad
            float perpendicular = Mathf.Abs(Vector2.Dot(new Vector2(dir.y, -dir.x), toDir));
            float distancePenalty = toCandidate.sqrMagnitude * 0.01f;
            float score = alignment - perpendicular * 0.15f - distancePenalty;

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            SetSelection(bestIndex);
            return;
        }

        if (!wrapAround) return;

        // Wrap-around: ir al más extremo en la dirección solicitada cuando no hay candidatos directos
        int fallbackIndex = -1;
        float extreme = dir.x != 0f ? dir.x > 0f ? float.NegativeInfinity : float.PositiveInfinity
                                   : dir.y > 0f ? float.NegativeInfinity : float.PositiveInfinity;

        for (int i = 0; i < items.Count; i++)
        {
            var candidate = items[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.interactable)
                continue;

            var pos = candidate.transform.position;
            float axisValue = dir.x != 0f ? pos.x : pos.y;

            if (dir.x > 0f || dir.y > 0f)
            {
                if (axisValue > extreme)
                { extreme = axisValue; fallbackIndex = i; }
            }
            else
            {
                if (axisValue < extreme)
                { extreme = axisValue; fallbackIndex = i; }
            }
        }

        if (fallbackIndex >= 0)
            SetSelection(fallbackIndex);
    }

    void SetSelection(int i)
    {
        _idx = i;
        var b = items[i];
        if (!b) return;

        // focus UI
        if (_es) _es.SetSelectedGameObject(b.gameObject);

        // “nudge” visual al hijo de texto (convive con tu MenuTextHighlight)
        var rt = b.GetComponentInChildren<RectTransform>();
        if (rt)
        {
            // resetear todos y empujar el activo
            foreach (var prev in _nudged) { if (prev) prev.DOKill(); }
            _nudged.Clear();

            var startPos = rt.anchoredPosition;
            rt.DOKill();
            rt.DOComplete();
            rt.anchoredPosition = startPos; // por si acaso
            rt.DOAnchorPos(startPos + new Vector2(nudge, 0f), nudgeTime)
              .SetEase(Ease.OutCubic)
              .OnKill(() => { if (rt) rt.anchoredPosition = startPos; });
            _nudged.Add(rt);
        }
    }

    Button CurrentButton() => (_idx >= 0 && _idx < items.Count) ? items[_idx] : null;

    /// <summary>
    /// Reconstruye la lista de botones a partir de los hijos actuales.
    /// </summary>
    public void RefreshItemsFromChildren(bool resetSelection = true)
    {
        var current = CurrentButton();

        AutoPopulateIfNeeded(force: true);

        if (resetSelection)
        {
            SelectFirstInteractable();
        }
        else if (current != null)
        {
            int idx = items.IndexOf(current);
            if (idx >= 0)
                SetSelection(idx);
            else
                _idx = -1;
        }

        ResetCooldown();
    }

    /// <summary>
    /// Fuerza la selección a un botón concreto y opcionalmente reinicia el cooldown de navegación.
    /// </summary>
    public void ForceSelect(Button button, bool resetCooldown = true)
    {
        if (button == null) return;

        AutoPopulateIfNeeded();
        int idx = items.IndexOf(button);
        if (idx < 0) return;

        SetSelection(idx);
        if (resetCooldown) _cooldown = 0f;
    }

    /// <summary>
    /// Resetea el cooldown de navegación para que el siguiente input se procese inmediatamente.
    /// </summary>
    public void ResetCooldown()
    {
        _cooldown = 0f;
    }

    public void ForceSelect(GameObject go, bool resetCooldown = true)
        => ForceSelect(go != null ? go.GetComponent<Button>() : null, resetCooldown);

    Vector2 ReadHeldMoveFromEvents()
    {
        if (_heldNav == Vector2.zero) return Vector2.zero;

        if (Mathf.Abs(_heldNav.y) >= Mathf.Abs(_heldNav.x))
            return new Vector2(0f, _heldNav.y > 0f ? 1f : -1f);

        return new Vector2(_heldNav.x > 0f ? 1f : -1f, 0f);
    }

    void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        switch (input.Type)
        {
            case GamepadInputReader.InputEventType.Navigate:
                if (input.Phase == InputActionPhase.Canceled)
                {
                    _heldNav = Vector2.zero;
                }
                else
                {
                    _heldNav = input.Value;
                    _navEventExpiry = Time.unscaledTime + 0.2f;
                }
                break;

            case GamepadInputReader.InputEventType.DpadUp when input.Phase == InputActionPhase.Performed:
                _queuedMove = Vector2.up;
                _navEventExpiry = Time.unscaledTime + 0.1f;
                break;

            case GamepadInputReader.InputEventType.DpadDown when input.Phase == InputActionPhase.Performed:
                _queuedMove = Vector2.down;
                _navEventExpiry = Time.unscaledTime + 0.1f;
                break;

            case GamepadInputReader.InputEventType.DpadLeft when input.Phase == InputActionPhase.Performed:
                _queuedMove = Vector2.left;
                _navEventExpiry = Time.unscaledTime + 0.1f;
                break;

            case GamepadInputReader.InputEventType.DpadRight when input.Phase == InputActionPhase.Performed:
                _queuedMove = Vector2.right;
                _navEventExpiry = Time.unscaledTime + 0.1f;
                break;

            case GamepadInputReader.InputEventType.Submit when input.Phase == InputActionPhase.Performed:
                _submitRequested = true;
                break;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
    }

    void Update()
    {
        _cooldown -= Time.unscaledDeltaTime;

        int move = ReadUpDownThisFrame();
        if (move != 0 && _cooldown <= 0f)
        {
            MoveSelection(move);
            _cooldown = repeatDelay;
        }

        if (PressedSubmitThisFrame())
        {
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

    void AutoPopulateIfNeeded()
    {
        if (items.Count > 0) return;
        items.Clear();
        var btns = GetComponentsInChildren<Button>(true);
        // Ordenar por posición vertical (arriba -> abajo)
        System.Array.Sort(btns, (a,b) =>
            -a.transform.position.y.CompareTo(b.transform.position.y));
        items.AddRange(btns);
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

    void MoveSelection(int dir) // dir: -1 arriba, +1 abajo
    {
        if (items.Count == 0) return;
        int start = _idx < 0 ? 0 : _idx;
        int i = start;

        for (int step = 0; step < items.Count; step++)
        {
            i = i + dir;
            if (wrapAround) i = (i + items.Count) % items.Count;
            else i = Mathf.Clamp(i, 0, items.Count - 1);

            var b = items[i];
            if (b && b.gameObject.activeInHierarchy && b.interactable)
            { SetSelection(i); return; }

            // si no hay wrap y chocamos en extremos, salimos
            if (!wrapAround && (i == 0 || i == items.Count - 1))
                return;
        }
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

    int ReadUpDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        // Eventos discretos para teclado/gamepad
        if (Gamepad.current != null)
        {
            var gp = Gamepad.current;
            if (gp.dpad.up.wasPressedThisFrame   || gp.leftStick.up.wasPressedThisFrame)   return -1;
            if (gp.dpad.down.wasPressedThisFrame || gp.leftStick.down.wasPressedThisFrame) return +1;
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)   return -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) return +1;
        }
        return 0;
#else
        // Entrada antigua (detecta flanco)
        int v = (int)Mathf.Sign(Input.GetAxisRaw("Vertical"));
        // convertimos a discreto por frame
        if (v > 0 && Input.GetButtonDown("Vertical")) return +1;
        if (v < 0 && Input.GetButtonDown("Vertical")) return -1;
        // fallback
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))   return -1;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return +1;
        return 0;
#endif
    }

    bool PressedSubmitThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            var gp = Gamepad.current;
            if (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame) return true;
        }
        var kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
#else
        return Input.GetButtonDown("Submit");
#endif
    }
}

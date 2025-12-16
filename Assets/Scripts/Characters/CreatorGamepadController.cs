using UnityEngine;
using Core;

public class CreatorGamepadController : MonoBehaviour
{
    public ModularAutoBuilder builder;               // arrastra MC01
    public RowSelectionHighlighter highlighter;      // arrastra Panel (RowSelectionHighlighter)
    public CharacterCreatorUI ui;                    // arrastra el componente del Panel

    float _lastNavY;
    float _lastNavX;

    void OnEnable()
    {
        // Cambiar a modo UI para el character creator
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PushUIMode();
    }

    void OnDisable()
    {
        // Restaurar modo Gameplay
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            pim.PopUIMode();
    }

    void Start()
    {
        // Selección visual inicial
        Debug.Log($"Start - Highlighter: {(highlighter != null ? "OK" : "NULL")}");
        if (highlighter)
            Debug.Log($"Highlighter Count: {highlighter.Count}");
        
        if (highlighter && highlighter.Count > 0) 
        {
            highlighter.SetSelected(0);
            Debug.Log("Selección inicial establecida en 0");
        }
        else
        {
            Debug.LogError("ERROR: Highlighter es null o no tiene filas registradas!");
        }
    }

    void Update()
    {
        if (builder == null) return;

        // Obtener controles del PlayerInputManager
        if (!ServiceLocator.TryGet(out Core.PlayerInputManager pim) || pim.Controls == null)
            return;

        var controls = pim.Controls;

        // --- D-Pad / stick VERTICAL para cambiar categoría (arriba/abajo) ---
        Vector2 nav = controls.UI.Navigate.ReadValue<Vector2>();
        
        // Abajo: categoría siguiente
        if (nav.y < -0.5f && _lastNavY >= -0.5f)
        {
            Debug.Log("Abajo detectado");
            Debug.Log($"Highlighter null? {highlighter == null}");
            if (highlighter)
            {
                Debug.Log($"Highlighter.Count = {highlighter.Count}");
                Debug.Log($"SelectedIndex actual = {highlighter.SelectedIndex}");
            }
            
            if (highlighter && highlighter.Count > 0)
            {
                int newIndex = highlighter.SelectedIndex + 1;
                if (newIndex >= highlighter.Count) newIndex = 0;
                Debug.Log($"Intentando cambiar a índice: {newIndex}");
                highlighter.SetSelected(newIndex);
                Debug.Log($"Nueva selección: {newIndex}");
            }
            else
            {
                Debug.LogError("No se puede cambiar selección: highlighter null o sin filas");
            }
        }
        
        // Arriba: categoría anterior
        if (nav.y > 0.5f && _lastNavY <= 0.5f)
        {
            Debug.Log("Arriba detectado");
            if (highlighter && highlighter.Count > 0)
            {
                int newIndex = highlighter.SelectedIndex - 1;
                if (newIndex < 0) newIndex = highlighter.Count - 1;
                Debug.Log($"Intentando cambiar a índice: {newIndex}");
                highlighter.SetSelected(newIndex);
                Debug.Log($"Nueva selección: {newIndex}");
            }
        }
        
        _lastNavY = nav.y;

        // Categoría actual según highlight
        var cat = ui ? ui.CurrentHighlightedCategory() : PartCategory.Body;

        // --- A = Siguiente variante (derecha) ---
        if (controls.UI.Submit.WasPressedThisFrame())
        {
            Debug.Log($"A presionado - Next en {cat}");
            builder.Next(cat);
        }

        // --- X = Variante anterior (izquierda) ---
        if (controls.GamePlay.AttackMagicWest.WasPressedThisFrame())
        {
            Debug.Log($"X presionado - Prev en {cat}");
            builder.Prev(cat);
        }

        // --- B = Toggle on/off ---
        if (controls.UI.Cancel.WasPressedThisFrame())
        {
            Debug.Log($"B presionado - Toggle {cat}");
            var sel = builder.GetSelection();
            if (sel.ContainsKey(cat))
                builder.SetByName(cat, null);          // apagar
            else
                builder.SetByIndex(cat, 0);            // encender el primero
        }
    }
}
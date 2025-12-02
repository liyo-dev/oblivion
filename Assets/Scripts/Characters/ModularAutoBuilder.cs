using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class ModularAutoBuilder : MonoBehaviour
{
    // Prefijos -> categorías (case-insensitive)
    // IMPORTANTE: Orden importa - más específicos primero
    static readonly (string prefix, PartCategory cat)[] Map = new (string, PartCategory)[]
    {
        // Armas PRIMERO (antes de Body para evitar colisiones)
        ("Bow",      PartCategory.Bow),
        ("OHS",      PartCategory.Ohs),
        ("Shield",   PartCategory.ShieldR),
        ("Arrows",   PartCategory.Arrows),
        
        // Cuerpo y ropa
        ("Body",     PartCategory.Body),
        ("Cloak",    PartCategory.Cloak),
        ("AC",       PartCategory.Accessory),

        // OJO: Eyebrow antes que Eye para no colisionar
        ("Eyebrow",  PartCategory.Eyebrow),
        ("Eye",      PartCategory.Eyes),

        ("Mouth",    PartCategory.Mouth),
        ("Hair",     PartCategory.Hair),
        ("Head",     PartCategory.Head),
        ("Hat",      PartCategory.Hat),
    };


    static readonly HashSet<PartCategory> WeaponCats = new() { PartCategory.Bow, PartCategory.Ohs, PartCategory.ShieldR };

    // cache
    readonly Dictionary<PartCategory, List<GameObject>> parts = new();
    readonly Dictionary<PartCategory, int> idx = new();

    // Snapshot de la selección inicial para poder restaurarla (ej: Nueva Partida)
    Dictionary<PartCategory, string> _initialSelection;

    // Use central Hand enum from Identifiers.cs (None, Left, Right)
    readonly Dictionary<GameObject, Hand> handOf = new();

    [Header("Opcional")]
    public bool randomizeAtAwake = false;      // déjalo en false hasta que veas todo ok
    public bool preserveActivePartsOnAwake = true;  // mantiene las partes activas del prefab

    void Awake()
    {
        CacheAll();
        
        if (randomizeAtAwake)
        {
            DeactivateAll();
            RandomizeAll();
        }
        else if (preserveActivePartsOnAwake)
        {
            // NO TOCAR NADA - Solo registra lo que está activo
            RegisterActivePartsWithoutModifying();
        }
        else
        {
            DeactivateAll();
        }

        // Guardar una foto de lo que el prefab tenía activo al arrancar para poder
        // resetear la apariencia cuando se inicie una partida nueva.
        _initialSelection = GetSelection();
    }

    void Start()
    {
        // Asegura que holders típicos estén activos (por si el prefab los trae apagados)
        EnsureHolderActive("head");
        EnsureHolderActive("weapon_l");
        EnsureHolderActive("weapon_r");
    }
    
    void RegisterActivePartsWithoutModifying()
    {   
        foreach (var kvp in parts)
        {
            foreach (var go in kvp.Value)
            {
                bool isSelf = go.activeSelf;
                bool isHierarchy = go.activeInHierarchy;
                
                if (isSelf || isHierarchy)
                {
                    var list = kvp.Value;
                    int index = list.IndexOf(go);
                    if (index >= 0 && !idx.ContainsKey(kvp.Key))
                    {
                        idx[kvp.Key] = index;
                    }
                }
            }
        }
    }
    
    void DetectAndPreserveActiveParts()
    {
        // Primero, detecta qué partes están activas en el prefab
        var activePartsDetected = new Dictionary<PartCategory, GameObject>();
        
        foreach (var kvp in parts)
        {
            foreach (var go in kvp.Value)
            {
                if (go.activeInHierarchy || go.activeSelf)
                {
                    // Si ya hay una activa en esta categoría, registra solo la primera
                    if (!activePartsDetected.ContainsKey(kvp.Key))
                    {
                        activePartsDetected[kvp.Key] = go;
                    }
                }
            }
        }
        
        Debug.Log($"[ModularAutoBuilder] Total detectadas: {activePartsDetected.Count} partes activas");
        
        // Si no se detectó nada activo, no hagas nada (deja todo como está)
        if (activePartsDetected.Count == 0)
        {
            Debug.LogWarning("[ModularAutoBuilder] No se detectaron partes activas. Dejando todo como está.");
            return;
        }
        
        // Ahora desactiva todas las partes
        foreach (var list in parts.Values)
            foreach (var go in list) 
                go.SetActive(false);
        
        idx.Clear();
        
        // Reactiva solo las que estaban activas y registra su índice
        foreach (var kvp in activePartsDetected)
        {
            var cat = kvp.Key;
            var go = kvp.Value;
            
            if (parts.TryGetValue(cat, out var list))
            {
                int index = list.IndexOf(go);
                if (index >= 0)
                {
                    EnsureAncestorsActive(go.transform);
                    go.SetActive(true);
                    idx[cat] = index;
                    Debug.Log($"[ModularAutoBuilder] Reactivado {cat}: {go.name} (índice {index})");
                }
            }
        }
    }

    // ---------- Construcción de caché ----------
    void CacheAll()
    {
        parts.Clear();
        handOf.Clear();
        foreach (var c in Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>())
            parts[c] = new List<GameObject>();

        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == transform) continue;

            PartCategory? maybe = null;
            foreach (var m in Map)
                if (t.name.StartsWith(m.prefix, StringComparison.OrdinalIgnoreCase)) { maybe = m.cat; break; }
            if (maybe == null) continue;

            var go = t.gameObject;
            
            // FILTRO: Solo cachear objetos que tienen Renderer DIRECTAMENTE
            // Esto excluye carpetas contenedoras como "Bows", "Shields", "head", etc.
            bool hasRenderer = go.GetComponent<Renderer>() != null;
            if (!hasRenderer)
            {
                continue;
            }
            
            // FILTRO EXTRA para armas: deben tener números en el nombre (Bow01, Shield02, etc.)
            // Esto excluye carpetas como "Bows" (sin número)
            if (maybe == PartCategory.Bow || maybe == PartCategory.Ohs || maybe == PartCategory.ShieldR || maybe == PartCategory.Arrows)
            {
                bool hasDigit = t.name.Any(char.IsDigit);
                if (!hasDigit) continue;
            }

            parts[maybe.Value].Add(go);

            // mano aproximada por ruta
            var path = GetPath(t);
            Hand h = Hand.None;
            if (path.IndexOf("weapon_l", StringComparison.OrdinalIgnoreCase) >= 0) h = Hand.Left;
            else if (path.IndexOf("weapon_r", StringComparison.OrdinalIgnoreCase) >= 0) h = Hand.Right;
            else if (path.Contains("_l/")) h = Hand.Left;
            else if (path.Contains("_r/")) h = Hand.Right;
            handOf[go] = h;
        }

        foreach (var kv in parts)
            kv.Value.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            
        Debug.Log($"[ModularAutoBuilder.CacheAll] Cacheado completo. Total categorías con partes: {parts.Count(p => p.Value.Count > 0)}");
    }

    static string GetPath(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    void DeactivateAll()
    {
        foreach (var list in parts.Values)
            foreach (var go in list) go.SetActive(false);
        idx.Clear();
    }

    void EnsureHolderActive(string holderName)
    {
        var holder = GetComponentsInChildren<Transform>(true)
                     .FirstOrDefault(x => x.name.Equals(holderName, StringComparison.OrdinalIgnoreCase));
        if (holder && !holder.gameObject.activeSelf) holder.gameObject.SetActive(true);
    }

    void TryEnsureOne(PartCategory cat)
    {
        var opts = GetOptions(cat);
        if (opts != null && opts.Count > 0)
        {
            // si no hay ya algo activo, enciende el primero
            if (!idx.ContainsKey(cat)) SetByIndex(cat, 0);
        }
    }

    // ---------- API pública ----------
    public IReadOnlyList<string> GetOptions(PartCategory cat) =>
        parts.TryGetValue(cat, out var list) ? list.Select(g => g.name).ToList() : Array.Empty<string>();

    public Dictionary<PartCategory, string> GetSelection()
    {
        var dict = new Dictionary<PartCategory, string>();
        foreach (var kv in idx)
        {
            var list = parts[kv.Key];
            if (kv.Value >= 0 && kv.Value < list.Count)
                dict[kv.Key] = list[kv.Value].name;
        }
        return dict;
    }

    public void ApplySelection(Dictionary<PartCategory, string> sel)
    {
        // aplica Bow primero por su dependencia con Arrows
        if (sel.TryGetValue(PartCategory.Bow, out var bow)) SetByName(PartCategory.Bow, bow);
        foreach (var kv in sel)
        {
            if (kv.Key == PartCategory.Bow) continue;
            SetByName(kv.Key, kv.Value);
        }
    }

    public void RestoreInitialSelection()
    {
        if (_initialSelection == null || _initialSelection.Count == 0)
        {
            // Si no había snapshot, mantener selección actual para no dejar el modelo vacío.
            return;
        }

        // Limpiar selección actual y aplicar la inicial tal cual estaba en el prefab.
        foreach (var kv in parts)
        {
            for (int i = 0; i < kv.Value.Count; i++)
                kv.Value[i].SetActive(false);
        }
        idx.Clear();

        ApplySelection(new Dictionary<PartCategory, string>(_initialSelection));
    }

    public void Next(PartCategory cat, int step = 1)
    {
        if (!parts.TryGetValue(cat, out var list) || list.Count == 0) return;
        int cur = idx.TryGetValue(cat, out var v) ? v : -1;
        cur = (cur + step + list.Count) % list.Count;
        SetByIndex(cat, cur);
    }
    public void Prev(PartCategory cat) => Next(cat, -1);

    public void Randomize(PartCategory cat, float noneChance = 0f)
    {
        if (!parts.TryGetValue(cat, out var list) || list.Count == 0) return;
        if (noneChance > 0f && UnityEngine.Random.value < noneChance) { SetByName(cat, null); return; }
        int i = UnityEngine.Random.Range(0, list.Count);
        SetByIndex(cat, i);
    }

    public void RandomizeAll()
    {
        Randomize(PartCategory.Body);
        Randomize(PartCategory.Cloak, 0.5f);
        Randomize(PartCategory.Head);
        Randomize(PartCategory.Hair, 0.2f);
        Randomize(PartCategory.Eyes);
        Randomize(PartCategory.Mouth);
        Randomize(PartCategory.Hat, 0.6f);
        Randomize(PartCategory.Eyebrow, 0.3f);
        Randomize(PartCategory.Accessory, 0.7f);

        // armas (reglas de exclusión aplicarán dentro de SetByName)
        if (UnityEngine.Random.value < 0.25f)
            Randomize(PartCategory.Bow);
        else
        {
            Randomize(PartCategory.Ohs, 0.4f);
            if (!idx.ContainsKey(PartCategory.Ohs)) Randomize(PartCategory.ShieldR, 0.5f);
        }
    }

    public void SetByIndex(PartCategory cat, int i)
    {
        if (!parts.TryGetValue(cat, out var list) || list.Count == 0) return;
        i = Mathf.Clamp(i, 0, list.Count - 1);
        SetByName(cat, list[i].name);
    }
    
    public void SetByName(PartCategory cat, string nameOrNull)
{
    if (!parts.TryGetValue(cat, out var list) || list.Count == 0) return;

    // apaga TODO lo de la categoría actual
    foreach (var go in list) go.SetActive(false);

    // si piden "None"
    if (string.IsNullOrEmpty(nameOrNull))
    {
        idx.Remove(cat);

        // coherencia con Bow -> apaga flechas
        if (cat == PartCategory.Bow) SetByName(PartCategory.Arrows, null);
        return;
    }

    // encuentra el índice del elegido
    int i = list.FindIndex(g => g.name.Equals(nameOrNull, System.StringComparison.OrdinalIgnoreCase));
    if (i < 0) { idx.Remove(cat); return; }

    var chosen = list[i];

    // ------------------ REGLAS ESPECIALES ------------------

    // 1) Exclusión dura: Hair y Hat no pueden coexistir
    //    (se ejecuta SIEMPRE que activas uno u otro)
    if (cat == PartCategory.Hat)       TurnOffCategory(PartCategory.Hair);
    else if (cat == PartCategory.Hair) TurnOffCategory(PartCategory.Hat);

    // 2) Reglas por armas / manos (exclusividad por mano y Bow usa ambas)
    if (WeaponCats.Contains(cat) || cat == PartCategory.Arrows)
    {
        var h = handOf.TryGetValue(chosen, out var hh) ? hh : Hand.None;

        if (cat == PartCategory.Bow)
        {
            // Bow ocupa ambas manos: apaga todo en ambas y enciende flechas der.
            TurnOffAllWeapons(Hand.Left);
            TurnOffAllWeapons(Hand.Right);
            EnsureAncestorsActive(chosen.transform);
            chosen.SetActive(true);
            idx[cat] = i;
            AutoEnableArrowsRight();
            return;
        }

        if (cat == PartCategory.Ohs || cat == PartCategory.ShieldR || cat == PartCategory.Arrows)
        {
            // exclusividad dentro de la mano de ese objeto
            TurnOffAllWeapons(h);
        }
    }

    // -------------------------------------------------------

    // Asegura ancestros encendidos y activa la pieza
    EnsureAncestorsActive(chosen.transform);
    chosen.SetActive(true);
    idx[cat] = i;
    
    // Desactivar animaciones en armas
    if (WeaponCats.Contains(cat) || cat == PartCategory.Arrows)
    {
        DisableAnimators(chosen);
    }

    // Consistencia: si enciendo OHS o Shield, apaga Bow/Arrows
    if (cat == PartCategory.Ohs || cat == PartCategory.ShieldR)
    {
        SetByName(PartCategory.Bow, null);
        SetByName(PartCategory.Arrows, null);
    }
}


    void EnsureAncestorsActive(Transform t)
    {
        while (t != null && t != transform)
        {
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    void TurnOffAllWeapons(Hand h)
    {
        foreach (var cat in WeaponCats.Concat(new[] { PartCategory.Arrows }))
        {
            if (!parts.TryGetValue(cat, out var list)) continue;
            for (int k = 0; k < list.Count; k++)
            {
                var go = list[k];
                if (handOf.TryGetValue(go, out var hh) && (h == Hand.None || hh == h))
                {
                    if (go.activeSelf) go.SetActive(false);
                    if (idx.TryGetValue(cat, out var activeIdx) && list[activeIdx] == go)
                        idx.Remove(cat);
                }
            }
        }
    }

    void AutoEnableArrowsRight()
    {
        if (!parts.TryGetValue(PartCategory.Arrows, out var arrows)) return;
        foreach (var a in arrows) a.SetActive(false);

        var right = arrows.FirstOrDefault(a => handOf.TryGetValue(a, out var h) && h == Hand.Right);
        if (right != null)
        {
            EnsureAncestorsActive(right.transform);
            right.SetActive(true);
            DisableAnimators(right);
            idx[PartCategory.Arrows] = arrows.IndexOf(right);
        }
    }
    
    void DisableAnimators(GameObject go)
    {
        // Desactivar todos los Animators en el arma y sus hijos
        var animators = go.GetComponentsInChildren<Animator>(true);
        foreach (var anim in animators)
        {
            anim.enabled = false;
        }
    }

    // -------- utilidades de depuración --------
    [ContextMenu("Debug/Print Current State")]
    void DebugCurrentState()
    {
        Debug.Log("=== CURRENT STATE ===");
        Debug.Log($"Cached categories: {parts.Count}");
        Debug.Log($"Active selections: {idx.Count}");
        
        foreach (var cat in Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>())
        {
            if (parts.TryGetValue(cat, out var list) && list.Count > 0)
            {
                Debug.Log($"\n{cat} - {list.Count} total parts:");
                foreach (var go in list)
                {
                    bool isActive = go.activeSelf;
                    bool isSelected = idx.TryGetValue(cat, out var selIdx) && list[selIdx] == go;
                    Debug.Log($"  {go.name}: active={isActive}, selected={isSelected}");
                }
            }
        }
    }
    
    [ContextMenu("Debug/Print active Heads")]
    void DebugHeads()
    {
        var trs = GetComponentsInChildren<Transform>(true);
        foreach (var t in trs)
        {
            if (t != transform && t.name.StartsWith("Head", StringComparison.OrdinalIgnoreCase))
            {
                var r = t.GetComponent<Renderer>();
                Debug.Log($"Head {t.name} | activeSelf={t.gameObject.activeSelf} | rend={(r!=null)} | layer={t.gameObject.layer}");
            }
        }
    }
    
    [ContextMenu("Debug/Print active Weapons")]
    void DebugWeapons()
    {
        Debug.Log("=== WEAPON DEBUG START ===");
        
        // Mostrar qué hay en caché
        foreach (var cat in WeaponCats.Concat(new[] { PartCategory.Arrows }))
        {
            if (parts.TryGetValue(cat, out var list))
            {
                Debug.Log($"Category {cat}: {list.Count} items cached");
                foreach (var go in list)
                {
                    var hand = handOf.TryGetValue(go, out var h) ? h : Hand.None;
                    Debug.Log($"  - {go.name} | hand={hand}");
                }
            }
        }
        
        Debug.Log("\n=== ACTIVE WEAPONS IN HIERARCHY ===");
        var trs = GetComponentsInChildren<Transform>(true);
        foreach (var t in trs)
        {
            if (t == transform) continue;
            bool isWeapon = t.name.StartsWith("Bow", StringComparison.OrdinalIgnoreCase) ||
                           t.name.StartsWith("OHS", StringComparison.OrdinalIgnoreCase) ||
                           t.name.StartsWith("Shield", StringComparison.OrdinalIgnoreCase) ||
                           t.name.StartsWith("Arrows", StringComparison.OrdinalIgnoreCase);
            if (isWeapon)
            {
                var r = t.GetComponent<Renderer>();
                var rends = t.GetComponentsInChildren<Renderer>(true);
                var path = GetPath(t);
                Debug.Log($"Weapon {t.name} | activeSelf={t.gameObject.activeSelf} | activeInHierarchy={t.gameObject.activeInHierarchy} | hasRenderer={r!=null} | childRenderers={rends.Length} | layer={LayerMask.LayerToName(t.gameObject.layer)} | path={path}");
            }
        }
        
        Debug.Log("\n=== CURRENT SELECTION ===");
        foreach (var cat in WeaponCats.Concat(new[] { PartCategory.Arrows }))
        {
            if (idx.TryGetValue(cat, out var i) && parts.TryGetValue(cat, out var list) && i < list.Count)
            {
                Debug.Log($"{cat}: {list[i].name} (index {i})");
            }
            else
            {
                Debug.Log($"{cat}: NONE");
            }
        }
    }
    
    [ContextMenu("Debug/Print all cached parts")]
    void DebugAllParts()
    {
        Debug.Log("=== ALL CACHED PARTS ===");
        foreach (var cat in Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>())
        {
            if (parts.TryGetValue(cat, out var list) && list.Count > 0)
            {
                Debug.Log($"\n{cat} ({list.Count} items):");
                foreach (var go in list)
                {
                    var path = GetPath(go.transform);
                    Debug.Log($"  - {go.name} | path={path}");
                }
            }
        }
    }
    
    void TurnOffCategory(PartCategory cat)
    {
        if (!parts.TryGetValue(cat, out var list)) return;
        foreach (var go in list) go.SetActive(false);
        idx.Remove(cat);
    }


}

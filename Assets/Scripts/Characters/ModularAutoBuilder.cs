using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class ModularAutoBuilder : MonoBehaviour
{
    // Prefijos -> categorías (case-insensitive)
    static readonly (string prefix, PartCat cat)[] Map = new (string, PartCat)[]
    {
        ("Body",     PartCat.Body),
        ("Cloak",    PartCat.Cloak),
        ("AC",       PartCat.Accessory),

        // OJO: Eyebrow antes que Eye para no colisionar
        ("Eyebrow",  PartCat.Eyebrow),
        ("Eye",      PartCat.Eyes),

        ("Mouth",    PartCat.Mouth),
        ("Hair",     PartCat.Hair),
        ("Head",     PartCat.Head),
        ("Hat",      PartCat.Hat),

        // Armas
        ("Bow",      PartCat.Bow),
        ("OHS",      PartCat.OHS),
        ("Shield",   PartCat.Shield),
        ("Arrows",   PartCat.Arrows),
    };


    static readonly HashSet<PartCat> WeaponCats = new() { PartCat.Bow, PartCat.OHS, PartCat.Shield };

    // cache
    readonly Dictionary<PartCat, List<GameObject>> parts = new();
    readonly Dictionary<PartCat, int> idx = new();

    // Use central Hand enum from Identifiers.cs (None, Left, Right)
    readonly Dictionary<GameObject, Hand> handOf = new();

    [Header("Opcional")]
    public bool randomizeAtAwake = false;      // déjalo en false hasta que veas todo ok
    public bool ensureDefaultsAtStart = true;  // fuerza Body/Head/Eyes/Mouth si no hay selección

    void Awake()
    {
        CacheAll();
        DeactivateAll();
        if (randomizeAtAwake) RandomizeAll();
    }

    void Start()
    {
        // Asegura que holders típicos estén activos (por si el prefab los trae apagados)
        EnsureHolderActive("head");
        EnsureHolderActive("weapon_l");
        EnsureHolderActive("weapon_r");

        if (ensureDefaultsAtStart)
            EnsureDefaults();
    }

    // ---------- Construcción de caché ----------
    void CacheAll()
    {
        parts.Clear();
        handOf.Clear();
        foreach (var c in Enum.GetValues(typeof(PartCat)).Cast<PartCat>())
            parts[c] = new List<GameObject>();

        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == transform) continue;

            PartCat? maybe = null;
            foreach (var m in Map)
                if (t.name.StartsWith(m.prefix, StringComparison.OrdinalIgnoreCase)) { maybe = m.cat; break; }
            if (maybe == null) continue;

            var go = t.gameObject;
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

    void EnsureDefaults()
    {
        TryEnsureOne(PartCat.Body);
        TryEnsureOne(PartCat.Head);
        TryEnsureOne(PartCat.Eyes);
        TryEnsureOne(PartCat.Mouth);
    }

    void TryEnsureOne(PartCat cat)
    {
        var opts = GetOptions(cat);
        if (opts != null && opts.Count > 0)
        {
            // si no hay ya algo activo, enciende el primero
            if (!idx.ContainsKey(cat)) SetByIndex(cat, 0);
        }
    }

    // ---------- API pública ----------
    public IReadOnlyList<string> GetOptions(PartCat cat) =>
        parts.TryGetValue(cat, out var list) ? list.Select(g => g.name).ToList() : Array.Empty<string>();

    public Dictionary<PartCat, string> GetSelection()
    {
        var dict = new Dictionary<PartCat, string>();
        foreach (var kv in idx)
        {
            var list = parts[kv.Key];
            if (kv.Value >= 0 && kv.Value < list.Count)
                dict[kv.Key] = list[kv.Value].name;
        }
        return dict;
    }

    public void ApplySelection(Dictionary<PartCat, string> sel)
    {
        // aplica Bow primero por su dependencia con Arrows
        if (sel.TryGetValue(PartCat.Bow, out var bow)) SetByName(PartCat.Bow, bow);
        foreach (var kv in sel)
        {
            if (kv.Key == PartCat.Bow) continue;
            SetByName(kv.Key, kv.Value);
        }
    }

    public void Next(PartCat cat, int step = 1)
    {
        if (!parts.TryGetValue(cat, out var list) || list.Count == 0) return;
        int cur = idx.TryGetValue(cat, out var v) ? v : -1;
        cur = (cur + step + list.Count) % list.Count;
        SetByIndex(cat, cur);
    }
    public void Prev(PartCat cat) => Next(cat, -1);

    public void Randomize(PartCat cat, float noneChance = 0f)
    {
        if (!parts.TryGetValue(cat, out var list) || list.Count == 0) return;
        if (noneChance > 0f && UnityEngine.Random.value < noneChance) { SetByName(cat, null); return; }
        int i = UnityEngine.Random.Range(0, list.Count);
        SetByIndex(cat, i);
    }

    public void RandomizeAll()
    {
        Randomize(PartCat.Body);
        Randomize(PartCat.Cloak, 0.5f);
        Randomize(PartCat.Head);
        Randomize(PartCat.Hair, 0.2f);
        Randomize(PartCat.Eyes);
        Randomize(PartCat.Mouth);
        Randomize(PartCat.Hat, 0.6f);
        Randomize(PartCat.Eyebrow, 0.3f);
        Randomize(PartCat.Accessory, 0.7f);

        // armas (reglas de exclusión aplicarán dentro de SetByName)
        if (UnityEngine.Random.value < 0.25f)
            Randomize(PartCat.Bow);
        else
        {
            Randomize(PartCat.OHS, 0.4f);
            if (!idx.ContainsKey(PartCat.OHS)) Randomize(PartCat.Shield, 0.5f);
        }
    }

    public void SetByIndex(PartCat cat, int i)
    {
        if (!parts.TryGetValue(cat, out var list) || list.Count == 0) return;
        i = Mathf.Clamp(i, 0, list.Count - 1);
        SetByName(cat, list[i].name);
    }
    
    public void SetByName(PartCat cat, string nameOrNull)
{
    if (!parts.TryGetValue(cat, out var list) || list.Count == 0) return;

    // apaga TODO lo de la categoría actual
    foreach (var go in list) go.SetActive(false);

    // si piden "None"
    if (string.IsNullOrEmpty(nameOrNull))
    {
        idx.Remove(cat);

        // coherencia con Bow -> apaga flechas
        if (cat == PartCat.Bow) SetByName(PartCat.Arrows, null);
        return;
    }

    // encuentra el índice del elegido
    int i = list.FindIndex(g => g.name.Equals(nameOrNull, System.StringComparison.OrdinalIgnoreCase));
    if (i < 0) { idx.Remove(cat); return; }

    var chosen = list[i];

    // ------------------ REGLAS ESPECIALES ------------------

    // 1) Exclusión dura: Hair y Hat no pueden coexistir
    //    (se ejecuta SIEMPRE que activas uno u otro)
    if (cat == PartCat.Hat)       TurnOffCategory(PartCat.Hair);
    else if (cat == PartCat.Hair) TurnOffCategory(PartCat.Hat);

    // 2) Reglas por armas / manos (exclusividad por mano y Bow usa ambas)
    if (WeaponCats.Contains(cat) || cat == PartCat.Arrows)
    {
        var h = handOf.TryGetValue(chosen, out var hh) ? hh : Hand.None;

        if (cat == PartCat.Bow)
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

        if (cat == PartCat.OHS || cat == PartCat.Shield || cat == PartCat.Arrows)
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

    // Consistencia: si enciendo OHS o Shield, apaga Bow/Arrows
    if (cat == PartCat.OHS || cat == PartCat.Shield)
    {
        SetByName(PartCat.Bow, null);
        SetByName(PartCat.Arrows, null);
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
        foreach (var cat in WeaponCats.Concat(new[] { PartCat.Arrows }))
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
        if (!parts.TryGetValue(PartCat.Arrows, out var arrows)) return;
        foreach (var a in arrows) a.SetActive(false);

        var right = arrows.FirstOrDefault(a => handOf.TryGetValue(a, out var h) && h == Hand.Right);
        if (right != null)
        {
            EnsureAncestorsActive(right.transform);
            right.SetActive(true);
            idx[PartCat.Arrows] = arrows.IndexOf(right);
        }
    }

    // -------- utilidades de depuración --------
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
    
    void TurnOffCategory(PartCat cat)
    {
        if (!parts.TryGetValue(cat, out var list)) return;
        foreach (var go in list) go.SetActive(false);
        idx.Remove(cat);
    }


}

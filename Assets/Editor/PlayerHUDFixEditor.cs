using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace Editor
{
    /// <summary>
    /// Herramienta de editor para corregir rápidamente la alineación/pivote/tamaño de textos
    /// dentro de PlayerHUD_Main. No requiere TextMeshPro como referencia de compilación: usa reflection
    /// para soportarlo si está presente.
    /// Menu: Tools/PlayerHUD/Fix Alignment
    /// </summary>
    public static class PlayerHUDFixEditor
    {
        [MenuItem("Tools/PlayerHUD/Fix Alignment")]
        static void FixAlignment()
        {
            var main = GameObject.Find("PlayerHUD_Main");
            if (main == null)
            {
                Debug.LogWarning("PlayerHUDFix: No se encontró 'PlayerHUD_Main' en la escena.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(main, "Fix Player HUD Alignment");

            int changed = 0;

            // Ajustes para UnityEngine.UI.Text
            var texts = main.GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
            {
                var rt = t.rectTransform;
                t.alignment = TextAnchor.MiddleCenter;
                rt.pivot = new Vector2(0.5f, 0.5f);

                // Aseguramos un ancho mínimo para que el centrado sea visible
                if (Mathf.Abs(rt.sizeDelta.x) < 60f)
                {
                    rt.sizeDelta = new Vector2(Mathf.Max(300f, rt.sizeDelta.x), rt.sizeDelta.y == 0 ? 30f : rt.sizeDelta.y);
                }

                EditorUtility.SetDirty(t);
                changed++;
            }

            // Ajustes para TextMeshPro (si exista) usando reflection para evitar dependencia de compilación
            var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            if (tmpType != null)
            {
                var tmpComponents = main.GetComponentsInChildren(tmpType, true);
                if (tmpComponents != null)
                {
                    foreach (Component component in tmpComponents)
                    {
                        if (component == null) continue;

                        // Intentamos establecer alignment = Center
                        var propAlign = tmpType.GetProperty("alignment");
                        if (propAlign != null)
                        {
                            var enumType = Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
                            if (enumType != null)
                            {
                                try
                                {
                                    var centerVal = Enum.Parse(enumType, "Center");
                                    propAlign.SetValue(component, centerVal);
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"PlayerHUDFix: no se pudo ajustar alignment TMP: {e.Message}");
                                }
                            }
                        }

                        // Ajustamos rectTransform
                        var rtProp = tmpType.GetProperty("rectTransform");
                        if (rtProp != null)
                        {
                            var rtObj = rtProp.GetValue(component) as RectTransform;
                            if (rtObj != null)
                            {
                                rtObj.pivot = new Vector2(0.5f, 0.5f);
                                if (Mathf.Abs(rtObj.sizeDelta.x) < 60f)
                                {
                                    rtObj.sizeDelta = new Vector2(Mathf.Max(300f, rtObj.sizeDelta.x), rtObj.sizeDelta.y == 0 ? 30f : rtObj.sizeDelta.y);
                                }
                            }
                        }

                        EditorUtility.SetDirty(component);
                        changed++;
                    }
                }
            }

            EditorUtility.SetDirty(main);

            // Reorganizar botones de slots (X = izquierda, Y = centro, B = derecha)
            FixSlotButtons(main, ref changed);

            Debug.Log($"PlayerHUDFix: alineados {changed} componentes de texto en 'PlayerHUD_Main'.");
        }

        // Método para detectar y arreglar los botones de los slots
        static void FixSlotButtons(GameObject main, ref int changedCounter)
        {
            var allButtons = main.GetComponentsInChildren<Button>(true);
            if (allButtons == null || allButtons.Length == 0) return;

            // Buscar grupos de botones que parezcan ser los slots: preferimos padres que contengan "slot" en su nombre
            System.Collections.Generic.List<Button> group = null;
            foreach (var b in allButtons)
            {
                var parent = b.transform.parent;
                if (parent != null && parent.name.ToLowerInvariant().Contains("slot"))
                {
                    // tomar todos los botones bajo este padre
                    var buttons = parent.GetComponentsInChildren<Button>(false);
                    if (buttons.Length >= 3)
                    {
                        group = new System.Collections.Generic.List<Button>(buttons);
                        break;
                    }
                }
            }

            // Si no encontramos por nombre, buscar cualquier padre con al menos 3 botones
            if (group == null)
            {
                var groups = new System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.List<Button>>();
                foreach (var b in allButtons)
                {
                    var p = b.transform.parent;
                    if (p == null) continue;
                    if (!groups.ContainsKey(p)) groups[p] = new System.Collections.Generic.List<Button>();
                    groups[p].Add(b);
                }

                foreach (var kv in groups)
                {
                    if (kv.Value.Count >= 3)
                    {
                        group = kv.Value;
                        break;
                    }
                }
            }

            if (group == null || group.Count < 3)
            {
                // Como último recurso, tomar los primeros 3 botones en todo PlayerHUD_Main
                if (allButtons.Length >= 3)
                {
                    group = new System.Collections.Generic.List<Button> { allButtons[0], allButtons[1], allButtons[2] };
                }
                else return;
            }

            // Nos quedamos con exactamente 3 botones (si hay más, prioritizamos los que tengan label X/Y/B)
            // Intentar mapear por texto interior: buscar Text o TextMeshPro child que contenga X, Y o B
            var mapping = new System.Collections.Generic.Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

            foreach (var b in group)
            {
                string label = null;
                // comprobar UnityEngine.UI.Text en hijos
                var t = b.GetComponentInChildren<Text>(true);
                if (t != null) label = t.text?.Trim();

                // si no hay Text, intentar TMP vía reflection
                if (string.IsNullOrEmpty(label))
                {
                    var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                    if (tmpType != null)
                    {
                        var tmp = b.GetComponentInChildren(tmpType, true);
                        if (tmp != null)
                        {
                            var propText = tmpType.GetProperty("text");
                            if (propText != null)
                            {
                                try { label = (propText.GetValue(tmp) as string)?.Trim(); }
                                catch (Exception e) { Debug.LogWarning($"PlayerHUDFix: error leyendo texto TMP: {e.Message}"); }
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(label))
                {
                    // Normalizar a un solo carácter si viene así
                    var c = label.Length > 0 ? label[0].ToString().ToUpperInvariant() : label.ToUpperInvariant();
                    if (c == "X" || c == "Y" || c == "B") mapping[c] = b;
                }
            }

            // Si encontramos X/Y/B explícitos, construir el orden X,Y,B
            var finalOrder = new System.Collections.Generic.List<Button>();
            if (mapping.ContainsKey("X") && mapping.ContainsKey("Y") && mapping.ContainsKey("B"))
            {
                finalOrder = new System.Collections.Generic.List<Button> { mapping["X"], mapping["Y"], mapping["B"] };
            }
            else
            {
                // Si no, intentar ordenar por posición X y tomar los 3 más a la izquierda->derecha
                group.Sort((a, b) => a.GetComponent<RectTransform>().anchoredPosition.x.CompareTo(b.GetComponent<RectTransform>().anchoredPosition.x));
                // si hay más de 3, elegir 3 centrados
                if (group.Count == 3) finalOrder.AddRange(group);
                else if (group.Count > 3)
                {
                    // escoger los 3 que estén en el medio según sibling index
                    group.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
                    finalOrder.Add(group[0]);
                    finalOrder.Add(group[group.Count / 2]);
                    finalOrder.Add(group[group.Count - 1]);
                }
                else
                {
                    // relleno simple: los 3 primeros
                    for (int i = 0; i < Math.Min(3, group.Count); i++) finalOrder.Add(group[i]);
                }
            }

            if (finalOrder.Count < 3) return;

            // Determinar referencias de posición: usar el padre de los botones si comparten padre
            RectTransform parentRect = finalOrder[0].transform.parent as RectTransform;
            if (parentRect == null)
            {
                // si no tienen padre rect transform, usar main
                parentRect = main.GetComponent<RectTransform>();
            }

            float parentWidth = (parentRect != null && parentRect.rect.width > 0) ? parentRect.rect.width : 300f;
            float spacing = parentWidth * 0.22f;
            float left = -spacing;
            float center = 0f;
            float right = spacing;

            // Registrar undo para todos los botones implicados
            var objs = finalOrder.ConvertAll(b => (UnityEngine.Object)b.gameObject).ToArray();
            Undo.RegisterCompleteObjectUndo(objs, "Fix Slot Buttons");

            for (int i = 0; i < 3; i++)
            {
                var btn = finalOrder[i];
                var rt = btn.GetComponent<RectTransform>();
                if (rt == null) continue;

                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchorMin = new Vector2(0.5f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(0.5f, rt.anchorMax.y);

                var y = rt.anchoredPosition.y;
                float x = i == 0 ? left : i == 1 ? center : right;
                rt.anchoredPosition = new Vector2(x, y);

                // Asegurar tamaño razonable
                if (Mathf.Abs(rt.sizeDelta.x) < 10f) rt.sizeDelta = new Vector2(64f, rt.sizeDelta.y == 0 ? 64f : rt.sizeDelta.y);
                if (Mathf.Abs(rt.sizeDelta.y) < 10f) rt.sizeDelta = new Vector2(rt.sizeDelta.x, 64f);

                EditorUtility.SetDirty(btn);
                changedCounter++;
            }

            Debug.Log($"PlayerHUDFix: colocados botones X/Y/B en '{parentRect?.name ?? main.name}'.");
        }
    }
}

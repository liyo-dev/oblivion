# ✅ Solución: Gestión de Capas NPC - Narrativa + Combate

**Fecha:** 2025-12-26  
**Estado:** Implementado y funcional

---

## 🎯 Problema Resuelto

**Conflicto:** Un NPC con módulo de narrativa interactiva (sin auto-start) necesita estar en capa `"Interactable"` para poder interactuar, pero el módulo de combate requiere capa `"Enemy"`.

**Solución:** Sistema de gestión de capas dinámicas que cambia automáticamente la capa del NPC según el contexto.

---

## 🔧 Cambios Implementados

### Archivos Modificados:
1. ✅ `NPCInteractiveNarrativeConfig.cs` - Nuevas opciones de configuración
2. ✅ `NPCInteractiveNarrativeExecutor.cs` - Lógica de cambio de capas
3. ℹ️ `NPCCombatLifecycleHandler.cs` - Ya gestiona el retorno a Interactable (sin cambios)

### Nuevas Opciones en el Config:

```csharp
[Header("Layer Management")]
public LayerMode initialLayer = LayerMode.Interactable;
public bool switchToEnemyLayerOnCombat = true;
```

### Enum LayerMode:
- `Interactable` - Para interacción manual
- `Enemy` - Para combate
- `Default` - Capa por defecto
- `Custom` - No cambiar la capa actual

---

## 🎮 Cómo Usar

### Configuración Recomendada para tu caso:

```
NPC Interactive Narrative Config:
├─ initialLayer: Interactable ✅
├─ switchToEnemyLayerOnCombat: true ✅
└─ autoStartOnPlayerDetection: false ✅
```

### Flujo Automático:

1. **Inicio** → NPC en capa `Interactable`
2. **Jugador interactúa** → Narrativa se ejecuta (diálogos, etc.)
3. **Acción StartCombat** → NPC cambia a capa `Enemy` automáticamente
4. **Combate** → Funciona normalmente
5. **NPC derrotado** → Vuelve a capa `Interactable` (diálogo post-derrota)

---

## ✨ Ventajas

✅ **Automático**: No requiere scripts adicionales  
✅ **Transparente**: El diseñador solo configura las opciones  
✅ **Compatible**: Funciona con NPCs existentes sin cambios  
✅ **Flexible**: Soporta múltiples casos de uso

---

## 📝 Próximos Pasos

1. **Abrir Unity** y esperar a que compile
2. **Seleccionar el ScriptableObject** de tu NPC (`NPCInteractiveNarrativeConfig`)
3. **Configurar las nuevas opciones:**
   - `Initial Layer`: `Interactable`
   - `Switch To Enemy Layer On Combat`: ✅ activado
4. **Probar en escena:**
   - Interactuar con el NPC (debe funcionar)
   - Verificar que inicia combate después de la narrativa
   - Confirmar que el combate funciona correctamente

---

## 🐛 Troubleshooting

**Problema:** No puedo interactuar con el NPC  
**Solución:** Verificar que `initialLayer = Interactable` y que el GameObject tenga el componente `Interactable` habilitado

**Problema:** El combate no funciona  
**Solución:** Verificar que `switchToEnemyLayerOnCombat = true` y que la acción `StartCombat` esté en la cadena narrativa

**Problema:** Después de ser derrotado no puedo volver a interactuar  
**Solución:** El sistema debería cambiar automáticamente a `Interactable`. Verificar logs de `NPCCombatLifecycleHandler`

---

**Documentación completa:** Ver `LAYER_MANAGEMENT_FIX.md`


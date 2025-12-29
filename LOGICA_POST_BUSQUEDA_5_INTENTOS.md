# ✅ QUÉ PASA DESPUÉS DE LOS 5 INTENTOS DE BÚSQUEDA

## 🎯 Flujo Completo de Búsqueda

### Fase 1: Búsqueda Activa (Hasta 5 Intentos) ✅

```
Player se esconde
    ↓
❓ Interrogación #1 + Animación
    ↓
NPC busca en punto #1 → DETIENE → ❓ + Animación
    ↓
NPC busca en punto #2 → DETIENE → ❓ + Animación
    ↓
NPC busca en punto #3 → DETIENE → ❓ + Animación
    ↓
NPC busca en punto #4 → DETIENE → ❓ + Animación
    ↓
NPC busca en punto #5 → DETIENE → ❓ + Animación
    ↓
[NO ENCONTRÓ AL JUGADOR]
    ↓
⬇️ FASE 2 ⬇️
```

### Fase 2: Después de 5 Intentos Fallidos ✅

Hay **2 opciones configurables** en `NPCCombatConfig`:

## OPCIÓN A: Volver al Origen (returnToOriginAfterSearch = TRUE) ✅

```
Búsqueda agotada (5 intentos)
    ↓
Log: "😞 Búsqueda agotada - 5 intentos completados sin éxito"
    ↓
Ocultar icono ❓
    ↓
Log: "🏠 Volviendo al origen tras búsqueda fallida"
    ↓
NPC camina de regreso a su posición inicial
    ↓
[DURANTE EL REGRESO]
    ↓
¿Ve al jugador?
    │
    ├─ SÍ → ❗ Admiración + Retomar combate inmediatamente
    │
    └─ NO → Continúa hasta llegar al origen
            ↓
            LLEGA AL ORIGEN
            ↓
            Log: "✅ Regresó al origen - Saliendo del modo combate"
            ↓
            Abandona modo combate
            ↓
            Vuelve a IdleState/PatrolState
```

### Características del Regreso al Origen:

✅ **Camina hacia su posición inicial** (donde estaba al inicio del combate)
✅ **Verifica constantemente** si ve al jugador durante el regreso
✅ **Si lo ve** → Retoma combate inmediatamente
✅ **Si llega sin verlo** → Sale de combate y vuelve a su rutina normal

## OPCIÓN B: Abandonar Directamente (returnToOriginAfterSearch = FALSE) ✅

```
Búsqueda agotada (5 intentos)
    ↓
Log: "😞 Búsqueda agotada - 5 intentos completados sin éxito"
    ↓
Ocultar icono ❓
    ↓
Log: "🚫 No vuelve al origen - Abandonando combate"
    ↓
Log: "🏳️ Abandonando modo combate"
    ↓
Sale del modo combate INMEDIATAMENTE
    ↓
Se queda donde está
    ↓
Vuelve a IdleState/PatrolState
```

### Características del Abandono Directo:

✅ **No regresa** a la posición inicial
✅ **Se queda** en la última posición de búsqueda
✅ **Sale de combate** inmediatamente
✅ **Vuelve a su rutina** (idle, patrulla, etc.)

## 📊 Configuración en NPCCombatConfig

```
Search Behavior:
├── Actively Search For Player: ✓ (busca activamente)
├── Search Duration: 15s (tiempo máximo de búsqueda)
├── Search Movement Radius: 5m (radio de búsqueda)
└── Return To Origin After Search: ✓ o ✗
    │
    ├─ TRUE (✓) → Vuelve al origen después de buscar
    └─ FALSE (✗) → Se queda donde está y abandona
```

## 🎮 Logs Esperados - Escenario Completo

### Escenario A: Vuelve al Origen

```
[CombatBrain:Boy_Pirate] 🔍 INICIANDO BÚSQUEDA
[NPCAlertIcon:Boy_Pirate] ❓ Mostrando icono de interrogación

[CombatBrain:Boy_Pirate] 👣 Movimiento de búsqueda #1 hacia: (10, 0, 5)
[CombatBrain:Boy_Pirate] ❓ Parada de búsqueda #1 - No encontrado

[CombatBrain:Boy_Pirate] 👣 Movimiento de búsqueda #2 hacia: (12, 0, 8)
[CombatBrain:Boy_Pirate] ❓ Parada de búsqueda #2 - No encontrado

[CombatBrain:Boy_Pirate] 👣 Movimiento de búsqueda #3 hacia: (8, 0, 10)
[CombatBrain:Boy_Pirate] ❓ Parada de búsqueda #3 - No encontrado

[CombatBrain:Boy_Pirate] 👣 Movimiento de búsqueda #4 hacia: (15, 0, 7)
[CombatBrain:Boy_Pirate] ❓ Parada de búsqueda #4 - No encontrado

[CombatBrain:Boy_Pirate] 👣 Movimiento de búsqueda #5 hacia: (6, 0, 12)
[CombatBrain:Boy_Pirate] ❓ Parada de búsqueda #5 - No encontrado

// ✅ DESPUÉS DE 5 INTENTOS:
[CombatBrain:Boy_Pirate] 😞 Búsqueda agotada - 5 intentos completados sin éxito
[CombatBrain:Boy_Pirate] 🏠 Volviendo al origen tras búsqueda fallida: (5, 0, 5)
... NPC caminando de regreso ...
[CombatBrain:Boy_Pirate] ✅ Regresó al origen - Saliendo del modo combate
[CombatBrain:Boy_Pirate] 🏳️ Abandonando modo combate - Jugador no encontrado
[NPC:Boy_Pirate] [Dead] OnExit
[NPC:Boy_Pirate] [Idle] OnEnter
```

### Escenario B: No Vuelve al Origen

```
[CombatBrain:Boy_Pirate] 🔍 INICIANDO BÚSQUEDA
... 5 intentos de búsqueda ...
[CombatBrain:Boy_Pirate] 😞 Búsqueda agotada - 5 intentos completados sin éxito
[CombatBrain:Boy_Pirate] 🚫 No vuelve al origen (returnToOriginAfterSearch = false)
[CombatBrain:Boy_Pirate] 🏳️ Abandonando modo combate - Jugador no encontrado
[NPC:Boy_Pirate] [Dead] OnExit
[NPC:Boy_Pirate] [Idle] OnEnter
```

### Escenario C: Encuentra al Jugador Durante el Regreso

```
... 5 intentos de búsqueda ...
[CombatBrain:Boy_Pirate] 😞 Búsqueda agotada - 5 intentos completados sin éxito
[CombatBrain:Boy_Pirate] 🏠 Volviendo al origen: (5, 0, 5)
... NPC caminando de regreso ...
[CombatBrain:Boy_Pirate] ✅ ¡Jugador encontrado en el camino de regreso!
[NPCAlertIcon:Boy_Pirate] ❗ Mostrando icono de admiración (¡encontrado!)
[CombatBrain:Boy_Pirate] Estado: EVALUATE
... Retoma combate ...
```

## 🎯 Resumen de Estados

| Momento | Estado NPC | Icono | Comportamiento |
|---------|-----------|-------|----------------|
| Pérdida de visión | SEARCHING | ❓ | Inicia búsqueda |
| Intento 1-5 | SEARCHING | ❓ en cada parada | Busca activamente |
| Después de 5 intentos | SEARCHING → Regreso | Ninguno | Vuelve o abandona |
| Durante regreso | Movimiento | Ninguno | Verifica visión |
| Si encuentra en regreso | EVALUATE | ❗ | Retoma combate |
| Si no encuentra | Salida de combate | Ninguno | Idle/Patrol |

## ✅ Implementación Técnica

### En State_Searching():

```csharp
// Bucle de hasta 5 intentos
for (int i = 0; i < 5; i++)
{
    // Buscar en punto aleatorio
    // Si encuentra → Salir y retomar combate
}

// DESPUÉS DE 5 INTENTOS:

// Ocultar interrogación
_alertIconController.HideAlertIcon();

// ¿Volver al origen?
if (settings.returnToOriginAfterSearch)
{
    // Caminar de regreso
    while (regresando)
    {
        // Si ve al jugador → Retomar combate
    }
    // Si llega → Abandonar
}
else
{
    // Abandonar directamente
}

// Salir de combate
StopCombat();
_manager.Context.IsInCombat = false;
```

## 🎮 Recomendaciones de Configuración

### Para NPC Agresivos/Persistentes:
```
Actively Search For Player: TRUE
Search Duration: 20s
Return To Origin After Search: TRUE  ✓
```
**Resultado**: Busca exhaustivamente y vuelve a su puesto.

### Para NPC Cautelosos/Territoriales:
```
Actively Search For Player: TRUE
Search Duration: 15s
Return To Origin After Search: TRUE  ✓
```
**Resultado**: Busca, pero vuelve rápido a su zona.

### Para NPC Pasivos/Cobardes:
```
Actively Search For Player: FALSE
Passive Search Duration: 5s
Return To Origin After Search: TRUE  ✓
```
**Resultado**: Apenas busca, vuelve rápido a su origen.

### Para NPC Errantes/Patrulleros:
```
Actively Search For Player: TRUE
Search Duration: 10s
Return To Origin After Search: FALSE  ✗
```
**Resultado**: Busca pero se queda donde termina la búsqueda.

---

**Fecha**: 29 de diciembre de 2024  
**Estado**: ✅ COMPLETADO  
**Lógica**: 5 intentos → Volver al origen O Abandonar directamente  
**Configurable**: `returnToOriginAfterSearch` en NPCCombatConfig


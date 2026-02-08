# 🐛 BUG: Golem No Aparece - Estado Narrativo Bloqueado

**Fecha**: 2026-02-06  
**Estado**: ✅ DIAGNOSTICADO - Solución lista

---

## 🔍 DIAGNÓSTICO COMPLETO

### El Problema Real

El Golem **NO aparece** porque el **grafo narrativo está bloqueado** en un estado anterior. Los logs muestran:

```
[Signals] Custom: EXIT_FROM_WOODS_ESTELA (sin oyentes → pendiente)
[NarrativeGraphHub] ✅ Blackboard restaurado para grafo 'Historia Principal' | Nodo guardado: WaitCustomEventNode (78455749...)
```

**¿Qué significa esto?**

1. ✅ El trigger `EXIT_FROM_WOODS_ESTELA` **SÍ se dispara correctamente** cuando el player lo atraviesa
2. ✅ El evento se emite al sistema de señales narrativas
3. ❌ **PERO** el grafo está pausado esperando el evento `LETTER_START` (GUID: 78455749...)
4. ❌ El nodo que espera `EXIT_FROM_WOODS_ESTELA` **ni siquiera se ha alcanzado todavía**
5. ❌ Por lo tanto, el evento se guarda como "pendiente" pero **nunca se consumirá**

### Flujo Narrativo Correcto

El grafo `Historia Principal` tiene este orden de eventos:

```
1. ERIKA_FIGHT           → Pelea con Erika
2. ERIKA_BATTLE_WON      → Erika derrotada
3. LETTER_START          ← 🔴 EL GRAFO ESTÁ BLOQUEADO AQUÍ
4. ADD_CLOAK             → Añadir capa al inventario
5. ESTELA_FOUND          → Estela encontrada
6. EXIT_FROM_WOODS_ESTELA → Salida del bosque con Estela (INICIA GOLEM)
```

**Tu partida guardada está en un estado inconsistente:**
- ✅ Has completado misiones que requieren a Estela (ELDRAN_MISSION6)
- ✅ Estela está en tu party
- ❌ Pero el grafo narrativo sigue esperando `LETTER_START` (evento #3)
- ❌ Por eso cuando llegas al trigger del Golem, el grafo NO está escuchando

---

## ✅ SOLUCIONES

### Solución 1: Emitir Evento Manualmente (RECOMENDADO)

He creado un script de debug que te permite **emitir eventos manualmente** para desbloquear el grafo.

#### Pasos:

1. **Añadir el componente** `NarrativeEventDebugger` a cualquier GameObject de la escena `MainWorld` (puede ser un GameObject vacío llamado `[DEBUG]`)

2. **En el Inspector**, marca los checkboxes de los eventos que faltan en orden:
   - ✅ `emitLETTER_START`
   - ✅ `emitADD_CLOAK`
   - ✅ `emitESTELA_FOUND`

3. **En Play Mode**, los eventos se emitirán automáticamente y el grafo avanzará

4. **Verifica** que el grafo llegue al nodo correcto observando los logs:
   ```
   [WaitCustom] Recibido EXIT_FROM_WOODS_ESTELA
   ```

5. **Guarda la partida** para que el estado del grafo se persista correctamente

### Solución 2: Corregir el Preset Manualmente (AVANZADO)

Si prefieres corregir directamente el archivo de guardado:

1. Cerrar Unity

2. Abrir el archivo:
   ```
   Assets/Scripts/Player/SO/PlayerPreset_Runtime.asset
   ```

3. Buscar la sección `narrativeBlackboards:`

4. Encontrar el snapshot de `Historia Principal`

5. Cambiar el `currentNodeGuid` de:
   ```yaml
   currentNodeGuid: 78455749-f638-46de-a00c-c00cb1e61e5d
   ```
   
   A (nodo que espera EXIT_FROM_WOODS_ESTELA):
   ```yaml
   currentNodeGuid: 1db8d4ab-1b78-48f8-961e-9e0668aacf6e
   ```

6. Añadir las flags de eventos completados en `variables:`:
   ```yaml
   - key: __event_78455749-f638-46de-a00c-c00cb1e61e5d_LETTER_START_received
     boolValue: true
   - key: __event_[guid-del-nodo-ADD_CLOAK]_ADD_CLOAK_received
     boolValue: true
   - key: __event_[guid-del-nodo-ESTELA_FOUND]_ESTELA_FOUND_received
     boolValue: true
   ```

7. Guardar y abrir Unity

### Solución 3: Resetear el Grafo (ÚLTIMA OPCIÓN)

Si las soluciones anteriores no funcionan, puedes resetear completamente el grafo:

1. En el menú de Unity: `Tools → Narrative Graph → Reset All Graphs`

2. Esto reiniciará el grafo desde el principio, pero **perderás el progreso narrativo**

3. **Consecuencias:**
   - Tendrás que volver a jugar los diálogos principales
   - Las quests completadas se mantendrán
   - El inventario y party se mantendrán

---

## 🛠️ PREVENCIÓN: Cómo Evitar Este Problema

Este problema ocurre cuando:

1. **Usas presets de testeo** que dan acceso a Estela sin haber completado los eventos narrativos previos

2. **Guardas la partida** en modo testeo, creando un estado inconsistente

### Recomendación:

En `GameBootProfile.cs`, añadir validación que verifique que el estado del grafo es consistente con el party y las quests completadas:

```csharp
// Después de restaurar el grafo, verificar consistencia
if (HasPartyMember("Estela") && !HasCompletedEvent("ESTELA_FOUND"))
{
    Debug.LogWarning("⚠️ Estado inconsistente detectado: Estela en party pero evento no completado. Emitiendo eventos faltantes...");
    EmitEvent("LETTER_START");
    EmitEvent("ADD_CLOAK");
    EmitEvent("ESTELA_FOUND");
}
```

---

## 📊 Logs de Verificación

Para confirmar que el problema se ha resuelto, busca estos logs:

### ✅ Antes de la corrección:
```
[Signals] Custom: EXIT_FROM_WOODS_ESTELA (sin oyentes → pendiente)
[NarrativeGraphHub] Nodo guardado: WaitCustomEventNode (78455749...) ← LETTER_START
```

### ✅ Después de la corrección:
```
[Signals] Custom: LETTER_START
[WaitCustom] Recibido LETTER_START
[Signals] Custom: ADD_CLOAK
[WaitCustom] Recibido ADD_CLOAK
[Signals] Custom: ESTELA_FOUND
[WaitCustom] Recibido ESTELA_FOUND
[Signals] Custom: EXIT_FROM_WOODS_ESTELA
[WaitCustom] Recibido EXIT_FROM_WOODS_ESTELA
[StartBattleNode] Iniciando batalla: Golem_1
[BossArenaController] TriggerStartBattle llamado por narrativa
```

---

## 🎯 PARA IMPLEMENTAR LA SOLUCIÓN RÁPIDA

1. **Añade el componente `NarrativeEventDebugger`** a la escena

2. **En Play Mode**, marca en este orden (uno por uno, espera que se procese cada uno):
   - ✅ `emitLETTER_START` → Wait 1 segundo
   - ✅ `emitADD_CLOAK` → Wait 1 segundo
   - ✅ `emitESTELA_FOUND` → Wait 1 segundo

3. **Sal del bosque** con Estela (activa el trigger `EXIT_FROM_WOODS_ESTELA`)

4. **El Golem debería aparecer** 🎉

5. **Guarda la partida** para persistir el estado correcto

---

**Autor**: GitHub Copilot  
**Categoría**: Bug - Sistema Narrativo  
**Prioridad**: Alta

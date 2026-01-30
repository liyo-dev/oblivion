# 🧪 Pruebas del Sistema de Spawn de Party

## ✅ Cómo Probar el Sistema

### Prueba 1: Cargar Partida con Party
1. **Preparación:**
   - Inicia una partida nueva
   - Recluta uno o más NPCs al party
   - Guarda la partida en un SavePoint
   - Cierra el juego

2. **Ejecución:**
   - Inicia el juego
   - Carga la partida guardada

3. **Resultado Esperado:**
   ✅ El jugador aparece en el SavePoint
   ✅ Los NPCs del party aparecen cerca del SavePoint (1.5-2m de distancia)
   ✅ Los NPCs están en formación semicircular alrededor del anchor
   ✅ Los NPCs comienzan a seguir al jugador después de ~1 segundo

4. **Logs a Verificar en la Consola:**
   ```
   [PlayerParty] 🔄 Restaurando X miembros del party: [...]
   [PlayerParty] 🔄 Iniciando restauración de party - Período de gracia de 8s activado
   [PlayerParty] ⏳ Miembro se unió durante restauración, el posicionamiento se hará después
   [PlayerParty] 📍 Teletransportando X miembros al anchor 'SavePoint_01'
   [PlayerParty] 🔹 Teletransportando NPC_Name al anchor de ... a ...
   [PlayerParty] ✅ NPC_Name teletransportado al anchor - Distancia al jugador: X.Xm
   ```

---

### Prueba 2: NPC Se Une Durante Gameplay
1. **Preparación:**
   - Inicia una partida
   - Ten un NPC disponible para unirse al party

2. **Ejecución:**
   - Haz que el NPC se una al party (por diálogo o script)

3. **Resultado Esperado:**
   ✅ Si el NPC está cerca (<50m): Se queda donde está y comienza a seguir
   ✅ Si el NPC está lejos (>50m): Se teletransporta cerca del jugador
   ✅ NO debería usar el sistema de anchor en este caso

4. **Logs a Verificar:**
   ```
   [PlayerParty] ✅ NPC_Name está cerca (X.Xm), no requiere teletransporte
   // O
   [PlayerParty] 📍 NPC_Name está MUY lejos (X.Xm), teletransportando al punto de guardado...
   ```

---

### Prueba 3: Múltiples NPCs en el Party
1. **Preparación:**
   - Recluta 3-4 NPCs al party
   - Guarda la partida

2. **Ejecución:**
   - Carga la partida

3. **Resultado Esperado:**
   ✅ Todos los NPCs aparecen en formación alrededor del SavePoint
   ✅ Están distribuidos en semicírculo (ángulos: -60°, 0°, +60°, etc.)
   ✅ Distancias entre 1.5m y 2.3m del anchor
   ✅ Todos están en NavMesh válido

---

### Prueba 4: Anchor Sin NavMesh Cerca
1. **Preparación:**
   - Crea un SavePoint en una posición con poco NavMesh alrededor

2. **Ejecución:**
   - Guarda con party y carga

3. **Resultado Esperado:**
   ✅ Los NPCs usan el sistema de fallback
   ✅ Se posicionan en la posición del anchor o lo más cerca posible en NavMesh
   ✅ Logs de advertencia si no se encuentra NavMesh válido:
   ```
   [PlayerParty] ⚠️ Posición NavMesh demasiado lejos del anchor (X.Xm), usando fallback
   [PlayerParty] ✅ Fallback 1: Posición encontrada a X.Xm del anchor
   ```

---

### Prueba 5: Comportamiento Durante Gameplay
1. **Preparación:**
   - Carga partida con party
   - Espera a que los NPCs comiencen a seguir

2. **Ejecución:**
   - Aléjate mucho del party (>20m)
   - Observa si se teletransportan

3. **Resultado Esperado:**
   ✅ Los NPCs se teletransportan usando `TeleportMemberToPlayer()` (no al anchor)
   ✅ Se posicionan en formación relativa al jugador
   ✅ El sistema normal de seguimiento funciona correctamente

---

## 🐛 Problemas Potenciales y Soluciones

### Problema: NPCs no aparecen al cargar
**Posibles causas:**
- NPCs no están registrados en NPCRegistry
- IDs de persistencia incorrectos
- NPCs en escena diferente

**Solución:**
- Verifica logs: `[PlayerParty] ❌ No se encontró NPC con ID: '...'`
- Asegúrate de que los NPCs tienen `interactiveNarrativeConfig.persistenceId` configurado
- Revisa que los NPCs estén en la misma escena que el jugador

---

### Problema: NPCs aparecen lejos del jugador
**Posibles causas:**
- `TeleportAllMembersToCurrentAnchor()` no se está llamando
- El anchor actual no está configurado correctamente
- Sistema de fallback activado

**Solución:**
- Busca el log: `[PlayerParty] 📍 Teletransportando X miembros al anchor '...'`
- Si ves: `No hay anchor actual definido, usando posición del jugador`
  → Verifica que `SpawnManager.CurrentAnchorId` esté configurado
- Si ves warnings de NavMesh, revisa la geometría del nivel

---

### Problema: NPCs se teletransportan constantemente
**Posibles causas:**
- Período de gracia no está funcionando
- `_lastPartyRestoreTime` no se está actualizando

**Solución:**
- Verifica que ves el log: `Período de gracia de 8s activado`
- Si el problema persiste, aumenta `PARTY_RESTORE_GRACE_PERIOD` a 10 segundos
- Revisa que `_sceneLoadTime` se actualiza correctamente

---

## 📊 Métricas de Éxito

| Métrica | Valor Esperado | Cómo Verificar |
|---------|----------------|----------------|
| **Distancia al jugador** | 1.5m - 4m | Consola: `Distancia al jugador: X.Xm` |
| **Tiempo hasta seguir** | ~1-2 segundos | Observar comportamiento visual |
| **NPCs en NavMesh** | 100% | Sin errores de NavMesh en consola |
| **Formación correcta** | Semicírculo | Usar Gizmos en Scene view |
| **Sin teletransportes extra** | 0 en primeros 8s | Verificar logs de teletransporte |

---

## 🎯 Checklist de Verificación

Antes de dar por finalizado:
- [ ] Los NPCs aparecen cerca del SavePoint al cargar
- [ ] Los NPCs están en formación semicircular
- [ ] Los NPCs comienzan a seguir después de ~1 segundo
- [ ] No hay teletransportes extra en los primeros 8 segundos
- [ ] El sistema de seguimiento normal funciona durante gameplay
- [ ] Los NPCs que se unen durante gameplay usan el sistema anterior
- [ ] No hay errores en la consola relacionados con NavMesh
- [ ] Los logs muestran las llamadas correctas a `TeleportAllMembersToCurrentAnchor()`

---

## 📝 Notas Adicionales

### Configuración Recomendada
- `PARTY_RESTORE_GRACE_PERIOD`: 8 segundos (actual)
- `SCENE_LOAD_GRACE_PERIOD`: 3 segundos (actual)
- `MEMBER_JOIN_GRACE_PERIOD`: 5 segundos (actual)
- `teleportRadius`: 2m (en PlayerParty Inspector)
- `minTeleportDistance`: 1.5m (en PlayerParty Inspector)

### Debug Mode
Para ver todos los logs detallados, activa `debugMode = true` en:
- `PlayerParty` (Inspector)
- `NPCPartyMember` (Inspector de cada NPC)

Esto mostrará logs como:
```
[PlayerParty] 🔹 Teletransportando X de ... a ...
[PlayerParty] ✅ Posición de formación en anchor encontrada a X.Xm
[NPCPartyMember:NPC_Name] ⏳ Iniciando DelayedStartFollowingAfterJoin...
```

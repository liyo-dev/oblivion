# ✅ SISTEMA COMPLETO: Posicionamiento y Mirada Durante Diálogos

## 🎯 Implementación Completada

Sistema completo de posicionamiento cinematográfico para diálogos:

1. ✅ **Party members se posicionan a los lados del player** (izquierda/derecha configurables)
2. ✅ **Player mira automáticamente al NPC** al iniciar el diálogo
3. ✅ **Party members miran al NPC** (no al player)
4. ✅ **Dirección correcta**: "Derecha" es la derecha del player mirando al NPC

## 🎬 Resultado Visual

**Configuración**:
- Estela: `Right` (derecha del player)
- Aria: `Left` (izquierda del player)

**Antes**:
```
            PLAYER
              🧑
        ESTELA  ARIA
          🧝    🧙
                         ELDRAN
                           🤖
```

**Durante el diálogo** (todos mirando al NPC):
```
              ELDRAN
                🤖
                👁️ ← Todos miran aquí
               
    ARIA     PLAYER     ESTELA
     🧙        🧑         🧝
   (izq)               (der)
```

## 📋 Configuración en NPCPartyConfig

Abre cada `NPCPartyConfig` en el Inspector:

```
=== POSICIONAMIENTO EN DIÁLOGOS ===

✓ posicionarseDuranteDialogos: true

  Lado Preferido Dialogo: Right           ← Left o Right (del player mirando al NPC)
  Distancia Lateral Dialogo: 1.5          ← Metros de separación lateral
  Offset Delante Dialogo: -0.3            ← Negativo = atrás, positivo = adelante
  Tiempo Maximo Movimiento: 2.0           ← Segundos antes de teleport
```

### Ejemplo para Estela (derecha del player):
```
posicionarseDuranteDialogos: ✓ true
ladoPreferidoDialogo: Right              ← A la DERECHA del player
distanciaLateralDialogo: 1.5
offsetDelanteDialogo: -0.3               ← Ligeramente atrás
tiempoMaximoMovimientoDialogo: 2.0
```

## 🔧 Cómo Funciona Internamente

### 1. Al Iniciar el Diálogo

**DialogueManager.StartDialogue(asset, npc)**:
```csharp
// 1. Rotar player hacia el NPC
player.rotation = LookAt(npc)

// 2. Posicionar party members
PlayerParty.PositionMembersForDialogue(npc)
  ↓
  // Calcular la "derecha" del player mirando al NPC
  playerForward = (npc.position - player.position).normalized
  playerRight = Cross(up, playerForward)
  
  // Para cada member:
  if (lado == Right):
    posición = player.position + playerRight * distancia
  else:
    posición = player.position - playerRight * distancia
  
  // Mover member a esa posición
  member.MoveToDialoguePosition(posición, tiempo, npc)
```

### 2. Durante el Movimiento

**DialoguePositionState**:
- El party member camina hacia la posición calculada
- Usa el NavMesh para evitar obstáculos
- Si tarda más del tiempo máximo → teletransporte
- Al llegar → `LookAt(npc)` (NO al player)

### 3. Al Terminar el Diálogo

**DialogueManager.Close()**:
```csharp
PlayerParty.ReleaseDialoguePositioning()
  ↓
  // Cada member vuelve a FollowPlayerState
  member.ReleaseDialoguePosition()
```

## 🎮 Ejemplo Completo: Diálogo con Eldran

**Setup**:
- Eldran en posición (100, 0, 50)
- Player en posición (95, 0, 45)
- Estela (Right): distancia 1.5m
- Aria (Left): distancia 1.5m

**Cálculos**:
```
1. Player mira a Eldran:
   direction = (100, 0, 50) - (95, 0, 45) = (5, 0, 5)
   normalized = (0.707, 0, 0.707)
   player.forward = (0.707, 0, 0.707)

2. Derecha del player:
   playerRight = Cross((0,1,0), (0.707,0,0.707))
   playerRight = (0.707, 0, -0.707)

3. Posición de Estela (Right):
   pos = (95,0,45) + (0.707,0,-0.707) * 1.5 + (0.707,0,0.707) * -0.3
   pos ≈ (95.85, 0, 43.85)

4. Posición de Aria (Left):
   pos = (95,0,45) - (0.707,0,-0.707) * 1.5 + (0.707,0,0.707) * -0.3
   pos ≈ (94.15, 0, 46.15)

5. Ambas miran a Eldran:
   Estela.LookAt(100, 0, 50)
   Aria.LookAt(100, 0, 50)
```

**Resultado Visual**:
```
              ELDRAN (100, 0, 50)
                🤖
                ↑
         ┌──────┴──────┐
         │             │
    ARIA │    PLAYER   │ ESTELA
  (94,46)│   (95, 45)  │(96, 44)
     🧙  │      🧑     │  🧝
         └─────────────┘
```

## 📁 Archivos del Sistema

### Nuevos:
- **`DialoguePositionState.cs`** ⭐
  - Estado FSM para posicionamiento
  - Recibe el NPC target para la mirada
  - Maneja movimiento y llegada

### Modificados:

**`NPCPartyConfig.cs`**:
- 5 nuevos campos de configuración
- Enum `DialoguePositionSide` (Left/Right)

**`PlayerParty.cs`**:
- `PositionMembersForDialogue(npcTarget)` - Rota player + posiciona members
- `ReleaseDialoguePositioning()` - Libera al terminar
- Cálculo correcto de "derecha" relativa al player

**`NPCPartyMember.cs`**:
- `MoveToDialoguePosition(pos, time, npcTarget)` - Recibe NPC target
- `ReleaseDialoguePosition()` - Vuelve a seguir

**`DialogueManager.cs`**:
- Llama automáticamente al iniciar diálogo
- Libera automáticamente al terminar

## 🎯 Características Implementadas

### ✅ Posicionamiento Correcto
- "Right" = Derecha del player **mirando al NPC**
- "Left" = Izquierda del player **mirando al NPC**
- Offset adelante/atrás configurable

### ✅ Miradas Correctas
- **Player** → mira al NPC al iniciar
- **Party members** → miran al NPC al llegar
- **NPC** → (ya miraba al player por defecto)

### ✅ Múltiples NPCs
- Si hay varios en el mismo lado, se distribuyen automáticamente
- Cada uno más lejos y más atrás que el anterior

### ✅ Seguridad
- Validación en NavMesh
- Teletransporte si tarda mucho
- Funciona aunque falte configuración

## 🔍 Debugging

Los logs te dirán exactamente qué está pasando:

```
[PlayerParty] 📍 Posicionando 2 party members para diálogo
[PlayerParty] 👁️ Player girado hacia NPC_Eldran
[PlayerParty]   ↳ Estela → DERECHA (distancia: 1.5m, offset: -0.3m)
[PlayerParty]   ↳ Aria → IZQUIERDA (distancia: 1.5m, offset: -0.3m)

[DialoguePositionState:NPC_Estela] 🎯 Moviéndose a posición de diálogo: (96, 0, 44)
[DialoguePositionState:NPC_Estela] ✅ Llegó a posición de diálogo
[DialoguePositionState:NPC_Estela] 👁️ Mirando hacia NPC_Eldran

[PlayerParty] 🔓 Liberando posicionamiento de diálogo para 2 members
[NPCPartyMember:NPC_Estela] 🔓 Liberado de posición de diálogo, volviendo a seguir
```

## 💡 Configuraciones Recomendadas

### Para Conversaciones Formales:
```
Distancia Lateral: 2.0        ← Más separados
Offset Delante: 0.0           ← Misma línea que player
Tiempo Máximo: 3.0
```

### Para Conversaciones Casuales:
```
Distancia Lateral: 1.2        ← Más cerca
Offset Delante: -0.5          ← Más atrás
Tiempo Máximo: 1.5
```

### Para Escenas Cinematográficas:
```
Distancia Lateral: 1.8
Offset Delante: -0.2          ← Ligeramente atrás
Tiempo Máximo: 2.5
```

## 🚀 Integración con Sistema de Cámara

Este sistema se integra perfectamente con el sistema de cámara multi-speaker:

1. **Player habla con Eldran**
2. Party members se posicionan a los lados
3. Todos miran a Eldran
4. **Estela dice una línea** → La cámara enfoca a Estela
5. **Player responde** → La cámara enfoca al Player
6. **Eldran responde** → La cámara enfoca a Eldran

**Resultado**: Escena de diálogo profesional como en películas/series.

## ✅ Checklist de Configuración

Para cada party member:

- [ ] Abrir su `NPCPartyConfig` en el Inspector
- [ ] Activar `posicionarseDuranteDialogos`
- [ ] Configurar `ladoPreferidoDialogo` (Left o Right)
- [ ] Ajustar `distanciaLateralDialogo` si es necesario
- [ ] Ajustar `offsetDelanteDialogo` si quieres que esté adelante/atrás
- [ ] Configurar `dialogueCharacterId` para el sistema de cámara
- [ ] Probar en juego hablando con un NPC

## 🎬 Resultado Final

**Antes del sistema**:
- Party members detrás del player
- Posiciones aleatorias
- Nadie mira a nadie

**Con el sistema**:
- ✅ Party members flanqueando al player (izq/der configurables)
- ✅ Todos miran al NPC
- ✅ Posicionamiento consistente y profesional
- ✅ Transiciones suaves
- ✅ Compatible con sistema de cámara multi-speaker
- ✅ Escenas cinematográficas de calidad profesional

---

**Fecha**: 2026-02-05  
**Estado**: ✅ Completamente implementado y funcional  
**Nota**: Si Unity no encuentra `DialoguePositionState`, reimporta el archivo o reinicia Unity

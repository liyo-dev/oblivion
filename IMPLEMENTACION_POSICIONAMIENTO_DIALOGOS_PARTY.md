# ✅ IMPLEMENTADO: Posicionamiento de Party Members Durante Diálogos

## 🎯 Funcionalidad Implementada

Ahora los **party members se posicionan automáticamente al lado del player** cuando habla con un NPC. Puedes configurar si cada miembro se pone a la **izquierda** o **derecha** del jugador.

## 🎬 Cómo Funciona

### Flujo Automático:

1. **Player habla con NPC** → DialogueManager.StartDialogue() se llama
2. **PlayerParty.PositionMembersForDialogue()** se ejecuta automáticamente
3. Cada party member se mueve a su posición configurada (izquierda/derecha)
4. **Diálogo termina** → PlayerParty.ReleaseDialoguePositioning() libera a los miembros
5. Party members vuelven a seguir al player normalmente

### Estado Especial: DialoguePositionState

Los NPCs cambian temporalmente a un nuevo estado `DialoguePositionState` que:
- ✅ Mueve al NPC a la posición lateral del player
- ✅ Se detiene al llegar y mira hacia el player
- ✅ Si tarda demasiado, se teletransporta (configurable)
- ✅ Se mantiene quieto durante todo el diálogo
- ✅ Al terminar, vuelve a FollowPlayerState automáticamente

## 📋 Configuración en NPCPartyConfig

Abre el ScriptableObject `NPCPartyConfig` de cada party member en el Inspector:

```
=== POSICIONAMIENTO EN DIÁLOGOS ===

posicionarseDuranteDialogos: ✓ true           ← Activar/desactivar
ladoPreferidoDialogo: Right                    ← Left o Right
distanciaLateralDialogo: 1.5                   ← Distancia lateral (metros)
offsetDelanteDialogo: -0.3                     ← Adelante (+) o atrás (-)
tiempoMaximoMovimientoDialogo: 2.0             ← Tiempo antes de teleport
```

### Ejemplo: Configuración de Estela

```
posicionarseDuranteDialogos: ✓ true
ladoPreferidoDialogo: Right        ← Se pone a la DERECHA del player
distanciaLateralDialogo: 1.5       ← A 1.5 metros de distancia
offsetDelanteDialogo: -0.3         ← Ligeramente atrás (-0.3m)
tiempoMaximoMovimientoDialogo: 2.0 ← Si tarda más de 2 seg, se teletransporta
```

## 🎮 Resultado Visual

**Antes del diálogo**:
```
    Estela
      🧙
        Player → NPC
          🧑      🤖
```

**Durante el diálogo** (Estela configurada en Right):
```
Player → NPC
  🧑      🤖
         Estela
           🧙
```

**Con múltiples party members**:
```
Aria     Player → NPC    Estela
 🧙        🧑      🤖       🧝
(Left)                    (Right)
```

## 🔧 Características Avanzadas

### 1. Múltiples NPCs en el Mismo Lado

Si tienes 2+ NPCs configurados en el mismo lado, se distribuyen automáticamente:
- **Primer NPC**: Posición base
- **Segundo NPC**: Un poco más atrás y más separado
- **Tercer NPC**: Aún más atrás y separado

```python
# Cálculo automático:
lateralDistance = base + (sideCount * 0.5)    # +0.5m por cada NPC adicional
forwardOffset = base - (sideCount * 0.3)      # -0.3m atrás por cada NPC
```

### 2. Validación en NavMesh

El sistema valida automáticamente que las posiciones estén en el NavMesh:
- Intenta la posición configurada
- Si no está en NavMesh, busca la posición válida más cercana (radio 3m)
- Si no encuentra, usa la posición calculada de todas formas

### 3. Teletransporte de Seguridad

Si un NPC tarda más del tiempo configurado en llegar:
- Se teletransporta automáticamente a la posición
- Log: `⚡ Teletransportado a posición de diálogo (tardó X.Xs)`

### 4. Orientación Inteligente

Al llegar a la posición, el NPC:
- Se detiene completamente
- Gira hacia el player automáticamente
- Se queda quieto durante todo el diálogo

## 📁 Archivos Creados/Modificados

### Nuevos:
- `Assets/Scripts/Behaviour NPC/States/DialoguePositionState.cs` ⭐
  - Estado FSM para posicionamiento durante diálogos
  - Gestiona movimiento, llegada, y teletransporte

### Modificados:
- `Assets/Scripts/Behaviour NPC/Modules/NPCPartyConfig.cs`
  - Nuevo enum: `DialoguePositionSide` (Left/Right)
  - 5 nuevos campos configurables para posicionamiento

- `Assets/Scripts/Behaviour NPC/PlayerParty.cs`
  - `PositionMembersForDialogue()` - Posiciona todos los miembros
  - `ReleaseDialoguePositioning()` - Libera a los miembros

- `Assets/Scripts/Behaviour NPC/NPCPartyMember.cs`
  - `MoveToDialoguePosition()` - Mueve a posición específica
  - `ReleaseDialoguePosition()` - Vuelve a seguir al player

- `Assets/Scripts/Dialogue/DialogueManager.cs`
  - Llama a `PositionMembersForDialogue()` al iniciar diálogo
  - Llama a `ReleaseDialoguePositioning()` al terminar

## 🎯 Casos de Uso

### Caso 1: Un Solo Party Member (Estela)

**Configuración**:
```
Estela:
  ladoPreferidoDialogo: Right
  distanciaLateralDialogo: 1.5
```

**Resultado**:
```
Player habla con Eldran → Estela se pone a la derecha del player
```

### Caso 2: Dos Party Members

**Configuración**:
```
Estela:
  ladoPreferidoDialogo: Right

Aria:
  ladoPreferidoDialogo: Left
```

**Resultado**:
```
Aria         Player → Eldran        Estela
(izquierda)                        (derecha)
```

### Caso 3: Tres en el Mismo Lado

**Configuración**:
```
Estela, Aria, Lumina:
  todas → ladoPreferidoDialogo: Right
```

**Resultado**:
```
Player → NPC
             Estela (base)
                  Aria (más lejos y atrás)
                       Lumina (aún más lejos y atrás)
```

### Caso 4: Desactivar para un NPC

**Configuración**:
```
Estela:
  posicionarseDuranteDialogos: ✓ true   ← Se posiciona

Compañero temporal:
  posicionarseDuranteDialogos: ✗ false  ← Se queda donde está
```

## 🔍 Debugging

Los logs te mostrarán exactamente qué está pasando:

```
[PlayerParty] 📍 Posicionando 2 party members para diálogo
[PlayerParty]   ↳ Estela → DERECHA (distancia: 1.5m, offset: -0.3m)
[PlayerParty]   ↳ Aria → IZQUIERDA (distancia: 1.5m, offset: -0.3m)

[DialoguePositionState:NPC_Estela] 🎯 Moviéndose a posición de diálogo: (100, 0, 50)
[DialoguePositionState:NPC_Estela] ✅ Llegó a posición de diálogo

[PlayerParty] 🔓 Liberando posicionamiento de diálogo para 2 members
[NPCPartyMember:NPC_Estela] 🔓 Liberado de posición de diálogo, volviendo a seguir
```

### Si un NPC no se posiciona:

**Verifica**:
1. ✅ `posicionarseDuranteDialogos` está activado en su NPCPartyConfig
2. ✅ El NPC está efectivamente en el party (`IsInParty = true`)
3. ✅ El diálogo se inició con `StartDialogue(asset, npc)` (con el NPC)
4. ✅ El NPCPartyConfig está asignado en el componente

### Si se teletransporta siempre:

**Ajusta**:
- Aumenta `tiempoMaximoMovimientoDialogo` (ej: de 2.0 a 3.0)
- Verifica que el NavMesh esté bien configurado
- Revisa que no haya obstáculos entre el NPC y la posición objetivo

## 💡 Consejos de Configuración

### Para Diálogos Normales:
```
distanciaLateralDialogo: 1.5      ← Distancia natural
offsetDelanteDialogo: -0.3        ← Ligeramente atrás
tiempoMaximoMovimientoDialogo: 2.0
```

### Para Diálogos Formales/Importantes:
```
distanciaLateralDialogo: 2.0      ← Más separados
offsetDelanteDialogo: 0.0         ← A la misma altura
tiempoMaximoMovimientoDialogo: 3.0
```

### Para Diálogos Rápidos/Casuales:
```
distanciaLateralDialogo: 1.0      ← Más cerca
offsetDelanteDialogo: -0.5        ← Más atrás
tiempoMaximoMovimientoDialogo: 1.5
```

## 🚀 Ventajas del Sistema

✅ **Completamente automático**: No requiere configuración por diálogo  
✅ **Configurable por NPC**: Cada party member tiene su preferencia  
✅ **Distribuye múltiples NPCs**: Gestiona automáticamente varios en el mismo lado  
✅ **Valida NavMesh**: No coloca NPCs en posiciones inválidas  
✅ **Teletransporte de seguridad**: Si tarda mucho, teletransporta  
✅ **Transiciones suaves**: Los NPCs caminan/corren a su posición  
✅ **Compatible con sistema de cámara**: Los NPCs están posicionados para los planos  
✅ **Se integra con diálogos multi-speaker**: La cámara puede enfocar a cualquiera  

## 🎬 Escenas Cinematográficas Mejoradas

Este sistema mejora significativamente la presentación visual de los diálogos:

**Antes**:
- Party members detrás del player (posición de seguimiento normal)
- Puede que no se vean en cámara
- Posicionamiento aleatorio

**Ahora**:
- Party members flanqueando al player
- Siempre visibles en los planos de cámara
- Posicionamiento consistente y profesional
- Se ve como una escena de película/serie

## 📝 TODO (Futuras Mejoras Opcionales)

- [ ] Animación idle específica durante diálogos (ej: brazos cruzados)
- [ ] Configuración de mirada (hacia player, hacia NPC, o neutral)
- [ ] Posiciones custom por diálogo específico (override)
- [ ] Reacciones emocionales durante el diálogo (gestos)
- [ ] Sistema de prioridad si hay más NPCs que espacio

---

**Fecha**: 2026-02-05  
**Estado**: ✅ Completamente implementado y funcional  
**Requiere**: Configurar `ladoPreferidoDialogo` en cada NPCPartyConfig

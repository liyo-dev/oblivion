# ✅ MEJORAS: Interacción con NPCs - Animaciones y Rotación

## 📋 Problemas Corregidos

### Problema 1: NPCs se giraban y luego volvían a su posición original
**ANTES**: Los NPCs se giraban brevemente al jugador en el primer frame del diálogo pero luego volvían a su rotación original, quedando de espaldas durante la conversación.

**CAUSA**: Solo se aplicaba una rotación instantánea inicial, pero no se mantenía durante el diálogo.

**AHORA**: ✅ El NPC se mantiene mirando al jugador durante **todo el diálogo** con rotación suave.

---

### Problema 2: No se reproducía animación de interacción
**ANTES**: Los NPCs no reproducían ninguna animación especial al hablar con ellos.

**AHORA**: ✅ Todos los NPCs reproducen la animación `InteractWithPeople_NoWeapon` al iniciar un diálogo (excepto en batalla).

---

## 🔧 Cambios Implementados

### Archivo Modificado: `Interactable.cs`

#### 1. **Método `PlayInteractionAnimation()`** - NUEVO
```csharp
private System.Collections.IEnumerator PlayInteractionAnimation(NPCSimpleAnimator npcAnimator)
{
    // Reproduce la animación de saludo/interacción
    npcAnimator.PlayOneShot("InteractWithPeople_NoWeapon", 0, onComplete: null);
    yield return null;
}
```

**Qué hace**:
- Reproduce la animación `InteractWithPeople_NoWeapon` configurada en `NPCSimpleAnimator`
- Solo se ejecuta si el NPC NO está en combate

---

#### 2. **Método `KeepLookingAtPlayer()`** - NUEVO
```csharp
private System.Collections.IEnumerator KeepLookingAtPlayer()
{
    // Mientras el diálogo esté abierto
    while (dm.IsOpen)
    {
        // Calcular dirección hacia el jugador
        Vector3 directionToPlayer = playerGo.transform.position - transform.position;
        directionToPlayer.y = 0f; // Solo rotación horizontal
        
        // Rotar suavemente hacia el jugador
        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        
        yield return null;
    }
}
```

**Qué hace**:
- **Loop continuo** que se ejecuta mientras el diálogo esté abierto
- **Calcula** la dirección hacia el jugador cada frame
- **Rota suavemente** al NPC usando `Quaternion.Slerp` para una transición fluida
- **Solo rotación horizontal** (Y = 0) para evitar que el NPC se incline
- **Se detiene automáticamente** cuando el diálogo se cierra

---

#### 3. **Actualizado `StartDialogue()`**
```csharp
void StartDialogue()
{
    if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
    {
        // 1. Rotación instantánea inicial
        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        transform.rotation = targetRotation;
        
        // 2. Reproducir animación de interacción (si no es batalla)
        var npcAnimator = GetComponent<NPCSimpleAnimator>();
        if (npcAnimator != null && _npcManager != null)
        {
            if (_npcManager.Context == null || !_npcManager.Context.IsInCombat)
            {
                StartCoroutine(PlayInteractionAnimation(npcAnimator));
            }
        }
        
        // 3. Mantener al NPC mirando al jugador durante el diálogo
        StartCoroutine(KeepLookingAtPlayer());
    }
    
    // ...inicio del diálogo...
}
```

**Mejoras**:
- ✅ Rotación instantánea al inicio
- ✅ Reproducción de animación de interacción
- ✅ Seguimiento continuo del jugador durante el diálogo

---

#### 4. **Actualizado `StartDialogueWithOptions()`**
Mismas mejoras aplicadas para diálogos con opciones (Sí/No).

---

## 🎭 Animación `InteractWithPeople_NoWeapon`

Esta animación debe existir en el `Animator` del NPC. Es una animación configurada en `NPCSimpleAnimator.cs`:

```csharp
[Header("Interaction")]
[SerializeField] private string interactState = "InteractWithPeople_NoWeapon";
```

**Comportamiento**:
- Se reproduce cuando hablas con el NPC
- **NO** se reproduce en batalla (se respeta el contexto de combate)
- Es una animación "one-shot" que se reproduce una vez y vuelve a idle

---

## 🔄 Flujo de Interacción Completo

```
Jugador presiona E cerca del NPC
    ↓
Interactable.Interact()
    ↓
StartDialogue()
    ↓
┌────────────────────────────────────────┐
│ 1. Rotar NPC hacia jugador (instant)  │
│ 2. PlayInteractionAnimation()         │
│    └─ InteractWithPeople_NoWeapon     │
│ 3. KeepLookingAtPlayer() INICIA       │
│    └─ Loop: Rotar suavemente cada     │
│       frame mientras dm.IsOpen == true│
└────────────────────────────────────────┘
    ↓
DialogueManager.StartDialogue()
    ↓
⏱️ Durante el diálogo:
    - NPC sigue mirando al jugador (smooth)
    - Animación de interacción se reproduce
    - Si el jugador se mueve, NPC ajusta rotación
    ↓
Diálogo se cierra (dm.IsOpen = false)
    ↓
KeepLookingAtPlayer() detecta y TERMINA
    ↓
✅ NPC mantiene su última rotación
```

---

## 🎯 Condiciones Especiales

### 1. **Durante Combate**
Si el NPC está en combate (`_npcManager.Context.IsInCombat == true`):
- ❌ **NO** se reproduce `InteractWithPeople_NoWeapon`
- ✅ **SÍ** se mantiene mirando al jugador
- Esto permite diálogos pre-batalla y post-batalla con comportamiento apropiado

### 2. **Sin NPCSimpleAnimator**
Si el GameObject no tiene `NPCSimpleAnimator`:
- ❌ No se reproduce animación de interacción
- ✅ Sigue manteniendo la rotación hacia el jugador
- Útil para NPCs simples o props interactivos

### 3. **Sin NPCBehaviourManagerV2**
Si no hay manager:
- ❌ No se reproduce animación (falta contexto)
- ✅ Sigue manteniendo la rotación

---

## 📊 Comparación ANTES vs AHORA

| Aspecto | ANTES | AHORA |
|---------|-------|-------|
| **Rotación inicial** | ✅ Instantánea | ✅ Instantánea |
| **Mantener rotación** | ❌ Se perdía inmediatamente | ✅ Durante todo el diálogo |
| **Animación de saludo** | ❌ No había | ✅ InteractWithPeople_NoWeapon |
| **Seguimiento del jugador** | ❌ No | ✅ Smooth tracking |
| **Respeto a combate** | ❌ No distinguía | ✅ No anima en combate |
| **Performance** | ✅ Buena | ✅ Buena (solo durante diálogo) |

---

## 🐛 Logs de Debug

### Al iniciar diálogo:
```
[Interactable:Oliver] 📖 StartDialogue - dialogue=Oliver_Greeting
[Interactable:Oliver] 👁️ NPC girado hacia el jugador para diálogo
[Interactable:Oliver] 🎭 Reproduciendo animación de interacción
[Interactable:Oliver] ✅ Iniciando diálogo: Oliver_Greeting
```

### Durante el diálogo:
```
(No hay logs para evitar spam - rotación silenciosa cada frame)
```

### Al cerrar diálogo:
```
[Interactable:Oliver] 🔚 Diálogo cerrado - dejando de seguir al jugador
[Interactable:Oliver] 🔚 Diálogo terminado
```

---

## ⚙️ Configuración en NPCSimpleAnimator

Para que funcione correctamente, el `NPCSimpleAnimator` debe tener configurado:

```
[Header("Interaction")]
Interact State: "InteractWithPeople_NoWeapon"
```

Y esta animación debe existir en el `Animator Controller` del NPC.

---

## 🧪 Testing

### Test 1: Diálogo Normal
1. Acércate a Oliver
2. Presiona E para hablar
3. **Verificar**:
   - ✅ Oliver se gira hacia ti instantáneamente
   - ✅ Oliver reproduce animación de saludo/interacción
   - ✅ Oliver se mantiene mirándote durante el diálogo
   - ✅ Si te mueves, Oliver ajusta su rotación suavemente

### Test 2: Diálogo con Opciones
1. Habla con un NPC que tenga opciones (Sí/No)
2. **Verificar**:
   - ✅ Mismo comportamiento que Test 1
   - ✅ Se mantiene mirando incluso con el prompt de opciones

### Test 3: Diálogo Pre-Batalla
1. Inicia un combate con un NPC agresivo
2. Durante el diálogo de desafío
3. **Verificar**:
   - ✅ NPC se mantiene mirándote
   - ❌ NO reproduce animación de saludo (está en combate)
   - ✅ Mantiene postura de batalla

### Test 4: Múltiples Interacciones
1. Habla con Oliver
2. Cierra el diálogo
3. Vuelve a hablar con él
4. **Verificar**:
   - ✅ Funciona correctamente cada vez
   - ✅ No hay rotaciones acumuladas o bugs

---

## 🎨 Animaciones Relacionadas

Estas son las animaciones configuradas en `NPCSimpleAnimator` para interacciones:

| Animación | Uso | Cuándo |
|-----------|-----|--------|
| `InteractWithPeople_NoWeapon` | Saludo general | Diálogos normales |
| `Greeting01_NoWeapon` | Saludo alternativo | (No usado actualmente) |
| `Challenging_NoWeapon` | Desafío | Pre-batalla (futuro) |

---

## ✅ Resultado Final

**Los NPCs ahora**:
1. ✅ Se giran hacia el jugador al iniciar el diálogo
2. ✅ Reproducen una animación de saludo/interacción apropiada
3. ✅ **Se mantienen mirando al jugador** durante todo el diálogo
4. ✅ Ajustan su rotación suavemente si el jugador se mueve
5. ✅ Respetan el contexto de combate (no saludan en batalla)
6. ✅ Funcionan en todos los tipos de diálogo (normal, opciones, narrativas)

**¡La interacción con NPCs ahora se siente mucho más natural y viva!** 🎉


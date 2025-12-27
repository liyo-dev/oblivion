# 📋 INSTRUCCIONES: Agregar PlayerBattleModeController al Player

## ✅ **PASO 1: Agregar el Componente**

### En Unity Editor:

1. **Abrir la escena** donde está el player
2. **Seleccionar** el GameObject del player en la Hierarchy
   - Busca el GameObject llamado `_PLAYER` o similar
3. **Agregar el componente:**
   - Click en `Add Component` en el Inspector
   - Buscar: `PlayerBattleModeController`
   - O escribir: `Player Battle Mode Controller`
   - Click para agregarlo

**Alternativa rápida:**
- Arrastra el script desde el Project panel hasta el Inspector del player

---

## ✅ **PASO 2: Verificar Referencias Auto-Configuradas**

El componente **auto-encuentra** estas referencias en `Awake()`:
- ✅ Animator
- ✅ vThirdPersonController  
- ✅ Rigidbody

### Verificación en Inspector:

Después de agregar el componente, verifica en el Inspector que se llenaron automáticamente:

```
PlayerBattleModeController (Script)
├── Referencias
│   ├── Animator: [Auto-asignado ✅]
│   ├── Controller: [Auto-asignado ✅]
│   └── Player Rigidbody: [Auto-asignado ✅]
├── Configuración
│   ├── Battle Idle State Name: "Idle_Battle"
│   ├── Normal Idle State Name: "Idle"
│   ├── Enemy Detection Radius: 15
│   ├── Enemy Layer: Everything
│   ├── Exit Battle Delay: 3
│   └── Idle Speed Threshold: 0.1
└── Debug
    └── Debug Mode: □ (desactivado)
```

---

## ✅ **PASO 3: Configurar Animator del Player**

El player necesita tener un estado `Idle_Battle` en su Animator Controller.

### Opción A: Ya existe el estado
Si ya tienes un estado de Battle Idle:
1. Verifica el nombre exacto en el Animator Controller
2. Si es diferente a `Idle_Battle`, ajusta el campo `Battle Idle State Name` en el Inspector

### Opción B: Crear el estado (si no existe)
1. Abrir el Animator Controller del player
2. Crear un nuevo estado llamado `Idle_Battle`
3. Asignarle la animación de idle de batalla
4. (Opcional) Crear transiciones desde otros estados

**Nota:** El componente mostrará un warning en Console si el estado no existe.

---

## ✅ **PASO 4: Configurar Layer de Enemigos**

Los NPCs enemigos deben estar en el layer `Enemy`:

1. **Verificar NPCs:**
   - Seleccionar un NPC enemigo en Hierarchy
   - Inspector → Layer dropdown (arriba)
   - Debe estar en `Enemy`

2. **Configurar Enemy Layer Mask:**
   - Seleccionar el player
   - En `PlayerBattleModeController` → Enemy Layer
   - Asegurarse de que `Enemy` está marcado

---

## ✅ **PASO 5: Ajustar Configuración (Opcional)**

Puedes ajustar estos valores según tus necesidades:

### Enemy Detection Radius (15m default)
```
Más grande = Detecta enemigos más lejos
Más pequeño = Solo detecta enemigos muy cerca
```

### Exit Battle Delay (3s default)
```
Más largo = Tarda más en salir de Battle Mode sin enemigos
Más corto = Sale rápido de Battle Mode
```

### Idle Speed Threshold (0.1 default)
```
Velocidad mínima para considerar al player "quieto"
```

---

## 🐛 **PASO 6: Activar Debug (Para Verificar)**

### Activar Debug Mode:
1. Seleccionar el player
2. En `PlayerBattleModeController`
3. Marcar la casilla `Debug Mode`

### Lo que verás:
- **Gizmos visuales:**
  - Esfera VERDE = No está en Battle Mode
  - Esfera ROJA = En Battle Mode
  - Radio = Enemy Detection Radius

- **Logs en Console:**
```
[PlayerBattleMode] Enemigo detectado: Erika
[PlayerBattleMode] 🗡️ ENTRANDO en Battle Mode
[PlayerBattleMode] ✅ Cambiado a Battle Idle
[PlayerBattleMode] 🏡 SALIENDO de Battle Mode
```

---

## ✅ **VERIFICACIÓN FINAL**

### Test en Play Mode:

1. **Iniciar Play Mode**
2. **Acercarse a un NPC enemigo (< 15m)**
   - ✅ Console: "Enemigo detectado"
   - ✅ Console: "ENTRANDO en Battle Mode"
   - ✅ Gizmo cambia a ROJO
3. **Detenerse (quieto)**
   - ✅ Console: "Cambiado a Battle Idle"
   - ✅ Animación cambia a postura de batalla
4. **Moverse**
   - ✅ Locomotion normal (Invector)
5. **Alejarse del enemigo**
   - ✅ Después de 3s: "SALIENDO de Battle Mode"
   - ✅ Gizmo cambia a VERDE
   - ✅ Animación vuelve a idle normal

---

## ⚠️ **TROUBLESHOOTING**

### Problema: Referencias NULL
**Síntoma:** Warnings en Console sobre referencias nulas

**Solución:**
1. Verificar que el player tiene:
   - Animator component
   - vThirdPersonController component
   - Rigidbody component
2. Si no las encuentra automáticamente, arrastrarlas manualmente en el Inspector

---

### Problema: No detecta enemigos
**Síntoma:** El player no entra en Battle Mode

**Solución:**
1. Verificar que los NPCs están en layer `Enemy`
2. Verificar que `Enemy Layer` mask incluye `Enemy`
3. Activar Debug Mode y verificar logs
4. Verificar que el NPC está en `CombatState` (no en Idle o Patrol)

---

### Problema: Estado "Idle_Battle" no encontrado
**Síntoma:** Warning en Console

**Solución:**
1. Abrir el Animator Controller del player
2. Verificar el nombre exacto del estado de Battle Idle
3. Ajustar `Battle Idle State Name` en el Inspector para que coincida

---

### Problema: No se puede detectar si está quieto
**Síntoma:** Warning sobre Rigidbody no encontrado

**Solución:**
1. Verificar que el player tiene un Rigidbody
2. Si está en un child, asegurarse de que es accesible
3. Asignar manualmente el Rigidbody en el Inspector

---

## 📊 **RESUMEN DE SETUP**

| Paso | Acción | Verificación |
|------|--------|--------------|
| 1 | Agregar componente | ✅ Visible en Inspector |
| 2 | Verificar auto-config | ✅ Referencias asignadas |
| 3 | Configurar Animator | ✅ Estado "Idle_Battle" existe |
| 4 | Configurar Enemy Layer | ✅ NPCs en layer "Enemy" |
| 5 | Ajustar parámetros | ✅ Valores configurados |
| 6 | Activar Debug | ✅ Logs y Gizmos funcionan |
| 7 | Test en Play Mode | ✅ Todo funciona correctamente |

---

## ✅ **ESTADO FINAL**

Después de seguir estos pasos:

```
✅ Componente agregado al player
✅ Referencias auto-configuradas
✅ Animator con estado Battle Idle
✅ NPCs en layer Enemy
✅ Debug mode configurado (opcional)
✅ Funcionalidad verificada en Play Mode
```

**El player ahora usará Battle Idle automáticamente cuando haya enemigos cerca.**

---

## 🎯 **RESULTADO ESPERADO**

### Comportamiento Final:

```
Player + NPC enemigo cerca (< 15m):
    ↓
Player quieto → 🗡️ Battle Idle
Player moviéndose → 🏃 Locomotion normal
    ↓
Sin enemigos por 3s:
    ↓
Player quieto → 🧍 Idle Normal
```

---

**¿Listo para usar?** Sigue los 7 pasos y el sistema estará funcionando. 🎮


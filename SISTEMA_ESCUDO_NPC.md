# 🛡️ SISTEMA DE ESCUDO DEFENSIVO PARA NPCs

## 📋 **RESUMEN**

Los NPCs ahora pueden **defenderse activamente** cuando no pueden atacar (cooldowns activos). El sistema:
- ✅ **Instancia un escudo mágico** visual
- ✅ **Bloquea proyectiles del player**
- ✅ **Reproduce animaciones** de defensa e impacto
- ✅ **Se activa automáticamente** cuando no hay ataques disponibles
- ✅ **Sistema de cooldown** para balanceo

---

## 🎯 **PROBLEMA RESUELTO**

**ANTES:**
```
NPC lanza 3 hechizos → Cooldowns activos → ❌ Se queda quieto sin hacer nada → Blanco fácil
```

**AHORA:**
```
NPC lanza 3 hechizos → Cooldowns activos → ✅ Activa escudo defensivo → Bloquea proyectiles del player
```

---

## 📁 **ARCHIVOS NUEVOS**

### **1. NPCShieldController.cs**
Controlador del escudo para NPCs (similar al `PlayerShieldController`).

**Ubicación:** `Assets/Scripts/Behaviour NPC/NPCShieldController.cs`

**Responsabilidades:**
- ✅ Instanciar/destruir el prefab del escudo
- ✅ Reproducir animaciones de defensa (`Defend_NoWeapon`, `DefendHit_NoWeapon`)
- ✅ Detectar colisiones con proyectiles del player
- ✅ Destruir proyectiles bloqueados
- ✅ Gestionar duración del escudo

**Parámetros configurables:**
```csharp
[Header("Escudo")]
public GameObject shieldPrefab;              // Prefab visual del escudo
public Transform shieldAnchor;               // Punto de anclaje (opcional)
public Vector3 shieldOffset;                 // Offset de posición

[Header("Animaciones")]
public string defendAnimation = "Defend_NoWeapon";
public string defendHitAnimation = "DefendHit_NoWeapon";
public int upperBodyLayer = 1;               // Capa UpperBody del Animator

[Header("Colisiones a bloquear")]
public string[] blockLayerNames = { "Projectile", "PlayerProjectile" };

[Header("Duración")]
public float minDefendDuration = 2f;         // Mínimo 2s defendiendo
public float maxDefendDuration = 5f;         // Máximo 5s defendiendo
```

---

## 🔧 **ARCHIVOS MODIFICADOS**

### **1. NPCCombatBrain.cs**

**Cambios:**

#### **A) Settings Struct - Nuevos campos:**
```csharp
public bool useShield;              // Si el NPC puede usar escudo
public float shieldMinDuration;     // Duración mínima del escudo
public float shieldMaxDuration;     // Duración máxima del escudo
public float shieldCooldown;        // Cooldown entre usos
```

#### **B) Variables de instancia:**
```csharp
NPCShieldController _shieldController;  // Referencia al controller
float _shieldCooldownTimer;             // Timer de cooldown
bool _isDefending;                      // Flag de estado
```

#### **C) Inicialización en BeginCombat():**
```csharp
// Busca el NPCShieldController en el mismo GameObject
_shieldController = GetComponent<NPCShieldController>();
_shieldCooldownTimer = 0f;
_isDefending = false;
```

#### **D) Update de cooldown en CombatLoop():**
```csharp
if (_shieldCooldownTimer > 0f)
{
    _shieldCooldownTimer -= Time.deltaTime;
}
```

#### **E) Activación automática cuando no hay ataques:**
```csharp
if (availableAttacks.Count == 0)
{
    Debug.Log("⏳ Esperando cooldowns...");
    TryActivateShield();  // ✅ NUEVA LÓGICA
    return;
}
```

#### **F) Nueva función TryActivateShield():**
```csharp
void TryActivateShield()
{
    if (!_settings.useShield) return;
    if (_shieldController == null) return;
    if (_isDefending || _shieldController.IsDefending) return;
    if (_shieldCooldownTimer > 0f) return;
    
    // Activar escudo por duración aleatoria
    float duration = Random.Range(
        _settings.shieldMinDuration,
        _settings.shieldMaxDuration
    );
    
    _shieldController.StartDefending(duration);
    _isDefending = true;
    _shieldCooldownTimer = _settings.shieldCooldown;
    
    // Desactivar automáticamente después de la duración
    StartCoroutine(DeactivateShieldAfter(duration));
}
```

---

### **2. NPCCombatConfig.cs**

**Cambios:**

```csharp
[Header("🛡️ Escudo Defensivo")]
[Tooltip("¿El NPC puede usar escudo para defenderse?")]
public bool useShield = false;

[Tooltip("Prefab del escudo visual")]
public GameObject shieldPrefab;

[Min(0.5f)]
public float shieldMinDuration = 2f;

[Min(0.5f)]
public float shieldMaxDuration = 5f;

[Min(0f)]
public float shieldCooldown = 10f;
```

---

### **3. CombatState.cs**

**Cambios:**

Mapea los valores del config al Settings:

```csharp
var settings = new NPCCombatBrain.Settings
{
    // ...existing code...
    
    // ✅ Escudo defensivo
    useShield = combatConfig.useShield,
    shieldMinDuration = combatConfig.shieldMinDuration,
    shieldMaxDuration = combatConfig.shieldMaxDuration,
    shieldCooldown = combatConfig.shieldCooldown
};
```

---

## 🎮 **SETUP EN UNITY**

### **Paso 1: Añadir componente al NPC**

1. Selecciona el GameObject del NPC (ej. `Boy_Pirate`)
2. **Add Component** → `NPCShieldController`

### **Paso 2: Configurar NPCShieldController**

En el Inspector del `NPCShieldController`:

```
Escudo:
  ├─ Shield Prefab: [Asignar prefab del escudo visual]
  ├─ Shield Anchor: [Opcional - punto de anclaje]
  └─ Shield Offset: (0, 1, 0.5)  // Ajustar según necesidad

Animaciones:
  ├─ Defend Animation: "Defend_NoWeapon"
  ├─ Defend Hit Animation: "DefendHit_NoWeapon"
  ├─ Upper Body Layer: 1
  └─ Hit Feedback Duration: 0.3

Colisiones a bloquear:
  └─ Block Layer Names:
      ├─ [0] "Projectile"
      └─ [1] "PlayerProjectile"

Duración:
  ├─ Min Defend Duration: 2
  └─ Max Defend Duration: 5
```

### **Paso 3: Configurar NPCCombatConfig (ScriptableObject)**

En el `NPCCombatConfig` del NPC:

```
🛡️ Escudo Defensivo:
  ├─ Use Shield: ✅ true
  ├─ Shield Prefab: [Asignar prefab del escudo]
  ├─ Shield Min Duration: 2
  ├─ Shield Max Duration: 5
  └─ Shield Cooldown: 10
```

**⚠️ IMPORTANTE:**
- Si `useShield = true` pero NO hay `NPCShieldController` en el GameObject, aparecerá un warning en la consola
- El prefab del escudo debe tener un **Collider** (se añadirá automáticamente si no lo tiene)

---

## 🔮 **PREFAB DEL ESCUDO**

### **Opción 1: Usar el prefab del Player**

Si el player usa un escudo similar, puedes reutilizarlo:

1. Busca el prefab en `Assets/Prefabs/Player/Shield/` (o similar)
2. Asígnalo en `shieldPrefab`

### **Opción 2: Crear uno nuevo**

1. Crea un GameObject vacío: `NPC_Shield`
2. Añade un mesh visual (esfera, cúpula, etc.)
3. Añade un **Collider** (SphereCollider recomendado)
   - ✅ **Is Trigger**: true
   - Radio: 1.5 - 2.0
4. Opcional: Añade un material con shader transparente/emisivo
5. Guárdalo como Prefab

**Estructura recomendada:**
```
NPC_Shield (GameObject)
  ├─ SphereCollider (Trigger)
  └─ Visual (Mesh)
      ├─ MeshRenderer
      └─ Material (Shader: Transparent/Emissive)
```

---

## 📊 **FLUJO DE ACTIVACIÓN**

```
┌─────────────────────────────────────────┐
│  CombatLoop - Intenta atacar            │
└──────────────┬──────────────────────────┘
               │
               ▼
        ┌──────────────┐
        │ Cooldowns?   │
        └──────┬───────┘
               │
        ┌──────▼─────────┐
        │ Todos activos? │
        └──────┬─────────┘
               │ SÍ
               ▼
    ┌──────────────────────┐
    │ TryActivateShield()  │
    └──────────┬───────────┘
               │
        ┌──────▼─────────────┐
        │ useShield = true?  │
        └──────┬─────────────┘
               │ SÍ
        ┌──────▼──────────────────┐
        │ ShieldCooldown listo?   │
        └──────┬──────────────────┘
               │ SÍ
        ┌──────▼────────────────────────┐
        │ shieldController.StartDefending()│
        └──────┬────────────────────────┘
               │
        ┌──────▼─────────────────────┐
        │ Duración aleatoria 2-5s    │
        └──────┬─────────────────────┘
               │
        ┌──────▼─────────────────────┐
        │ Escudo ACTIVO              │
        │ - Bloquea proyectiles      │
        │ - Animación de defensa     │
        └──────┬─────────────────────┘
               │
        ┌──────▼────────────────────┐
        │ Duración completada        │
        └──────┬────────────────────┘
               │
        ┌──────▼─────────────────────┐
        │ StopDefending()            │
        │ Cooldown = 10s             │
        └────────────────────────────┘
```

---

## 🎯 **COMPORTAMIENTO EN COMBATE**

### **Secuencia típica:**

```
1. NPC lanza MagicLeft    (t=0s)
2. NPC lanza MagicRight   (t=1s)
3. NPC lanza MagicSpecial (t=2s)
4. ⏳ Cooldowns activos   (t=3s)
5. 🛡️ Activa escudo      (t=3s)
   └─ Duración: 3.5s (random 2-5s)
6. 💥 Proyectil del player impacta
   └─ Bloqueado y destruido
   └─ Animación "DefendHit_NoWeapon"
7. 🛡️ Escudo se desactiva (t=6.5s)
8. ⏰ Cooldown del escudo (10s)
9. ✨ Cooldowns listos    (t=8s)
10. NPC vuelve a atacar   (t=8s)
```

---

## 🔍 **LOGS DE DEBUG**

### **Inicialización:**
```
[NPCCombatBrain] ✅ Shield controller encontrado
[NPCShieldController] ✅ Bloqueando capa: Projectile (layer 8)
[NPCShieldController] ✅ Bloqueando capa: PlayerProjectile (layer 9)
```

### **Activación:**
```
[NPCCombatBrain] ⏳ Esperando cooldowns... LEFT:2.1s RIGHT:1.5s SPECIAL:4.8s
[NPCCombatBrain] 🛡️ ESCUDO ACTIVADO - Duración: 3.2s, Cooldown: 10.0s
[NPCShieldController] 🛡️ DEFENSA ACTIVADA - Duración: 3.2s
[NPCShieldController] ✅ Escudo instanciado
[NPCShieldHitDetector] ✅ Inicializado - Bloqueando 2 capas
```

### **Impacto:**
```
[NPCShieldHitDetector] 🛡️ Bloqueado proyectil: Fireball(Clone) (layer 8)
[NPCShieldHitDetector] 💥 Destruyendo proyectil: Fireball(Clone)
[NPCShieldController] 💥 Escudo impactado!
```

### **Desactivación:**
```
[NPCShieldController] 🛡️ Duración de defensa completada (3.2s)
[NPCShieldController] 🛡️ DEFENSA DESACTIVADA
[NPCShieldController] ✅ Escudo destruido
[NPCCombatBrain] 🛡️ Escudo desactivado automáticamente
```

### **Cooldown:**
```
[NPCCombatBrain] 🛡️ Escudo en cooldown: 8.5s
```

---

## ⚙️ **CONFIGURACIÓN RECOMENDADA**

### **NPC Fácil (Low Level):**
```csharp
useShield = true
shieldMinDuration = 1.5f
shieldMaxDuration = 3f
shieldCooldown = 15f       // Cooldown largo
```

### **NPC Normal (Mid Level):**
```csharp
useShield = true
shieldMinDuration = 2f
shieldMaxDuration = 4f
shieldCooldown = 10f       // Cooldown medio
```

### **NPC Difícil (High Level):**
```csharp
useShield = true
shieldMinDuration = 3f
shieldMaxDuration = 6f
shieldCooldown = 5f        // Cooldown corto - escudo frecuente
```

### **Boss:**
```csharp
useShield = true
shieldMinDuration = 5f
shieldMaxDuration = 10f
shieldCooldown = 3f        // Casi siempre protegido
```

---

## 🧪 **TESTING**

### **Test 1: Activación básica**
```
1. Iniciar combate con NPC (useShield = true)
2. Esperar a que lance 3 hechizos
3. ✅ Verificar que activa el escudo
4. ✅ Verificar que aparece el prefab visual
5. ✅ Verificar animación "Defend_NoWeapon"
```

### **Test 2: Bloqueo de proyectiles**
```
1. NPC con escudo activo
2. Lanzar hechizo del player
3. ✅ Verificar que colisiona con el escudo
4. ✅ Verificar que se destruye el proyectil
5. ✅ Verificar animación "DefendHit_NoWeapon"
6. ✅ Verificar que NO daña al NPC
```

### **Test 3: Duración y cooldown**
```
1. NPC activa escudo (duración 3s)
2. ✅ Esperar 3s → Escudo se desactiva
3. ✅ Verificar cooldown de 10s
4. Esperar 10s
5. ✅ NPC puede volver a usar escudo
```

### **Test 4: Sin NPCShieldController**
```
1. Config: useShield = true
2. NO añadir NPCShieldController al GameObject
3. ✅ Verificar warning en consola:
   "⚠️ useShield=true pero no hay NPCShieldController"
```

---

## 🎨 **ANIMACIONES REQUERIDAS**

### **Defend_NoWeapon**
- **Ubicación:** UpperBody layer
- **Tipo:** Loop continuo
- **Descripción:** Pose defensiva con las manos adelante
- **Duración:** 1-2s loop

### **DefendHit_NoWeapon**
- **Ubicación:** UpperBody layer
- **Tipo:** One-shot
- **Descripción:** Reacción breve al impacto
- **Duración:** 0.3-0.5s
- **Transición:** Vuelve a `Defend_NoWeapon`

**⚠️ Si no tienes estas animaciones:**
1. Puedes reutilizar las del player
2. O crear placeholder animations vacías
3. O usar las existentes de `Idle_Battle_NoWeapon`

---

## 📈 **IMPACTO EN GAMEPLAY**

### **Antes:**
- ❌ NPC predecible - ataca → se queda quieto
- ❌ Fácil de derrotar con spam de proyectiles
- ❌ Combate monótono

### **Ahora:**
- ✅ NPC impredecible - ataca → se defiende
- ✅ Requiere timing - atacar cuando baja el escudo
- ✅ Combate dinámico y desafiante
- ✅ Más realista - el NPC "piensa"

---

## 🐛 **TROUBLESHOOTING**

### **Problema: El escudo no se activa**
```
Verificar:
1. ✅ useShield = true en NPCCombatConfig
2. ✅ NPCShieldController presente en GameObject
3. ✅ shieldPrefab asignado
4. ✅ Cooldown no activo
5. ✅ Logs en consola
```

### **Problema: Los proyectiles atraviesan el escudo**
```
Verificar:
1. ✅ Collider presente en shieldPrefab
2. ✅ Is Trigger = true
3. ✅ Capas correctas en blockLayerNames
4. ✅ Proyectiles del player en layer "Projectile" o "PlayerProjectile"
5. ✅ NPCShieldHitDetector añadido correctamente
```

### **Problema: Animaciones no se reproducen**
```
Verificar:
1. ✅ upperBodyLayer = 1
2. ✅ Animaciones existen en el Animator
3. ✅ Nombres correctos: "Defend_NoWeapon", "DefendHit_NoWeapon"
4. ✅ Capa UpperBody configurada correctamente
```

### **Problema: Escudo no se desactiva**
```
Verificar:
1. ✅ Duración > 0
2. ✅ NPCShieldController.Update() se ejecuta
3. ✅ No hay errores en consola
4. ✅ Coroutine DeactivateShieldAfter() se inicia
```

---

## 📝 **NOTAS TÉCNICAS**

1. **Layers del Animator:**
   - Base Layer: Locomoción (caminar, correr)
   - UpperBody Layer: Ataques y defensa
   - Weight de UpperBody = 1.0 cuando defiende

2. **Colisiones:**
   - El escudo tiene un SphereCollider Trigger
   - Detecta OnTriggerEnter y OnCollisionEnter
   - Busca el Rigidbody root del proyectil

3. **Destrucción de proyectiles:**
   - Verifica component `Projectile` del player
   - O layer "Projectile" / "PlayerProjectile"
   - Destruye con `Object.Destroy()`

4. **Cooldown del escudo:**
   - Independiente de cooldowns de ataques
   - Se actualiza en CombatLoop cada frame
   - Valor recomendado: 8-12 segundos

5. **Duración aleatoria:**
   - Hace el comportamiento impredecible
   - Rango recomendado: 2-5 segundos
   - Permite ventanas de ataque al player

---

## ✅ **CHECKLIST DE IMPLEMENTACIÓN**

- [x] ✅ Crear `NPCShieldController.cs`
- [x] ✅ Modificar `NPCCombatBrain.cs` (Settings, variables, lógica)
- [x] ✅ Modificar `NPCCombatConfig.cs` (campos del escudo)
- [x] ✅ Modificar `CombatState.cs` (mapeo de config)
- [x] ✅ Testing sin errores de compilación
- [ ] 🔲 Crear/asignar prefab del escudo
- [ ] 🔲 Configurar animaciones en Animator
- [ ] 🔲 Añadir NPCShieldController a NPCs
- [ ] 🔲 Configurar NPCCombatConfig
- [ ] 🔲 Testing en Unity

---

## 🚀 **PRÓXIMOS PASOS**

1. **En Unity:**
   ```
   - Añadir NPCShieldController a Boy_Pirate
   - Asignar prefab del escudo
   - Configurar animaciones
   - Activar useShield en el config
   - Probar en combate
   ```

2. **Testing:**
   ```
   - Verificar activación automática
   - Probar bloqueo de proyectiles
   - Ajustar duraciones y cooldowns
   - Verificar animaciones
   ```

3. **Balanceo:**
   ```
   - Ajustar cooldowns según dificultad
   - Modificar duraciones
   - Testear con diferentes NPCs
   ```

4. **Sistema complementario:**
   ```
   - Ver SISTEMA_HUIDA_TACTICA_NPC.md para huida hacia cobertura
   - Combinar escudo + cobertura para NPCs más inteligentes
   ```

---

## 🔗 **SISTEMAS RELACIONADOS**

- **SISTEMA_HUIDA_TACTICA_NPC.md** - Sistema de búsqueda de cobertura (complementa el escudo)
  - Los NPCs pueden buscar árboles/rocas como cobertura cuando están en desventaja
  - Funciona en conjunto con el sistema de escudo
  - Configuración: `useTacticalRetreat`, `preferShieldOverCover`

---

**¡Sistema de escudo defensivo completamente implementado!** 🎉

**Archivos modificados:** 4  
**Archivos nuevos:** 1  
**Errores de compilación:** 0  
**Estado:** ✅ LISTO PARA PROBAR EN UNITY


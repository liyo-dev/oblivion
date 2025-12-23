# 🏃 SISTEMA DE HUIDA TÁCTICA Y COBERTURA PARA NPCs

## 📋 **RESUMEN**

Los NPCs ahora pueden **huir estratégicamente** cuando están en desventaja, buscando **cobertura detrás de objetos** (árboles, rocas, edificios) para protegerse del jugador. El sistema:

- ✅ **Detecta situaciones de desventaja** (salud baja, cooldowns activos)
- ✅ **Busca cobertura automáticamente** usando Raycast e IA
- ✅ **Evalúa múltiples posiciones** para encontrar la óptima
- ✅ **Bloquea línea de visión** con el jugador
- ✅ **Sistema de cooldown** para equilibrio
- ✅ **Alternativa con escudo** si no hay cobertura disponible
- ✅ **Ventanas de vulnerabilidad** para mantener desafío

---

## 🎯 **PROBLEMA RESUELTO**

**ANTES:**
```
NPC con 20% salud → Sigue atacando → ❌ Muere fácilmente
NPC sin cooldowns → Se queda quieto → ❌ Blanco fácil
```

**AHORA:**
```
NPC con 20% salud → 🏃 Busca cobertura detrás de un árbol → Se esconde 4s → Vuelve al combate
NPC sin ataques → 🏃 Busca cobertura O activa escudo → Sobrevive más tiempo → ⚔️ Combate táctico
```

---

## 🧠 **SISTEMA DE DECISIÓN**

### **Condiciones para activar huida:**

1. **Salud baja:** `HP <= 30%` (configurable)
2. **Sin recursos:** Todos los ataques en cooldown Y escudo en cooldown
3. **Estado defensivo:** El NPC está en modo `CombatState.Defensive`

### **Prioridades:**

```
┌─────────────────────────────────────────┐
│  ¿Debería huir? (ShouldRetreat)        │
│  - Salud <= 30%                         │
│  - Sin ataques disponibles              │
│  - Escudo en cooldown                   │
└──────────────┬──────────────────────────┘
               │ SÍ
               ▼
        ┌──────────────────┐
        │ preferShieldOver │
        │     Cover?       │
        └──────┬───────────┘
               │
        ┌──────▼────────────────────┐
        │  PRIORIDAD 1              │
        │  Buscar Cobertura         │
        │  (si no prefiere escudo)  │
        └──────┬────────────────────┘
               │ ❌ No encontró
               ▼
        ┌──────────────────────────┐
        │  PRIORIDAD 2             │
        │  Activar Escudo          │
        └──────┬───────────────────┘
               │ ❌ No disponible
               ▼
        ┌──────────────────────────┐
        │  PRIORIDAD 3             │
        │  Buscar Cobertura        │
        │  (fallback)              │
        └──────────────────────────┘
```

---

## 📁 **ARCHIVOS NUEVOS**

### **1. NPCTacticalRetreat.cs**

**Ubicación:** `Assets/Scripts/Behaviour NPC/NPCTacticalRetreat.cs`

**Responsabilidades:**
- ✅ Buscar objetos cercanos que sirvan de cobertura
- ✅ Evaluar posiciones óptimas (scoring system)
- ✅ Navegar hacia la cobertura usando NavMesh
- ✅ Verificar línea de visión con el jugador
- ✅ Gestionar el tiempo de permanencia en cobertura

**Algoritmo de búsqueda:**

```csharp
1. Physics.OverlapSphere(coverSearchRadius) → Encuentra objetos cercanos
2. Para cada objeto:
   a) Calcular posición detrás del objeto (opuesta al jugador)
   b) Verificar que esté en NavMesh
   c) Verificar que bloquee línea de visión
   d) Calcular score basándose en:
      - Distancia al NPC (cercano pero no muy cerca)
      - Distancia al jugador (ni muy cerca ni muy lejos)
      - Tamaño del objeto (más grande = mejor)
      - Dirección de huida (alejándose del jugador)
3. Seleccionar la cobertura con mejor score
```

**Propiedades públicas:**
- `bool IsRetreating` - Flag de estado de huida
- `bool IsBehindCover` - Si llegó a la cobertura
- `Vector3? CoverPosition` - Posición actual de cobertura
- `Transform CoverObject` - Objeto usado como cobertura

**Parámetros configurables:**
```csharp
[Header("Configuración de Cobertura")]
public float coverSearchRadius = 15f;      // Radio de búsqueda
public LayerMask coverLayerMask = -1;      // Capas de cobertura
public float minCoverDistance = 3f;        // Distancia mínima
public float maxCoverDistance = 15f;       // Distancia máxima
public float coverStayDuration = 4f;       // Tiempo en cobertura
public float coverDistanceBehind = 1.5f;   // Distancia detrás del objeto
public bool showDebugGizmos = true;        // Gizmos en Scene view
public int maxCoverObjectsToCheck = 10;    // Límite de objetos por performance
```

---

## 🔧 **ARCHIVOS MODIFICADOS**

### **1. NPCCombatBrain.cs**

**Cambios principales:**

#### **A) Nuevos campos en Settings:**
```csharp
// Huida táctica y cobertura
public bool useTacticalRetreat;          // Puede buscar cobertura
public float retreatHealthThreshold;     // % de salud para huir (0.3 = 30%)
public float retreatCooldown;            // Cooldown entre huidas
public float coverSearchRadius;          // Radio de búsqueda
public LayerMask coverLayerMask;         // Capas de cobertura
public float minCoverDistance;           // Distancia mínima
public float maxCoverDistance;           // Distancia máxima
public float coverStayDuration;          // Tiempo en cobertura
public bool preferShieldOverCover;       // Priorizar escudo sobre cobertura
```

#### **B) Variables de instancia:**
```csharp
bool _isRetreating;                    // Flag de huida activa
float _retreatCooldownTimer;           // Timer de cooldown
Vector3? _coverPosition;               // Posición de cobertura
float _coverStayTimer;                 // Tiempo restante
Transform _currentCoverObject;         // Objeto de cobertura
bool _isBehindCover;                   // Flag: está detrás de cobertura
```

#### **C) Nuevas funciones:**

**ShouldRetreat():**
```csharp
// Evalúa si debe activar huida táctica
// - Verifica salud <= umbral
// - Verifica estado de ataques y escudo
// - Considera el CombatState actual
```

**TryFindAndMoveToCover():**
```csharp
// Intenta encontrar cobertura y moverse hacia ella
// - Busca NPCTacticalRetreat component
// - Inicia proceso de huida
// - Configura cooldown
// - Inicia coroutine de gestión
```

**ManageCoverState(retreat):**
```csharp
// Coroutine que gestiona el estado en cobertura
// - Espera hasta llegar a cobertura
// - Puede activar escudo adicional
// - Sale cuando termina la duración
```

**UpdateRetreatCooldown():**
```csharp
// Reduce el cooldown cada frame
// Se llama desde CombatLoop
```

#### **D) Integración en CombatLoop:**

En la sección donde no hay ataques disponibles:

```csharp
if (availableAttacks.Count == 0)
{
    Debug.Log("⏳ Esperando cooldowns...");
    
    // ✅ NUEVO: Sistema de huida táctica
    bool shouldRetreat = ShouldRetreat();
    
    if (shouldRetreat && _settings.useTacticalRetreat && _retreatCooldownTimer <= 0f)
    {
        // Prioridad según preferShieldOverCover
        if (!_settings.preferShieldOverCover)
        {
            if (TryFindAndMoveToCover()) return;
        }
        
        if (_settings.useShield)
        {
            TryActivateShield();
            return;
        }
        
        if (_settings.preferShieldOverCover)
        {
            if (TryFindAndMoveToCover()) return;
        }
    }
    else if (_settings.useShield)
    {
        TryActivateShield();
    }
    
    return;
}
```

---

### **2. NPCCombatConfig.cs**

**Nuevos campos:**

```csharp
[Header("🏃 Huida Táctica y Cobertura")]
[Tooltip("¿El NPC puede buscar cobertura cuando está en desventaja?")]
public bool useTacticalRetreat = false;

[Range(0.1f, 0.5f)]
[Tooltip("% de salud para activar huida táctica (0.3 = 30% de salud)")]
public float retreatHealthThreshold = 0.3f;

[Min(5f)]
[Tooltip("Cooldown entre intentos de huida en segundos")]
public float retreatCooldown = 15f;

[Min(5f)]
[Tooltip("Radio de búsqueda de cobertura en metros")]
public float coverSearchRadius = 15f;

[Tooltip("Capas que se consideran cobertura (Default, Environment, Props)")]
public LayerMask coverLayerMask = -1;

[Min(2f)]
[Tooltip("Distancia mínima de la cobertura al NPC")]
public float minCoverDistance = 3f;

[Min(5f)]
[Tooltip("Distancia máxima de la cobertura al NPC")]
public float maxCoverDistance = 15f;

[Min(2f)]
[Tooltip("Tiempo que permanece en cobertura en segundos")]
public float coverStayDuration = 4f;

[Tooltip("Si true, prioriza usar escudo sobre buscar cobertura")]
public bool preferShieldOverCover = false;
```

---

### **3. CombatState.cs**

**Cambios:**

Mapea los valores del config al Settings del NPCCombatBrain:

```csharp
// ✅ Huida táctica y cobertura
useTacticalRetreat = combatConfig.useTacticalRetreat,
retreatHealthThreshold = combatConfig.retreatHealthThreshold,
retreatCooldown = combatConfig.retreatCooldown,
coverSearchRadius = combatConfig.coverSearchRadius,
coverLayerMask = combatConfig.coverLayerMask,
minCoverDistance = combatConfig.minCoverDistance,
maxCoverDistance = combatConfig.maxCoverDistance,
coverStayDuration = combatConfig.coverStayDuration,
preferShieldOverCover = combatConfig.preferShieldOverCover
```

---

## 🎮 **SETUP EN UNITY**

### **Paso 1: Añadir componente NPCTacticalRetreat**

1. Selecciona el GameObject del NPC (ej. `Boy_Pirate`)
2. **Add Component** → Buscar `NPCTacticalRetreat`
3. Hacer clic en **Add Component**

### **Paso 2: Configurar NPCTacticalRetreat (Inspector)**

```
Configuración de Cobertura:
  ├─ Cover Search Radius: 15         // Radio de búsqueda
  ├─ Cover Layer Mask: [✓] Default, [✓] Environment, [✓] Props
  ├─ Min Cover Distance: 3            // Mínimo 3m del NPC
  ├─ Max Cover Distance: 15           // Máximo 15m del NPC
  ├─ Cover Stay Duration: 4           // 4 segundos en cobertura
  └─ Cover Distance Behind: 1.5       // 1.5m detrás del objeto

Debug:
  ├─ Show Debug Gizmos: ✓ true       // Ver en Scene view
  └─ Max Cover Objects To Check: 10   // Límite por performance
```

### **Paso 3: Configurar NPCCombatConfig (ScriptableObject)**

En el `NPCCombatConfig` del NPC:

```
🏃 Huida Táctica y Cobertura:
  ├─ Use Tactical Retreat: ✅ true
  ├─ Retreat Health Threshold: 0.3         // Huir al 30% HP
  ├─ Retreat Cooldown: 15                  // 15s entre huidas
  ├─ Cover Search Radius: 15               // 15m de búsqueda
  ├─ Cover Layer Mask: [✓] Default, Environment, Props
  ├─ Min Cover Distance: 3                 // Mínimo 3m
  ├─ Max Cover Distance: 15                // Máximo 15m
  ├─ Cover Stay Duration: 4                // 4s en cobertura
  └─ Prefer Shield Over Cover: ☐ false    // Prioriza cobertura sobre escudo
```

### **Paso 4: Configurar capas de cobertura**

En Unity, asegúrate de que los objetos que sirven de cobertura tienen las capas correctas:

**Objetos que pueden ser cobertura:**
- ✅ Árboles → Layer: `Default` o `Environment`
- ✅ Rocas → Layer: `Default` o `Props`
- ✅ Edificios → Layer: `Default` o `Environment`
- ✅ Muros → Layer: `Default`
- ✅ Columnas → Layer: `Props` o `Environment`

**Requisitos del objeto:**
- ✅ Debe tener un **Collider** (Box, Sphere, Mesh)
- ✅ El Collider NO debe ser Trigger
- ✅ Debe tener tamaño suficiente (mínimo 1m de radio)

---

## 📊 **ALGORITMO DE SCORING**

El sistema evalúa cada posición de cobertura con un **sistema de puntuación**:

### **Factores de Score:**

```csharp
Score Total = 
    (30 pts) Distancia al NPC (más cerca es mejor, dentro del rango)
  + (20 pts) Distancia al jugador (óptimo: 10m)
  + (20 pts) Tamaño del objeto (más grande = mejor cobertura)
  + (15 pts) Dirección de huida (alejándose del jugador)
```

### **Ejemplo de cálculo:**

```
Árbol A:
- Distancia al NPC: 5m  →  Score: 25/30
- Distancia al jugador: 12m  →  Score: 16/20
- Tamaño: 8m  →  Score: 16/20
- Ángulo de huida: -0.8 (retrocede)  →  Score: 12/15
TOTAL: 69 puntos

Roca B:
- Distancia al NPC: 12m  →  Score: 15/30
- Distancia al jugador: 8m  →  Score: 18/20
- Tamaño: 3m  →  Score: 6/20
- Ángulo de huida: 0.2 (avanza hacia jugador)  →  Score: 0/15
TOTAL: 39 puntos

✅ Se elige Árbol A (69 > 39)
```

---

## 🎯 **COMPORTAMIENTO EN COMBATE**

### **Secuencia típica (sin huida):**

```
1. NPC lanza MagicLeft    (t=0s, HP: 100%)
2. NPC lanza MagicRight   (t=1s, HP: 100%)
3. NPC lanza MagicSpecial (t=2s, HP: 95%)
4. NPC lanza MagicLeft    (t=3s, HP: 90%)
5. NPC lanza MagicRight   (t=4s, HP: 85%)
6. ⏳ Cooldowns activos   (t=5s, HP: 80%)
7. 🛡️ Activa escudo      (t=5s)
8. Cooldowns listos       (t=8s)
9. Continúa atacando...
```

### **Secuencia con huida táctica:**

```
1. NPC lanza MagicLeft    (t=0s, HP: 100%)
2. NPC lanza MagicRight   (t=1s, HP: 95%)
3. Player ataca mucho     (t=2-5s)
4. HP baja a 28%          (t=5s, HP: 28%)  ⚠️ UMBRAL!
5. 🏃 Busca cobertura     (t=5s)
6. 🌳 Encuentra árbol     (t=5.2s) - Score: 75
7. 🏃 Navega hacia árbol  (t=5.2-7s)
8. ✅ Llega a cobertura   (t=7s)
9. 🛡️ Activa escudo      (t=7s) - Defensa extra
10. ⏰ Permanece 4s       (t=7-11s)
11. ✅ Sale de cobertura  (t=11s, HP: 28%)
12. ⚔️ Vuelve al combate  (t=11s)
13. 🔄 Cooldown: 15s      (próxima huida a t=26s)
```

---

## 🔍 **LOGS DE DEBUG**

### **Evaluación de huida:**
```
[NPCCombatBrain] 🏃 Salud baja (28%), activando huida táctica
[NPCCombatBrain] 🏃 Iniciando huida táctica hacia cobertura
[NPCTacticalRetreat] ✅ Cobertura encontrada: Pine_Tree_02 (Score: 74.50)
```

### **Búsqueda de cobertura:**
```
[NPCTacticalRetreat] 🏃 Huyendo hacia cobertura: Pine_Tree_02 en (12.5, 0.2, 34.8)
[NPCTacticalRetreat] ✅ Llegó a cobertura, permanecerá por 4s
```

### **Sin cobertura disponible:**
```
[NPCTacticalRetreat] ❌ No se encontró cobertura disponible
[NPCCombatBrain] ❌ No se pudo encontrar cobertura
[NPCCombatBrain] 🛡️ ESCUDO ACTIVADO - Duración: 3.5s (fallback)
```

### **Cobertura comprometida:**
```
[NPCTacticalRetreat] ⚠️ Cobertura comprometida, jugador tiene LOS
```

### **Saliendo de cobertura:**
```
[NPCCombatBrain] ✅ Saliendo de cobertura, volviendo a combate activo
[NPCCombatBrain] 🔄 Cooldown de huida: 15.0s
```

---

## ⚙️ **CONFIGURACIONES RECOMENDADAS**

### **NPC Cobarde (Low Level):**
```csharp
useTacticalRetreat = true
retreatHealthThreshold = 0.5f       // Huye al 50% HP
retreatCooldown = 10f               // Cooldown corto
coverSearchRadius = 20f             // Busca lejos
coverStayDuration = 6f              // Se esconde mucho tiempo
preferShieldOverCover = false       // Prefiere cobertura
```

### **NPC Normal (Mid Level):**
```csharp
useTacticalRetreat = true
retreatHealthThreshold = 0.3f       // Huye al 30% HP
retreatCooldown = 15f               // Cooldown medio
coverSearchRadius = 15f             // Busca cerca
coverStayDuration = 4f              // Tiempo moderado
preferShieldOverCover = false       // Equilibrado
```

### **NPC Agresivo (High Level):**
```csharp
useTacticalRetreat = true
retreatHealthThreshold = 0.2f       // Huye al 20% HP (muy resistente)
retreatCooldown = 20f               // Cooldown largo
coverSearchRadius = 12f             // Busca cerca
coverStayDuration = 2f              // Sale rápido
preferShieldOverCover = true        // Prefiere escudo
```

### **Boss (sin huida):**
```csharp
useTacticalRetreat = false          // No huye nunca
useShield = true                    // Solo usa escudo
// Bosses deben ser desafiantes y no huir
```

---

## 🧪 **TESTING**

### **Test 1: Activación por salud baja**
```
1. Iniciar combate con NPC (retreatHealthThreshold = 0.3)
2. Reducir HP del NPC a 29%
3. ✅ Verificar que inicia huida automáticamente
4. ✅ Verificar que encuentra cobertura
5. ✅ Verificar que navega hacia ella
```

### **Test 2: Búsqueda de cobertura**
```
1. Colocar NPC en área con árboles/rocas
2. Activar combate y reducir HP
3. ✅ Verificar que busca el objeto más cercano
4. ✅ Verificar Gizmos en Scene view (posiciones evaluadas)
5. ✅ Verificar que se posiciona DETRÁS del objeto
```

### **Test 3: Sin cobertura disponible (fallback escudo)**
```
1. Colocar NPC en área VACÍA (sin objetos)
2. Reducir HP a 29%
3. ✅ Verificar log "No se encontró cobertura"
4. ✅ Verificar que activa escudo como alternativa
5. ✅ Verificar animación "Defend_NoWeapon"
```

### **Test 4: Cooldown de huida**
```
1. Activar huida (HP: 29%)
2. Esperar a que salga de cobertura (4s)
3. ✅ Verificar cooldown activo (15s)
4. Reducir HP a 10%
5. ✅ Verificar que NO huye (cooldown activo)
6. Esperar 15s
7. ✅ Verificar que puede volver a huir
```

### **Test 5: Prioridad escudo vs cobertura**
```
Config A: preferShieldOverCover = true
  ✅ Debe activar escudo primero
  ✅ Solo busca cobertura si escudo no disponible

Config B: preferShieldOverCover = false
  ✅ Debe buscar cobertura primero
  ✅ Solo usa escudo si no hay cobertura
```

---

## 🎨 **GIZMOS DE DEBUG**

En **Scene View** cuando seleccionas el NPC:

### **Radio de búsqueda:**
- 🟠 Esfera naranja semitransparente = `coverSearchRadius`

### **Posición de cobertura:**
- 🟢 Esfera verde = Cobertura activa (llegó)
- 🟡 Esfera amarilla = Navegando hacia cobertura
- 🔵 Línea = Path desde NPC hasta cobertura

### **Objeto de cobertura:**
- 🔷 Wireframe cyan = Bounds del objeto usado

### **Posiciones evaluadas:**
- 🔴 Esferas rojas pequeñas = Todas las posiciones consideradas

**Activar/Desactivar:**
```csharp
// En NPCTacticalRetreat Inspector
Show Debug Gizmos: ✓ true / ☐ false
```

---

## 📈 **IMPACTO EN GAMEPLAY**

### **Antes (sin huida táctica):**
- ❌ NPC predecible - siempre ataca o se defiende en el mismo sitio
- ❌ Fácil de derrotar - solo spam de proyectiles
- ❌ Sin tensión - el player controla totalmente el combate
- ❌ NPC con salud baja = victoria garantizada

### **Ahora (con huida táctica):**
- ✅ NPC impredecible - cambia de posición estratégicamente
- ✅ Requiere persecución - el NPC huye y se reposiciona
- ✅ Tensión táctica - el NPC "piensa" y se adapta
- ✅ NPC con salud baja ≠ victoria fácil (puede sobrevivir más)
- ✅ Uso del entorno - árboles, rocas tienen propósito
- ✅ Combates más dinámicos y variados

---

## 🔄 **EQUILIBRIO (BALANCE)**

### **Ventanas de vulnerabilidad:**

Para evitar que el NPC sea **inmortal** o **frustante**:

1. **Cooldown largo (15s):**
   - Solo puede huir cada 15 segundos
   - No puede "spamear" huida constantemente

2. **Tiempo limitado en cobertura (4s):**
   - Debe salir después de 4 segundos
   - No puede quedarse escondido indefinidamente

3. **Búsqueda visible:**
   - El player VE al NPC corriendo hacia cobertura
   - Puede perseguirlo e interceptarlo

4. **Sin cobertura = escudo:**
   - Si no hay objetos, solo usa escudo
   - El escudo tiene su propio cooldown (10s)

5. **Salud sigue bajando:**
   - La cobertura NO regenera HP
   - Solo compra tiempo, no invulnerabilidad

---

## 🐛 **TROUBLESHOOTING**

### **Problema: El NPC no busca cobertura**
```
Verificar:
1. ✅ useTacticalRetreat = true en NPCCombatConfig
2. ✅ NPCTacticalRetreat component presente en GameObject
3. ✅ HP <= retreatHealthThreshold
4. ✅ Cooldown de huida no activo
5. ✅ Hay objetos en el área con las capas correctas
6. ✅ Logs en consola
```

### **Problema: No encuentra objetos de cobertura**
```
Verificar:
1. ✅ coverLayerMask incluye las capas correctas
2. ✅ Objetos tienen Collider (no Trigger)
3. ✅ coverSearchRadius suficientemente grande (15m+)
4. ✅ Objetos dentro del rango min/max (3-15m)
5. ✅ Objetos suficientemente grandes (>1m)
6. ✅ Ver Gizmos rojos en Scene view (posiciones evaluadas)
```

### **Problema: El NPC se queda atascado en cobertura**
```
Verificar:
1. ✅ coverStayDuration > 0
2. ✅ Coroutine ManageCoverState se ejecuta
3. ✅ No hay errores en consola
4. ✅ NPCTacticalRetreat.Update() funciona
5. ✅ StopRetreat() se llama correctamente
```

### **Problema: El NPC huye constantemente (spam)**
```
Verificar:
1. ✅ retreatCooldown >= 10 segundos
2. ✅ retreatHealthThreshold <= 0.4 (no muy alto)
3. ✅ UpdateRetreatCooldown() se llama en CombatLoop
4. ✅ _retreatCooldownTimer se actualiza correctamente
```

### **Problema: El NPC ignora la cobertura y atraviesa objetos**
```
Verificar:
1. ✅ NavMesh está bien configurado alrededor de los objetos
2. ✅ NavMeshAgent.areaMask incluye las áreas necesarias
3. ✅ No hay gaps en el NavMesh
4. ✅ NavMesh Obstacles en objetos grandes (opcional)
```

---

## 📝 **NOTAS TÉCNICAS**

### **1. Performance:**
- Se limita a evaluar máximo 10 objetos por intento (`maxCoverObjectsToCheck`)
- El Raycast solo se hace para objetos prometedores
- Los Gizmos se pueden desactivar en builds

### **2. NavMesh:**
- Requiere NavMesh configurado en la escena
- Usa `NavMesh.SamplePosition()` para validar posiciones
- El NavMeshAgent debe tener `areaMask` correcto

### **3. Física:**
- Usa `Physics.OverlapSphere()` para búsqueda inicial
- Usa `Physics.Raycast()` para verificar línea de visión
- Compatible con cualquier tipo de Collider

### **4. Integración con escudo:**
- Si está en cobertura, puede activar escudo adicional
- Ambos sistemas son independientes pero complementarios
- El escudo tiene su propio cooldown separado

### **5. Estados de combate:**
- Funciona con los 3 estados: Aggressive, Neutral, Defensive
- En Defensive, prioriza más la huida
- En Aggressive, usa huida solo como último recurso

---

## ✅ **CHECKLIST DE IMPLEMENTACIÓN**

- [x] ✅ Crear `NPCTacticalRetreat.cs`
- [x] ✅ Modificar `NPCCombatBrain.cs` (funciones de huida)
- [x] ✅ Modificar `NPCCombatConfig.cs` (campos de configuración)
- [x] ✅ Modificar `CombatState.cs` (mapeo de config)
- [x] ✅ Testing sin errores de compilación
- [ ] 🔲 Añadir NPCTacticalRetreat a NPCs
- [ ] 🔲 Configurar capas de cobertura en objetos
- [ ] 🔲 Configurar NPCCombatConfig
- [ ] 🔲 Testear en escenas con diferentes layouts
- [ ] 🔲 Ajustar parámetros de balance

---

## 🚀 **PRÓXIMOS PASOS**

### **1. En Unity (Setup):**
```
- Seleccionar NPC → Add Component → NPCTacticalRetreat
- Configurar Cover Layer Mask (Default, Environment, Props)
- Ajustar Cover Search Radius (15m recomendado)
- Activar Show Debug Gizmos
```

### **2. Configurar ScriptableObject:**
```
- NPCCombatConfig → 🏃 Huida Táctica y Cobertura
- Use Tactical Retreat: ✅ true
- Retreat Health Threshold: 0.3
- Retreat Cooldown: 15
- Prefer Shield Over Cover: ☐ false
```

### **3. Preparar escena:**
```
- Asegurar NavMesh en toda el área de combate
- Colocar objetos de cobertura (árboles, rocas)
- Asignar capas correctas a objetos
- Verificar Colliders en objetos
```

### **4. Testing inicial:**
```
- Iniciar combate
- Reducir HP del NPC a 29%
- Verificar búsqueda de cobertura
- Ver Gizmos en Scene view
- Ajustar parámetros según necesidad
```

### **5. Ajuste fino:**
```
- Probar con diferentes layouts de escena
- Ajustar scoring weights si es necesario
- Configurar diferentes perfiles por tipo de NPC
- Balance de cooldowns y duraciones
```

---

## 📚 **REFERENCIAS**

### **Archivos del proyecto:**
- `Assets/Scripts/Behaviour NPC/NPCTacticalRetreat.cs` (NUEVO)
- `Assets/Scripts/Behaviour NPC/NPCCombatBrain.cs` (MODIFICADO)
- `Assets/Scripts/Behaviour NPC/Modules/NPCCombatConfig.cs` (MODIFICADO)
- `Assets/Scripts/Behaviour NPC/States/CombatState.cs` (MODIFICADO)
- `Assets/Scripts/Behaviour NPC/NPCShieldController.cs` (EXISTENTE)

### **Documentación relacionada:**
- `SISTEMA_ESCUDO_NPC.md` - Sistema de escudo defensivo (complementario)
- `SISTEMA_COMBATE_NPC_COMPLETO.md` - Documentación general de combate

---

**¡Sistema de huida táctica completamente implementado!** 🎉

**Archivos modificados:** 3  
**Archivos nuevos:** 1  
**Errores de compilación:** 0  
**Warnings menores:** 3 (no afectan funcionalidad)  
**Estado:** ✅ LISTO PARA PROBAR EN UNITY

---

## 🎮 **EJEMPLO DE USO FINAL**

### **NPC Mago Pirata (Boy_Pirate):**

```
Configuración recomendada:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🛡️ Escudo Defensivo:
  ├─ Use Shield: ✅ true
  ├─ Shield Min Duration: 2s
  ├─ Shield Max Duration: 4s
  └─ Shield Cooldown: 10s

🏃 Huida Táctica:
  ├─ Use Tactical Retreat: ✅ true
  ├─ Retreat Health Threshold: 0.3 (30% HP)
  ├─ Retreat Cooldown: 15s
  ├─ Cover Search Radius: 15m
  ├─ Cover Stay Duration: 4s
  └─ Prefer Shield Over Cover: ☐ false

Comportamiento resultante:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
HP: 100-31% → Ataca normalmente
HP: ≤30%    → 🏃 Busca cobertura (árbol más cercano)
              → 🛡️ Activa escudo mientras escapa
              → ⏰ Permanece 4s
              → ⚔️ Vuelve al combate
              → 🔒 Cooldown 15s (no puede huir de nuevo)
              
Cooldowns:  → 🛡️ Usa escudo como defensa
Sin HP bajo → ⚔️ Sigue atacando
```

Esto crea un combate **dinámico, impredecible y desafiante** donde el NPC usa inteligentemente el entorno para sobrevivir. 🎯


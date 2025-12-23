# 🎯 DIAGRAMA VISUAL - SISTEMA DE HUIDA TÁCTICA

## 📊 FLUJO DE DECISIÓN COMPLETO

```
┌─────────────────────────────────────────────────────────────┐
│                    COMBATE EN PROGRESO                       │
│  NPC atacando normalmente con MagicLeft/Right/Special       │
└───────────────────────────┬─────────────────────────────────┘
                            │
                ┌───────────▼──────────────┐
                │  ¿Hay ataques listos?    │
                └─────┬────────────┬───────┘
                  SÍ  │            │  NO
              ┌───────▼──┐     ┌───▼──────────────────┐
              │ ATACAR   │     │ Evaluar ShouldRetreat│
              │ Normal   │     └───┬──────────────────┘
              └──────────┘         │
                                   │
                        ┌──────────▼────────────┐
                        │  ¿ShouldRetreat()?    │
                        │  - HP <= 30%?         │
                        │  - Sin recursos?      │
                        │  - Modo defensivo?    │
                        └──┬──────────┬─────────┘
                       NO  │          │ SÍ
                    ┌──────▼──┐   ┌──▼────────────────────────┐
                    │ Esperar │   │ ¿useTacticalRetreat?     │
                    │cooldowns│   │ ¿Cooldown <= 0?           │
                    └─────────┘   └──┬────────────────────────┘
                                     │ SÍ
                        ┌────────────▼────────────────┐
                        │ ¿preferShieldOverCover?     │
                        └──┬──────────────────┬───────┘
                       NO  │                  │ SÍ
                ┌──────────▼─────┐    ┌──────▼──────────────┐
                │ PRIORIDAD 1:   │    │ PRIORIDAD 1:        │
                │ Buscar         │    │ Activar Escudo      │
                │ Cobertura      │    │                     │
                └──┬──────────┬──┘    └──┬──────────────────┘
            Éxito  │          │ Fallo    │ Disponible
         ┌─────────▼──┐   ┌───▼──────────▼──────┐
         │ HUIDA A    │   │ PRIORIDAD 2:        │
         │ COBERTURA  │   │ Activar Escudo      │
         └────────────┘   └───┬─────────────────┘
                              │ No disponible
                       ┌──────▼────────────┐
                       │ PRIORIDAD 3:      │
                       │ Buscar Cobertura  │
                       │ (Fallback)        │
                       └──┬────────────────┘
                   Éxito  │      │ Fallo
              ┌───────────▼──┐   └─────────┐
              │ HUIDA A      │             │
              │ COBERTURA    │      ┌──────▼──────┐
              └──────────────┘      │ Sin defensa │
                                    │ disponible  │
                                    └─────────────┘
```

---

## 🏃 SECUENCIA DE HUIDA TÁCTICA (Timeline)

```
T=0s   │ ⚔️ NPC atacando normalmente (HP: 100%)
       │
T=15s  │ 🎯 Player ataca constantemente
       │ ⚡ HP: 100% → 80% → 60% → 40%
       │
T=20s  │ ⚠️ HP baja a 28% (< umbral 30%)
       │ 🧠 ShouldRetreat() = TRUE
       │ 🔍 Iniciando búsqueda de cobertura...
       │
T=20.2s│ 🌳 NPCTacticalRetreat.FindNearestCover()
       │    ├─ OverlapSphere (radio 15m)
       │    ├─ 8 objetos encontrados
       │    ├─ Evaluando posiciones...
       │    ├─ Pine_Tree_02: Score 74.5 ✅
       │    ├─ Rock_Large_01: Score 58.2
       │    └─ Oak_Tree_01: Score 62.8
       │
T=20.5s│ ✅ Cobertura elegida: Pine_Tree_02
       │ 🎯 Destino calculado: (12.5, 0.2, 34.8)
       │ 🏃 NavMeshAgent.SetDestination()
       │ 📝 _isRetreating = true
       │ ⏰ _retreatCooldownTimer = 15s
       │
T=20.5s│ 🏃💨 NPC corriendo hacia árbol
→23s   │ 📊 Distancia: 8m → 6m → 4m → 2m → 0.5m
       │ 👁️ Player puede ver al NPC huyendo
       │
T=23s  │ ✅ Llegó a cobertura (distancia < 1.5m)
       │ 🛑 NavMeshAgent.isStopped = true
       │ 🛡️ _isBehindCover = true
       │ ⏰ _coverStayTimer = 4s
       │
T=23s  │ 🛡️ TryActivateShield() (defensa adicional)
       │ ✨ Escudo activado (duración: 3s)
       │
T=23-27s│ 🕐 Permanece en cobertura
       │ 🌳 Detrás del árbol
       │ 🛡️ Escudo activo (T=23-26s)
       │ 👁️ Jugador NO tiene línea de visión
       │ ❤️ HP: 28% (sin regeneración)
       │
T=26s  │ 🛡️ Escudo desactivado (duración agotada)
       │ 🌳 Sigue en cobertura (1s restante)
       │
T=27s  │ ⏰ _coverStayTimer <= 0
       │ ✅ StopRetreat()
       │ 📝 _isRetreating = false
       │ 📝 _isBehindCover = false
       │ 🔒 Cooldown activo: 15s
       │
T=27s  │ ⚔️ VUELVE AL COMBATE
→      │ 🎯 Busca al jugador
       │ ⚔️ Reanuda ataques normales
       │ ❤️ HP: 28% (sigue vulnerable)
       │
T=28-42s│ 🔒 Cooldown de huida activo
       │ ⚔️ Sigue combatiendo normalmente
       │ ❌ NO puede huir aunque HP < 30%
       │
T=42s  │ ⏰ Cooldown completado
       │ ✅ Puede huir de nuevo si HP < 30%
```

---

## 🧠 SISTEMA DE SCORING (Evaluación de Cobertura)

```
Para cada objeto candidato:
┌─────────────────────────────────────────────┐
│  📐 FACTOR 1: Distancia al NPC              │
│  ────────────────────────────────────────   │
│  Óptimo: 5-10m                              │
│  Score: 0-30 puntos                         │
│                                             │
│  Demasiado cerca (<3m)    → Rechazado      │
│  Muy cerca (3-5m)         → 15 pts         │
│  Distancia ideal (5-10m)  → 30 pts ✅      │
│  Lejos (10-15m)           → 20 pts         │
│  Demasiado lejos (>15m)   → Rechazado      │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│  🎯 FACTOR 2: Distancia al Jugador          │
│  ────────────────────────────────────────   │
│  Óptimo: ~10m del jugador                   │
│  Score: 0-20 puntos                         │
│                                             │
│  Muy cerca del jugador (<5m)  → 5 pts      │
│  Cerca (5-8m)                 → 15 pts     │
│  Distancia ideal (8-12m)      → 20 pts ✅  │
│  Lejos (12-20m)               → 10 pts     │
│  Muy lejos (>20m)             → 5 pts      │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│  📦 FACTOR 3: Tamaño del Objeto             │
│  ────────────────────────────────────────   │
│  Más grande = Mejor cobertura               │
│  Score: 0-20 puntos                         │
│                                             │
│  Objeto pequeño (<2m)     → 4 pts          │
│  Objeto mediano (2-5m)    → 10 pts         │
│  Objeto grande (5-8m)     → 16 pts ✅      │
│  Objeto enorme (>8m)      → 20 pts ✅✅    │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│  🧭 FACTOR 4: Dirección de Huida            │
│  ────────────────────────────────────────   │
│  Alejándose del jugador = Mejor             │
│  Score: 0-15 puntos                         │
│                                             │
│  Hacia jugador (dot > 0.5)    → 0 pts      │
│  Lateral (dot 0 a 0.5)        → 5 pts      │
│  Alejándose (dot -0.5 a 0)    → 10 pts     │
│  Directamente opuesto (< -0.5)→ 15 pts ✅  │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│  🏆 SCORE TOTAL                             │
│  ────────────────────────────────────────   │
│  Score = Factor1 + Factor2 + Factor3 + F4   │
│  Rango: 0-85 puntos                         │
│                                             │
│  Excelente (>70 pts)  → ⭐⭐⭐⭐⭐         │
│  Bueno (50-70 pts)    → ⭐⭐⭐⭐           │
│  Aceptable (30-50)    → ⭐⭐⭐             │
│  Malo (<30 pts)       → ⭐⭐               │
└─────────────────────────────────────────────┘
```

### **Ejemplo real de evaluación:**

```
Escena: Bosque con múltiples árboles
NPC en posición: (5, 0, 10)
Jugador en posición: (15, 0, 15)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🌳 CANDIDATO A: Pine_Tree_02
   Posición: (12, 0, 35)
   ────────────────────────────────────
   Factor 1 - Dist NPC: 7m     → 28 pts
   Factor 2 - Dist Player: 11m → 18 pts
   Factor 3 - Tamaño: 6m       → 12 pts
   Factor 4 - Dirección: -0.7  → 12 pts
   ────────────────────────────────────
   SCORE TOTAL: 70 pts ⭐⭐⭐⭐⭐
   Estado: ✅ ELEGIDO

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🪨 CANDIDATO B: Rock_Large_01
   Posición: (8, 0, 18)
   ────────────────────────────────────
   Factor 1 - Dist NPC: 9m     → 26 pts
   Factor 2 - Dist Player: 8m  → 16 pts
   Factor 3 - Tamaño: 3m       → 6 pts
   Factor 4 - Dirección: 0.1   → 5 pts
   ────────────────────────────────────
   SCORE TOTAL: 53 pts ⭐⭐⭐⭐

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🌳 CANDIDATO C: Oak_Tree_01
   Posición: (3, 0, 12)
   ────────────────────────────────────
   Factor 1 - Dist NPC: 3m     → 15 pts
   Factor 2 - Dist Player: 13m → 12 pts
   Factor 3 - Tamaño: 5m       → 10 pts
   Factor 4 - Dirección: -0.4  → 8 pts
   ────────────────────────────────────
   SCORE TOTAL: 45 pts ⭐⭐⭐

RESULTADO: Pine_Tree_02 elegido (score más alto)
```

---

## 🎮 ESTADOS DEL NPC (Diagrama de Estados)

```
┌─────────────────────────────────────────────────┐
│            ESTADO: COMBATE NORMAL               │
│  ⚔️ Ataque activo                               │
│  🎯 Persiguiendo al jugador                      │
│  ❤️ HP: 100% - 31%                              │
│  ────────────────────────────────────────────   │
│  Ataques: MagicLeft, MagicRight, MagicSpecial   │
│  Movimiento: Circular/Approach/Retreat          │
│  Cooldowns: Gestionados individualmente         │
└──────────────┬──────────────────────────────────┘
               │
               │ Trigger: HP <= 30%
               │ Condición: useTacticalRetreat = true
               │ Condición: retreatCooldownTimer <= 0
               │
               ▼
┌─────────────────────────────────────────────────┐
│         ESTADO: EVALUANDO HUIDA                 │
│  🧠 Decisión táctica                            │
│  ────────────────────────────────────────────   │
│  1. Evaluar ShouldRetreat()                     │
│  2. Verificar cooldown                          │
│  3. Decidir prioridades                         │
│  Duración: ~0.5s                                │
└──────────────┬──────────────────────────────────┘
               │
               │ Decisión: Buscar cobertura
               │
               ▼
┌─────────────────────────────────────────────────┐
│       ESTADO: BUSCANDO COBERTURA                │
│  🔍 Escaneando área                             │
│  ────────────────────────────────────────────   │
│  1. OverlapSphere(coverSearchRadius)            │
│  2. Evaluar cada objeto (scoring)               │
│  3. Seleccionar mejor opción                    │
│  4. Calcular posición detrás del objeto         │
│  Duración: ~0.2-0.5s                            │
└──────────────┬──────────────────────────────────┘
               │
               │ Resultado: Cobertura encontrada
               │
               ▼
┌─────────────────────────────────────────────────┐
│         ESTADO: HUYENDO A COBERTURA             │
│  🏃💨 Navegando                                  │
│  ────────────────────────────────────────────   │
│  • NavMeshAgent activo                          │
│  • Velocidad máxima                             │
│  • Vulnerable a ataques                         │
│  • Visible al jugador                           │
│  Duración: 2-4s (depende de distancia)          │
└──────────────┬──────────────────────────────────┘
               │
               │ Trigger: distanceToCover < 1.5m
               │
               ▼
┌─────────────────────────────────────────────────┐
│       ESTADO: DETRÁS DE COBERTURA               │
│  🌳🛡️ Protegido                                 │
│  ────────────────────────────────────────────   │
│  • NavMeshAgent.isStopped = true                │
│  • Posición fija                                │
│  • Línea de visión bloqueada                    │
│  • Puede activar escudo adicional               │
│  • Timer: coverStayDuration (4s)                │
│  Duración: 4s fijos                             │
└──────────────┬──────────────────────────────────┘
               │
               │ Trigger: coverStayTimer <= 0
               │
               ▼
┌─────────────────────────────────────────────────┐
│      ESTADO: VOLVIENDO AL COMBATE               │
│  ⚔️ Reactivando                                  │
│  ────────────────────────────────────────────   │
│  • StopRetreat()                                │
│  • Cooldown de huida: 15s                       │
│  • Buscar jugador                               │
│  • Reanudar ataques                             │
│  Duración: Instantáneo                          │
└──────────────┬──────────────────────────────────┘
               │
               │ Vuelve a estado inicial
               │
               ▼
┌─────────────────────────────────────────────────┐
│         ESTADO: COMBATE CON COOLDOWN            │
│  ⚔️ Combatiendo normalmente                     │
│  🔒 No puede huir (cooldown activo: 15s)        │
│  ────────────────────────────────────────────   │
│  Igual que COMBATE NORMAL pero:                 │
│  • retreatCooldownTimer > 0                     │
│  • NO puede activar huida aunque HP < 30%       │
│  Duración: 15s (hasta que cooldown = 0)         │
└─────────────────────────────────────────────────┘
```

---

## 📁 ARQUITECTURA DE COMPONENTES

```
┌─────────────────────────────────────────────────────────────┐
│                    NPC GameObject                           │
│  ┌────────────┐  ┌────────────┐  ┌──────────────────┐     │
│  │  Transform │  │ NavMesh    │  │   Animator       │     │
│  │            │  │  Agent     │  │                  │     │
│  └────────────┘  └────────────┘  └──────────────────┘     │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │        NPCBehaviourManagerV2 (Orquestador)          │  │
│  │  ┌────────────────────────────────────────────────┐ │  │
│  │  │  NPCStateContext                               │ │  │
│  │  │  - Player reference                            │ │  │
│  │  │  - Agent, Animator, Transform refs             │ │  │
│  │  └────────────────────────────────────────────────┘ │  │
│  └──────────────────┬───────────────────────────────────┘  │
│                     │                                       │
│  ┌──────────────────▼───────────────────────────────────┐  │
│  │         CombatState (Estado actual)                  │  │
│  │  - Gestiona transiciones                            │  │
│  │  - Configura NPCCombatBrain                         │  │
│  └──────────────────┬───────────────────────────────────┘  │
│                     │                                       │
│  ┌──────────────────▼───────────────────────────────────┐  │
│  │    NPCCombatBrain ⭐ (Cerebro táctico)              │  │
│  │  ┌─────────────────────────────────────────────┐    │  │
│  │  │ • TryExecuteAttack()                        │    │  │
│  │  │ • UpdateCombatState()                       │    │  │
│  │  │ • ComputeCirclePosition()                   │    │  │
│  │  │ • ShouldRetreat() ⭐ NUEVO                 │    │  │
│  │  │ • TryFindAndMoveToCover() ⭐ NUEVO         │    │  │
│  │  │ • ManageCoverState() ⭐ NUEVO              │    │  │
│  │  │ • UpdateRetreatCooldown() ⭐ NUEVO         │    │  │
│  │  │ • TryActivateShield()                       │    │  │
│  │  └─────────────────────────────────────────────┘    │  │
│  └──────────────────┬───────────────────────────────────┘  │
│                     │                                       │
│  ┌──────────────────▼───────────────────────────────────┐  │
│  │   NPCTacticalRetreat ⭐ NUEVO                       │  │
│  │  ┌─────────────────────────────────────────────┐    │  │
│  │  │ • StartRetreat(player)                      │    │  │
│  │  │ • StopRetreat()                             │    │  │
│  │  │ • FindNearestCover()                        │    │  │
│  │  │ • CalculateCoverScore()                     │    │  │
│  │  │ • DoesObjectBlockLineOfSight()              │    │  │
│  │  │ • HasLineOfSight()                          │    │  │
│  │  └─────────────────────────────────────────────┘    │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │        NPCShieldController (Complementario)          │  │
│  │  • StartDefending(duration)                          │  │
│  │  • StopDefending()                                   │  │
│  │  • OnProjectileHit()                                 │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │          Damageable (Salud)                          │  │
│  │  • Current HP                                        │  │
│  │  • Max HP                                            │  │
│  │  • TakeDamage()                                      │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│              NPCCombatConfig (ScriptableObject)             │
│  ┌────────────────────────────────────────────────────┐     │
│  │  Configuración estática del NPC                   │     │
│  │  ────────────────────────────────────────────────  │     │
│  │  • Ataques y cooldowns                            │     │
│  │  • Rangos de combate                              │     │
│  │  • useShield, shieldCooldown                      │     │
│  │  • useTacticalRetreat ⭐ NUEVO                   │     │
│  │  • retreatHealthThreshold ⭐ NUEVO               │     │
│  │  • coverSearchRadius ⭐ NUEVO                    │     │
│  │  • coverLayerMask ⭐ NUEVO                       │     │
│  │  • preferShieldOverCover ⭐ NUEVO                │     │
│  └────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

---

## 🎯 RELACIÓN ENTRE SISTEMAS

```
        SISTEMA DE ESCUDO                SISTEMA DE HUIDA
    (NPCShieldController)            (NPCTacticalRetreat)
              │                                │
              └────────┬───────────────────────┘
                       │
                       │ Coordinados por
                       │
                       ▼
              ┌────────────────┐
              │ NPCCombatBrain │
              └────────┬───────┘
                       │
          ┌────────────┼────────────┐
          │            │            │
    ┌─────▼────┐  ┌───▼───┐  ┌────▼─────┐
    │Ataque    │  │Escudo │  │Cobertura │
    │Normal    │  │       │  │          │
    └──────────┘  └───────┘  └──────────┘

Prioridades según preferShieldOverCover:

FALSE (prefiere cobertura):
1️⃣ Buscar cobertura
2️⃣ Activar escudo (si falla #1)
3️⃣ Buscar cobertura (fallback)

TRUE (prefiere escudo):
1️⃣ Activar escudo
2️⃣ Buscar cobertura (si escudo no disponible)
```

---

## 📊 MÉTRICAS Y BALANCE

```
╔═══════════════════════════════════════════════════════════╗
║              TABLA DE CONFIGURACIONES                     ║
╠═══════════════════════════════════════════════════════════╣
║  Parámetro              │ Cobarde │ Normal │ Agresivo     ║
║──────────────────────────┼─────────┼────────┼──────────── ║
║  retreatHealthThreshold  │  0.5    │  0.3   │   0.2       ║
║  retreatCooldown         │  10s    │  15s   │   20s       ║
║  coverSearchRadius       │  20m    │  15m   │   12m       ║
║  coverStayDuration       │  6s     │  4s    │   2s        ║
║  preferShieldOverCover   │  false  │  false │   true      ║
╚═══════════════════════════════════════════════════════════╝

╔═══════════════════════════════════════════════════════════╗
║           VENTANAS DE VULNERABILIDAD                      ║
╠═══════════════════════════════════════════════════════════╣
║  Momento                    │ Duración │ Puede atacar     ║
║─────────────────────────────┼──────────┼──────────────────║
║  Evaluando huida            │  0.5s    │  ❌ No           ║
║  Buscando cobertura         │  0.5s    │  ❌ No           ║
║  Corriendo hacia cobertura  │  2-4s    │  ❌ No (vulnerable)║
║  Detrás de cobertura        │  4s      │  ❌ No           ║
║  Volviendo al combate       │  0.5s    │  ❌ No           ║
║  Cooldown activo            │  15s     │  ✅ Sí           ║
║─────────────────────────────┼──────────┼──────────────────║
║  TOTAL VENTANA VULNERABLE   │  ~7-10s  │  Puede morir     ║
╚═══════════════════════════════════════════════════════════╝
```

---

## ✅ CHECKLIST VISUAL

```
SETUP EN UNITY:
┌─ NPCTacticalRetreat Component
│  ├─ [✓] Añadido al GameObject
│  ├─ [✓] Cover Search Radius: 15
│  ├─ [✓] Cover Layer Mask configurado
│  └─ [✓] Show Debug Gizmos: true
│
├─ NPCCombatConfig (ScriptableObject)
│  ├─ [✓] Use Tactical Retreat: true
│  ├─ [✓] Retreat Health Threshold: 0.3
│  ├─ [✓] Retreat Cooldown: 15
│  └─ [✓] Prefer Shield Over Cover: false
│
├─ Escena
│  ├─ [✓] Objetos de cobertura presentes
│  ├─ [✓] Colliders configurados
│  ├─ [✓] NavMesh configurado
│  └─ [✓] Capas asignadas
│
└─ Testing
   ├─ [✓] Activación al 30% HP
   ├─ [✓] Búsqueda exitosa
   ├─ [✓] Navegación correcta
   ├─ [✓] Permanencia en cobertura
   ├─ [✓] Cooldown funciona
   └─ [✓] Logs en Console
```

---

**Documento creado:** 2025-12-23  
**Versión:** 1.0  
**Estado:** ✅ Sistema completo y funcional  

📚 Ver también:
- SISTEMA_HUIDA_TACTICA_NPC.md (Documentación completa)
- GUIA_RAPIDA_HUIDA_TACTICA.md (Setup en 5 minutos)
- CHECKLIST_SETUP_HUIDA_TACTICA.md (Checklist detallado)


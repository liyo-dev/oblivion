# 🎯 SOLUCIÓN ENCONTRADA: NavMeshObstacle en Árboles

## 🐛 Problema Identificado

**CAUSA RAÍZ**: Los árboles del bosque tienen `NavMeshObstacle` mal configurados que **empujan físicamente** a los NPCs, causando:
- Velocidades masivas (200-500 unidades/s)
- Movimiento constante incluso en IdleState
- `isStopped = False` constantemente reactivado

### Confirmación:
- ✅ Araña sin scripts de NPC → también se movía
- ✅ Quitar NavMeshObstacle → funciona bien
- ✅ Todos los NPCs cerca de árboles → afectados

## ✅ Solución: Configurar NavMeshObstacle Correctamente

### Problema con NavMeshObstacle:
Cuando un `NavMeshObstacle` está mal configurado con **Carve activo y Move Threshold bajo**, el NavMesh se regenera constantemente, empujando a los agentes.

### Configuración Correcta para Árboles (Obstáculos Estáticos):

| Propiedad | Valor Correcto | Por qué |
|-----------|----------------|---------|
| **Carve** | ✅ True | Los árboles deben "tallar" agujeros en el NavMesh |
| **Move Threshold** | 🔥 1000 (MUY ALTO) | Los árboles NO se mueven, evita updates |
| **Carve Only Stationary** | ✅ True | Solo hace carve cuando está quieto |
| **Time To Stationary** | 0.1s (bajo) | Considerado quieto inmediatamente |

## 🔧 Cómo Arreglarlo

### Método 1: Script Automático (RECOMENDADO) ⭐

1. **Crear GameObject vacío** en la escena: `NavMeshObstacleFixer`

2. **Añadir componente**: `NavMeshObstacleFixer.cs`

3. **Configurar**:
   - `Search Root`: Arrastra el GameObject "Woods" o "Trees" del bosque
   - `Use Carving`: ✅ True
   - `Move Threshold`: 1000
   - `Carve Only Stationary`: ✅ True
   - `Time To Stationary`: 0.1

4. **En el Inspector**, hacer clic en:
   - **"🔧 Arreglar Todos los NavMeshObstacle"**

5. **¡Listo!** Todos los árboles configurados correctamente.

### Método 2: Manual (para pocos árboles)

1. Seleccionar el árbol en la jerarquía
2. En Inspector → **NavMeshObstacle**:
   - Carve: ✅
   - Carving Move Threshold: **1000**
   - Carve Only Stationary: ✅
   - Carving Time To Stationary: **0.1**
3. Repetir para cada árbol

### Método 3: Script en Prefab (para nuevos árboles)

Si los árboles son prefabs, edita el prefab:
1. Abrir el prefab del árbol
2. Seleccionar NavMeshObstacle
3. Aplicar la configuración correcta
4. Guardar el prefab
5. Los cambios se aplicarán a todas las instancias

## 📊 Verificación

### Antes del Fix:
```
NPC cerca de árboles:
- Agent.velocity: 200-500 unidades/s ❌
- Agent.isStopped: False ❌
- Movimiento constante ❌
```

### Después del Fix:
```
NPC cerca de árboles:
- Agent.velocity: 0 unidades/s ✅
- Agent.isStopped: True ✅
- Quieto en IdleState ✅
```

## 🎮 Uso del Script NavMeshObstacleFixer

### Funciones Disponibles:

#### 1. **Fix All NavMeshObstacles**
- Busca TODOS los NavMeshObstacle en la escena (o en Search Root)
- Configura cada uno con los valores correctos
- Reporta cuántos fueron arreglados

#### 2. **Add NavMeshObstacle to Trees**
- Busca todos los GameObjects con "Tree" en el nombre
- Añade NavMeshObstacle si no tienen uno
- Configura correctamente desde el inicio
- Intenta ajustar el tamaño automáticamente

### Ejemplo de Log:
```
[ObstacleFixer] 🔍 Buscando en 'Woods' y sus hijos...
[ObstacleFixer] 🔧 Tree02_a01 (157): carving=True moveThreshold=1000 carveOnlyStationary=True
[ObstacleFixer] 🔧 Tree02_a01 (13): carving=True moveThreshold=1000 carveOnlyStationary=True
[ObstacleFixer] ✅ 157/157 NavMeshObstacle configurados correctamente.
```

## 🔍 Alternativa: Usar Colliders en lugar de NavMeshObstacle

Si los NavMeshObstacle siguen causando problemas:

### Opción A: Bake NavMesh con exclusiones
1. Window → AI → Navigation
2. En la pestaña "Object":
   - Seleccionar los árboles
   - Marcar como "Navigation Static"
   - Navigation Area: "Not Walkable"
3. Rebake el NavMesh
4. **Quitar** los NavMeshObstacle

### Opción B: Solo Colliders
1. Asegurar que los árboles tienen **Colliders**
2. **Quitar** todos los NavMeshObstacle
3. El NavMeshAgent evitará los árboles por obstacle avoidance
4. Ajustar NavMeshAgent:
   - Radius: 0.5
   - Obstacle Avoidance Type: Good Quality
   - Avoidance Priority: 50

## ⚠️ Problemas Conocidos

### Si los NPCs siguen atravesando árboles:

1. **Verificar NavMesh**:
   - Window → AI → Navigation → Bake
   - "Agent Radius" debe ser apropiado (0.5 - 1.0)
   - Rebakear el NavMesh

2. **Verificar Colliders**:
   - Los árboles deben tener colliders
   - Colliders en la capa correcta (Default)
   - Colliders no marcados como Trigger

3. **Verificar NavMeshAgent**:
   - Obstacle Avoidance Type: High Quality
   - Radius: similar al del bake (0.5)

## 📁 Archivos del Fix

1. **NavMeshObstacleFixer.cs** - Script principal (NUEVO)
2. Este documento de referencia

## 🎉 Resultado Final

Con la configuración correcta:
- ✅ Los árboles bloquean el path de los NPCs
- ✅ Los NPCs no son empujados físicamente
- ✅ Los NPCs permanecen quietos en IdleState
- ✅ No hay velocidad residual masiva
- ✅ El bosque es navegable correctamente

---

**Fecha**: 2025-01-27
**Problema**: NavMeshObstacle mal configurado en árboles
**Solución**: NavMeshObstacleFixer.cs + configuración correcta
**Estado**: ✅ RESUELTO

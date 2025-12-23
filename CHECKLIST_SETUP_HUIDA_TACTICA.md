# ✅ CHECKLIST DE SETUP - SISTEMA DE HUIDA TÁCTICA

## 📋 IMPLEMENTACIÓN COMPLETADA (Ya hecho en código)

- [x] ✅ Crear `NPCTacticalRetreat.cs` (382 líneas)
- [x] ✅ Modificar `NPCCombatBrain.cs` (añadir funciones de huida)
- [x] ✅ Modificar `NPCCombatConfig.cs` (añadir campos configurables)
- [x] ✅ Modificar `CombatState.cs` (mapear configuración)
- [x] ✅ Testing de compilación (0 errores)
- [x] ✅ Crear documentación completa
- [x] ✅ Crear guía rápida
- [x] ✅ Crear resumen ejecutivo

---

## 🎮 PENDIENTE EN UNITY (Debes hacer tú)

### **Por cada NPC que quieras que use huida táctica:**

#### **Paso 1: Añadir componente (30 segundos)**
- [ ] Abrir Unity
- [ ] Seleccionar GameObject del NPC en Hierarchy
- [ ] Inspector → Add Component
- [ ] Buscar "NPCTacticalRetreat"
- [ ] Click "Add Component"

#### **Paso 2: Configurar NPCTacticalRetreat (1 minuto)**
- [ ] Cover Search Radius: **15** (metros)
- [ ] Cover Layer Mask: Seleccionar **Default**, **Environment**, **Props**
- [ ] Min Cover Distance: **3** (metros)
- [ ] Max Cover Distance: **15** (metros)
- [ ] Cover Stay Duration: **4** (segundos)
- [ ] Cover Distance Behind: **1.5** (metros)
- [ ] Show Debug Gizmos: **✓ true** (para debugging)
- [ ] Max Cover Objects To Check: **10** (performance)

#### **Paso 3: Configurar NPCCombatConfig (2 minutos)**
- [ ] Buscar el ScriptableObject `NPCCombatConfig` del NPC en Project
- [ ] Abrir en Inspector
- [ ] Ir a sección **🏃 Huida Táctica y Cobertura**
- [ ] **Use Tactical Retreat:** ✅ **true**
- [ ] **Retreat Health Threshold:** **0.3** (huye al 30% HP)
- [ ] **Retreat Cooldown:** **15** (segundos entre huidas)
- [ ] **Cover Search Radius:** **15** (metros)
- [ ] **Cover Layer Mask:** Seleccionar **Default**, **Environment**, **Props**
- [ ] **Min Cover Distance:** **3** (metros)
- [ ] **Max Cover Distance:** **15** (metros)
- [ ] **Cover Stay Duration:** **4** (segundos en cobertura)
- [ ] **Prefer Shield Over Cover:** ☐ **false** (prefiere cobertura)

---

## 🌳 PREPARAR ESCENA (Solo si es necesario)

### **Verificar objetos de cobertura:**
- [ ] Hay árboles, rocas, o edificios en la escena
- [ ] Objetos tienen **Collider** (Box, Sphere, o Mesh)
- [ ] Colliders **NO son Trigger**
- [ ] Objetos están en capas **Default**, **Environment**, o **Props**
- [ ] Objetos tienen tamaño suficiente (mínimo 1m de radio)

### **Verificar NavMesh:**
- [ ] NavMesh configurado en el área de combate
- [ ] NavMesh rodea los objetos de cobertura
- [ ] No hay gaps grandes en el NavMesh
- [ ] NPC puede navegar libremente

### **Opcional - Organizar capas:**
Si aún no tienes capas específicas:
- [ ] Edit → Project Settings → Tags and Layers
- [ ] Crear capa "Environment" (si no existe)
- [ ] Crear capa "Props" (si no existe)
- [ ] Asignar capas a objetos del entorno

---

## 🧪 TESTING INICIAL (5 minutos)

### **Test 1: Activación básica**
- [ ] Iniciar Play Mode
- [ ] Acercarse al NPC para iniciar combate
- [ ] Atacar al NPC hasta que tenga 29% HP
- [ ] ✅ **Verificar:** NPC busca cobertura automáticamente
- [ ] ✅ **Verificar:** Log en Console: "🏃 Salud baja (29%), activando huida táctica"

### **Test 2: Búsqueda de cobertura**
- [ ] Con NPC en huida
- [ ] Cambiar a Scene View
- [ ] Seleccionar NPC en Hierarchy
- [ ] ✅ **Verificar:** Esfera naranja (radio de búsqueda)
- [ ] ✅ **Verificar:** Línea azul hacia cobertura
- [ ] ✅ **Verificar:** Esfera verde/amarilla en destino

### **Test 3: Llegada a cobertura**
- [ ] Esperar a que NPC llegue a la cobertura
- [ ] ✅ **Verificar:** NPC se posiciona DETRÁS del objeto
- [ ] ✅ **Verificar:** Log: "✅ Llegó a cobertura, permanecerá por 4s"
- [ ] ✅ **Verificar:** NPC se queda quieto 4 segundos
- [ ] ✅ **Verificar:** NPC vuelve al combate después de 4s

### **Test 4: Sin cobertura (fallback)**
- [ ] Mover NPC a área vacía (sin objetos)
- [ ] Reducir HP a 29%
- [ ] ✅ **Verificar:** Log: "❌ No se encontró cobertura disponible"
- [ ] ✅ **Verificar:** NPC activa escudo como alternativa
- [ ] ✅ **Verificar:** Animación "Defend_NoWeapon"

### **Test 5: Cooldown**
- [ ] Activar huida una vez
- [ ] Esperar a que salga de cobertura
- [ ] Reducir HP a 10% (debajo de umbral)
- [ ] ✅ **Verificar:** NPC NO huye (cooldown activo)
- [ ] ✅ **Verificar:** Log: "🔒 Cooldown de huida: X.Xs"
- [ ] Esperar 15 segundos
- [ ] ✅ **Verificar:** Puede volver a huir

---

## 🎯 NPCs RECOMENDADOS PARA PROBAR

### **Prioridad Alta:**
- [ ] **Boy_Pirate** (NPC principal del proyecto)
  - Configuración: Normal (0.3 threshold, 15s cooldown)

### **Prioridad Media:**
- [ ] Otros NPCs mágicos/a distancia
  - Configuración según dificultad del NPC

### **Prioridad Baja:**
- [ ] NPCs cuerpo a cuerpo (opcional)
  - Puede funcionar pero es menos natural para melee

---

## 🐛 PROBLEMAS COMUNES Y SOLUCIONES

### **❌ NPC no busca cobertura**
- [ ] Verificar: `useTacticalRetreat = true` en config
- [ ] Verificar: NPCTacticalRetreat component presente
- [ ] Verificar: HP <= threshold (30%)
- [ ] Verificar: Cooldown no activo
- [ ] Ver logs en Console para más detalles

### **❌ No encuentra objetos**
- [ ] Verificar: Cover Layer Mask incluye capas correctas
- [ ] Verificar: Objetos tienen Collider
- [ ] Verificar: Cover Search Radius >= 10m
- [ ] Verificar: Objetos dentro de rango 3-15m
- [ ] Aumentar Cover Search Radius a 20m

### **❌ NPC se queda atascado**
- [ ] Verificar: NavMesh correctamente configurado
- [ ] Verificar: Cover Stay Duration > 0
- [ ] Verificar: No hay errores en Console
- [ ] Reconfigurar NavMesh en el área

### **❌ Warnings en compilación**
- [ ] Ignorar warnings de naming convention (no afectan)
- [ ] Ignorar warnings de serialized fields (no afectan)

---

## 📊 AJUSTE FINO (Después del testing básico)

### **Si el NPC huye demasiado:**
- [ ] Aumentar `Retreat Health Threshold` (ej: 0.2 en vez de 0.3)
- [ ] Aumentar `Retreat Cooldown` (ej: 20s en vez de 15s)
- [ ] Reducir `Cover Stay Duration` (ej: 2s en vez de 4s)

### **Si el NPC no huye suficiente:**
- [ ] Reducir `Retreat Health Threshold` (ej: 0.4 en vez de 0.3)
- [ ] Reducir `Retreat Cooldown` (ej: 10s en vez de 15s)
- [ ] Aumentar `Cover Stay Duration` (ej: 6s en vez de 4s)

### **Si el combate es muy fácil:**
- [ ] Activar `Prefer Shield Over Cover = true` (más defensa)
- [ ] Reducir `Retreat Cooldown` (huye más frecuentemente)
- [ ] Aumentar `Cover Stay Duration` (se esconde más tiempo)

### **Si el combate es muy difícil/frustrante:**
- [ ] Desactivar `Use Tactical Retreat = false` temporalmente
- [ ] Aumentar `Retreat Cooldown` a 20-30s
- [ ] Reducir `Cover Stay Duration` a 2-3s
- [ ] Activar solo para bosses/NPCs difíciles

---

## 📈 MÉTRICAS DE ÉXITO

Después del testing, el sistema debe cumplir:

- [ ] ✅ NPC busca cobertura al ≤30% HP
- [ ] ✅ NPC llega a cobertura en 2-4 segundos
- [ ] ✅ NPC permanece 4 segundos en cobertura
- [ ] ✅ NPC vuelve al combate después
- [ ] ✅ Cooldown de 15s se respeta
- [ ] ✅ Fallback a escudo si no hay cobertura
- [ ] ✅ Gizmos visibles en Scene view
- [ ] ✅ Logs claros en Console
- [ ] ✅ No hay errores ni excepciones
- [ ] ✅ Combate se siente más dinámico y desafiante

---

## 📚 DOCUMENTACIÓN DE REFERENCIA

Si tienes dudas durante el setup:

- **GUIA_RAPIDA_HUIDA_TACTICA.md** - Guía de 5 minutos
- **SISTEMA_HUIDA_TACTICA_NPC.md** - Documentación completa (750+ líneas)
- **RESUMEN_SISTEMA_HUIDA_TACTICA.md** - Resumen ejecutivo
- **SISTEMA_ESCUDO_NPC.md** - Sistema de escudo (complementario)

---

## 🎉 CHECKLIST FINAL

Cuando todo esté completo:

- [ ] ✅ NPCTacticalRetreat añadido a NPCs
- [ ] ✅ NPCCombatConfig configurado
- [ ] ✅ Escena preparada (NavMesh, objetos)
- [ ] ✅ Testing básico completado
- [ ] ✅ Sin errores en Console
- [ ] ✅ Comportamiento como esperado
- [ ] ✅ Ajuste fino realizado
- [ ] ✅ Sistema funcionando perfectamente

---

**Estado actual:** ✅ Código completamente implementado  
**Pendiente:** 🎮 Setup en Unity (5-10 minutos por NPC)  
**Dificultad:** ⭐⭐☆☆☆ (Fácil - solo configuración)

**¡Listo para empezar!** 🚀


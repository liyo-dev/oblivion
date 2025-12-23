# ⚡ GUÍA RÁPIDA - SISTEMA DE HUIDA TÁCTICA

## 🚀 SETUP EN 3 PASOS (5 MINUTOS)

### **PASO 1: Añadir componente al NPC**
```
1. Seleccionar GameObject del NPC en Hierarchy
2. Inspector → Add Component
3. Buscar: "NPCTacticalRetreat"
4. Click "Add Component"
```

### **PASO 2: Configurar el componente**
```
NPCTacticalRetreat (Script)
├─ Cover Search Radius: 15
├─ Cover Layer Mask: [✓] Default [✓] Environment [✓] Props
├─ Min Cover Distance: 3
├─ Max Cover Distance: 15
├─ Cover Stay Duration: 4
├─ Cover Distance Behind: 1.5
└─ Show Debug Gizmos: ✓
```

### **PASO 3: Activar en NPCCombatConfig**
```
NPCCombatConfig (ScriptableObject)
├─ 🏃 Huida Táctica y Cobertura
│   ├─ Use Tactical Retreat: ✅ true
│   ├─ Retreat Health Threshold: 0.3
│   ├─ Retreat Cooldown: 15
│   ├─ Cover Search Radius: 15
│   ├─ Cover Layer Mask: [✓] Default, Environment, Props
│   ├─ Min Cover Distance: 3
│   ├─ Max Cover Distance: 15
│   ├─ Cover Stay Duration: 4
│   └─ Prefer Shield Over Cover: ☐ false
```

---

## ✅ VERIFICACIÓN RÁPIDA

### **¿Funciona?**
```
□ NPC tiene NPCTacticalRetreat component
□ useTacticalRetreat = true en config
□ Cover Layer Mask incluye Default/Environment/Props
□ Hay objetos con Colliders en la escena
□ NavMesh configurado en el área
```

### **Test rápido:**
```
1. Iniciar combate con NPC
2. Reducir HP a 29%
3. ✅ NPC debería buscar cobertura cercana
4. ✅ Ver Gizmos naranja (radio de búsqueda)
5. ✅ Ver línea azul (path a cobertura)
```

---

## 🎨 GIZMOS EN SCENE VIEW

Cuando seleccionas el NPC:

- 🟠 **Esfera naranja** = Radio de búsqueda (15m)
- 🟢 **Esfera verde** = Cobertura activa (llegó)
- 🟡 **Esfera amarilla** = Navegando hacia cobertura
- 🔵 **Línea azul** = Path desde NPC a cobertura
- 🔷 **Wireframe cyan** = Objeto de cobertura
- 🔴 **Esferas rojas** = Posiciones evaluadas (debug)

---

## 🐛 TROUBLESHOOTING RÁPIDO

### **No busca cobertura:**
```
✓ useTacticalRetreat = true?
✓ HP <= 30%?
✓ Cooldown no activo?
✓ NPCTacticalRetreat presente?
```

### **No encuentra objetos:**
```
✓ Cover Layer Mask correcto?
✓ Objetos tienen Collider?
✓ Cover Search Radius >= 10m?
✓ Objetos dentro de 3-15m?
```

### **Se queda atascado:**
```
✓ Cover Stay Duration > 0?
✓ NavMesh correctamente configurado?
✓ Ver logs en Console
```

---

## 🎯 CONFIGURACIONES RÁPIDAS

### **NPC Cobarde:**
```
Retreat Health Threshold: 0.5  (huye al 50%)
Retreat Cooldown: 10           (huye frecuentemente)
Cover Stay Duration: 6         (se esconde mucho)
```

### **NPC Normal:**
```
Retreat Health Threshold: 0.3  (huye al 30%)
Retreat Cooldown: 15           (equilibrado)
Cover Stay Duration: 4         (tiempo moderado)
```

### **NPC Agresivo:**
```
Retreat Health Threshold: 0.2  (huye al 20%)
Retreat Cooldown: 20           (huye raramente)
Cover Stay Duration: 2         (sale rápido)
Prefer Shield Over Cover: ✓    (prefiere escudo)
```

---

## 📝 LOGS IMPORTANTES

### **Éxito:**
```
[NPCCombatBrain] 🏃 Salud baja (28%), activando huida táctica
[NPCTacticalRetreat] ✅ Cobertura encontrada: Pine_Tree_02 (Score: 74.50)
[NPCTacticalRetreat] ✅ Llegó a cobertura, permanecerá por 4s
```

### **Sin cobertura (fallback a escudo):**
```
[NPCTacticalRetreat] ❌ No se encontró cobertura disponible
[NPCCombatBrain] 🛡️ ESCUDO ACTIVADO - Duración: 3.5s (fallback)
```

### **Cooldown activo:**
```
[NPCCombatBrain] 🔒 Cooldown de huida: 12.3s
```

---

## 🔗 DOCUMENTACIÓN COMPLETA

- **SISTEMA_HUIDA_TACTICA_NPC.md** - Documentación detallada (750+ líneas)
- **RESUMEN_SISTEMA_HUIDA_TACTICA.md** - Resumen ejecutivo
- **SISTEMA_ESCUDO_NPC.md** - Sistema de escudo (complementario)

---

## 📊 QUÉ ESPERAR

### **Comportamiento con huida activa:**
```
HP: 100% → ⚔️ Ataca normalmente
HP: 35%  → ⚔️ Sigue atacando
HP: 30%  → 🚨 UMBRAL ALCANZADO
         → 🏃 Busca árbol cercano
         → 🏃 Corre hacia árbol (2-3s)
         → ✅ Llega detrás del árbol
         → 🛡️ Activa escudo (opcional)
         → ⏰ Espera 4s
         → ✅ Sale de cobertura
         → ⚔️ Vuelve a atacar
         → 🔒 Cooldown 15s
```

---

## ⚡ COMANDOS ÚTILES

### **Desactivar temporalmente:**
```csharp
// En NPCCombatConfig:
useTacticalRetreat = false
```

### **Ajustar prioridades:**
```csharp
// Prioriza escudo sobre cobertura:
preferShieldOverCover = true

// Prioriza cobertura sobre escudo:
preferShieldOverCover = false
```

### **Debug intensivo:**
```csharp
// En NPCTacticalRetreat:
showDebugGizmos = true  // Ver en Scene view
```

---

**¿Dudas?** Consulta `SISTEMA_HUIDA_TACTICA_NPC.md` para documentación completa.

**Estado:** ✅ Sistema completamente funcional y listo para usar.


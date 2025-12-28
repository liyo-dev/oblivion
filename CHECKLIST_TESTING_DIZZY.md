# 🧪 CHECKLIST: Testing Post-Death Dizzy System

**Fecha**: 28/12/2024  
**Feature**: Simplificación de secuencia dizzy después de muerte

---

## ✅ PRE-TESTING: Configuración en Unity

### 1. Configurar Animator Controller

Para cada NPC que use "GetUpDizzy":

- [ ] Abrir **Animator Controller** del NPC
- [ ] Localizar estado **"Die02_NoWeapon"** (o tu estado de muerte)
- [ ] Verificar configuración:
  ```
  ✅ Has Exit Time: TRUE
  ✅ Exit Time: 0.9 o mayor (para que la animación termine casi completa)
  ✅ Transición a estado "Dizzy_NoWeapon"
  ```
- [ ] Localizar estado **"Dizzy_NoWeapon"**
- [ ] (Opcional) Configurar transición a Idle Normal al terminar

### 2. Configurar NPCCombatConfig (ScriptableObject)

- [ ] Abrir NPCCombatConfig del NPC de prueba
- [ ] Configurar:
  ```
  Post Death Behavior: GetUpDizzy
  Dialogue On Dizzy: [Asignar DialogueAsset]
  Dialogue After Defeat: [Asignar DialogueAsset para interacciones futuras]
  ```

### 3. Verificar NPCSimpleAnimator Inspector

- [ ] Seleccionar NPC en Hierarchy
- [ ] En componente **NPCSimpleAnimator**:
  ```
  Die State: "Die02_NoWeapon" (debe coincidir con Animator)
  Dizzy State: "Dizzy_NoWeapon" (debe coincidir con Animator)
  ```

---

## 🎮 TESTING: Secuencia de Pruebas

### Test 1: Flujo Básico

**Objetivo**: Verificar que la secuencia completa funciona

1. [ ] Iniciar Play Mode
2. [ ] Activar Console y filtrar por `[Lifecycle]`
3. [ ] Iniciar combate con el NPC
4. [ ] Derrotar al NPC
5. [ ] **Verificar logs en orden**:
   ```
   [Lifecycle] 💀 Iniciando secuencia de muerte
   [Lifecycle] 😵 Iniciando secuencia GetUpDizzy
   [Lifecycle] 💀 Animación de muerte iniciada - transicionará automáticamente a dizzy
   [Lifecycle] ✅ NPC ahora está en animación dizzy - mostrando diálogo
   [Lifecycle] ✅ Secuencia GetUpDizzy completada
   ```

6. [ ] **Verificar visualmente**:
   - [ ] NPC reproduce animación de muerte completa
   - [ ] NPC transiciona automáticamente a animación dizzy
   - [ ] Diálogo aparece CUANDO está mareado (no antes, no después)
   - [ ] La animación dizzy sigue mientras el diálogo está activo

### Test 2: Timing del Diálogo

**Objetivo**: Verificar que el diálogo aparece en el momento exacto

1. [ ] Repetir combate
2. [ ] Observar **ventana Animator** durante la derrota
3. [ ] **Verificar**:
   - [ ] El círculo azul va de "Die02_NoWeapon" a "Dizzy_NoWeapon"
   - [ ] El diálogo aparece EXACTAMENTE cuando el círculo está en "Dizzy_NoWeapon"
   - [ ] No hay espera antes o después de la transición

### Test 3: Interacción Post-Combate

**Objetivo**: Verificar que el NPC queda interactuable

1. [ ] Derrotar NPC (completar secuencia dizzy)
2. [ ] Cerrar el diálogo
3. [ ] Acercarse al NPC
4. [ ] **Verificar**:
   - [ ] Aparece indicador de interacción (E)
   - [ ] Al interactuar, muestra el diálogo configurado en "Dialogue After Defeat"
   - [ ] El NPC sigue en animación idle/dizzy

### Test 4: Diferentes Exit Times

**Objetivo**: Verificar flexibilidad del sistema

1. [ ] En Animator Controller, cambiar **Exit Time** del estado Die a **0.5**
2. [ ] Repetir combate
3. [ ] **Verificar**:
   - [ ] La muerte termina más rápido
   - [ ] El diálogo sigue apareciendo correctamente en dizzy
   - [ ] No hay desfase ni errores

4. [ ] Cambiar Exit Time a **0.95** (más largo)
5. [ ] Repetir combate
6. [ ] **Verificar**:
   - [ ] La muerte es más lenta y dramática
   - [ ] El diálogo ESPERA a que termine la muerte
   - [ ] Todo sincroniza correctamente

### Test 5: Edge Cases

**Objetivo**: Verificar robustez del sistema

#### 5.1 Sin transición configurada
1. [ ] Eliminar temporalmente la transición Death → Dizzy en Animator
2. [ ] Derrotar NPC
3. [ ] **Verificar logs**:
   ```
   [Lifecycle] ⚠️ Timeout esperando animación dizzy - continuando de todas formas
   ```
4. [ ] **Verificar**:
   - [ ] El sistema no se bloquea
   - [ ] El diálogo aparece después de 10s (timeout)
5. [ ] Restaurar transición

#### 5.2 Sin diálogo configurado
1. [ ] Quitar DialogueAsset de "Dialogue On Dizzy"
2. [ ] Derrotar NPC
3. [ ] **Verificar**:
   - [ ] No hay errores en Console
   - [ ] La secuencia termina normalmente
   - [ ] El NPC queda interactuable

#### 5.3 Nombre de estado incorrecto
1. [ ] Cambiar "Dizzy State" a un nombre inexistente (ej: "WrongName")
2. [ ] Derrotar NPC
3. [ ] **Verificar**:
   - [ ] Aparece timeout warning
   - [ ] El sistema continúa sin crash
4. [ ] Restaurar nombre correcto

---

## 🐛 TROUBLESHOOTING

### ❌ Problema: "Diálogo no aparece"

**Síntomas**: NPC muere, se levanta mareado, pero no hay diálogo

**Solución**:
1. [ ] Verificar Console por error: `⚠️ Timeout esperando animación dizzy`
2. [ ] Si aparece timeout:
   - [ ] Revisar que existe transición Death → Dizzy en Animator
   - [ ] Verificar que "Dizzy State" en Inspector coincida con nombre en Animator
3. [ ] Verificar que DialogueAsset está asignado en NPCCombatConfig

### ❌ Problema: "Diálogo aparece antes de tiempo"

**Síntomas**: El diálogo aparece mientras el NPC está muriendo

**Solución**:
1. [ ] Revisar que el Exit Time del estado Death es suficientemente alto
2. [ ] Verificar que Has Exit Time está en TRUE
3. [ ] Asegurar que el estado Death no tiene Exit Time = 0

### ❌ Problema: "NPC se queda congelado"

**Síntomas**: NPC muere y no hace nada más

**Solución**:
1. [ ] Verificar logs - buscar línea `[Lifecycle] 😵 Iniciando secuencia GetUpDizzy`
2. [ ] Si no aparece:
   - [ ] Verificar que Post Death Behavior = "GetUpDizzy" en config
3. [ ] Si aparece pero luego nada:
   - [ ] Verificar que NPCSimpleAnimator está en el GameObject
   - [ ] Revisar que el Animator Controller está asignado

### ❌ Problema: "NPC no es interactuable después"

**Síntomas**: No aparece indicador (E) después de derrotarlo

**Solución**:
1. [ ] Verificar que el NPC tiene componente **Interactable**
2. [ ] Revisar que el Layer cambió a "Interactable"
3. [ ] Verificar que CapsuleCollider tiene `isTrigger = true`
4. [ ] Asegurar que "Dialogue After Defeat" está asignado en config

---

## 📊 RESULTADOS ESPERADOS

### ✅ Checklist Final

Después de todos los tests, deberías poder confirmar:

- [ ] ✅ Animación de muerte se reproduce completamente
- [ ] ✅ Transición automática a dizzy sin código extra
- [ ] ✅ Diálogo aparece exactamente cuando está mareado
- [ ] ✅ No hay esperas hardcodeadas visibles
- [ ] ✅ El sistema respeta los tiempos del Animator
- [ ] ✅ Es fácil ajustar tiempos cambiando Exit Time
- [ ] ✅ NPC queda interactuable después
- [ ] ✅ Sistema robusto ante errores de configuración
- [ ] ✅ Logs claros para debugging

---

## 📝 NOTAS DE TESTING

**Fecha del test**: ___________  
**Testeador**: ___________  
**Versión**: 1.0

### NPCs Testeados:
- [ ] Boy_Pirate
- [ ] _____________
- [ ] _____________

### Issues Encontrados:
```
(Anotar aquí cualquier problema encontrado)




```

### Observaciones:
```
(Feedback general sobre el comportamiento)




```

---

## 🎯 CRITERIOS DE APROBACIÓN

El sistema pasa si:

1. ✅ **Sincronización perfecta**: Diálogo aparece cuando NPC está mareado
2. ✅ **Sin bloqueos**: Sistema nunca se congela
3. ✅ **Configurabilidad**: Cambiar Exit Time afecta el timing correctamente
4. ✅ **Robustez**: No crashea con configuraciones incorrectas
5. ✅ **UX**: Flujo se siente natural y sin esperas artificiales

---

**Estado**: 🟡 PENDIENTE DE TESTING

**Una vez completado**: Marcar como ✅ APROBADO y actualizar RESUMEN_FIX_DIZZY.md


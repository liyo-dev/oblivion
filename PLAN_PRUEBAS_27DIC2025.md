# 🧪 PLAN DE PRUEBAS - Cambios 27 Diciembre 2025

## 📋 Resumen de Cambios Implementados

### 1. ✅ Player Battle Mode Controller
- Corrección de animación de locomoción en batalla
- Integración con sistema de audio centralizado

### 2. 🚨 NPC Combat Brain - Correcciones CRÍTICAS
- Movimiento en línea recta (no diagonal)
- Animación de Battle Idle sin pausas (CRÍTICO)
- Comportamiento de huida natural

### 3. 💀 Animación de Muerte NPC
- Protección contra interrupciones

### 4. 🎭 Sistema Post-Muerte NPC (NUEVO)
- Opción: Desaparecer con VFX
- Opción: Levantarse mareado con diálogo

---

## 🎯 PRUEBAS PRIORITARIAS (CRÍTICAS)

### ⚠️ PRUEBA CRÍTICA 1: Animación Battle Idle NPC (MÁXIMA PRIORIDAD)

**Objetivo:** Verificar que la animación de Battle Idle NO se pausa/congela

**Setup:**
1. Activar `debugMode` en `NPCSimpleAnimator` (Inspector)
2. Iniciar combate con cualquier NPC

**Pasos de Prueba:**
1. ✅ Observar al NPC **quieto** en Battle Idle durante 10-15 segundos
2. ✅ Verificar que la animación se reproduce **constantemente** sin pausas
3. ✅ Verificar que el Enemy Marker está **totalmente estable** (no se mueve)
4. ✅ Verificar que el modelo del NPC **no tiembla ni se sacude**

**Resultado Esperado:**
- ✅ Animación fluida y continua a velocidad 1.0x
- ✅ CERO temblor en el modelo
- ✅ Enemy Marker perfectamente estático
- ✅ Sensación de alta calidad visual

**Logs a Verificar:**
```
[NPCAnimator] SetMovementSpeed(0.00, dampTime: 0.08)
// animator.speed debería ser SIEMPRE 1.0 cuando está quieto
```

**❌ Indicadores de FALLO (reportar inmediatamente):**
- Animación entrecortada o con micro-pausas
- Temblor visible en el modelo
- Enemy Marker moviéndose erráticamente
- Sensación de inestabilidad

**Tiempo Estimado:** 2 minutos por NPC  
**NPCs a Probar:** Al menos 3 diferentes

---

### ⚠️ PRUEBA CRÍTICA 2: Comportamiento de Huida NPC

**Objetivo:** Verificar que el NPC se gira y corre al huir (no camina hacia atrás)

**Setup:**
1. Iniciar combate con NPC
2. Acercarse mucho al NPC (< 3 metros)

**Pasos de Prueba:**
1. ✅ El NPC detecta que estás muy cerca
2. ✅ El NPC **se gira completamente** hacia la dirección opuesta
3. ✅ El NPC **corre (no camina)** alejándose de ti
4. ✅ La velocidad es **visiblemente rápida** (más que su movimiento normal)
5. ✅ El NPC mira **hacia donde corre**, no hacia ti
6. ✅ El NPC gana suficiente distancia (5-8 metros)

**Resultado Esperado:**
- ✅ Rotación rápida de 180° (o hacia punto de escape)
- ✅ Animación de correr activa
- ✅ Velocidad aumentada (20% más rápido que normal)
- ✅ Movimiento en línea recta (no diagonal)
- ✅ Comportamiento natural y creíble

**Logs a Verificar:**
```
[NPCCombatBrain] 🏃💨 HUYENDO - Jugador muy cerca (2.3m)
```

**Tiempo Estimado:** 3 minutos por NPC  
**NPCs a Probar:** Al menos 2 diferentes

---

## 🎮 PRUEBAS DE FUNCIONALIDAD (IMPORTANTES)

### PRUEBA 3: Movimiento del Jugador en Batalla

**Objetivo:** Verificar que el jugador puede moverse correctamente en modo batalla

**Setup:**
1. Activar `debugMode` en `PlayerBattleModeController` (Inspector)
2. Iniciar combate con NPC

**Pasos de Prueba:**

**3.1 - Jugador Quieto en Batalla:**
1. ✅ Quedarse quieto sin mover joystick
2. ✅ Verificar animación `Idle_Battle` se reproduce
3. ✅ Esperar 5 segundos sin moverse
4. ✅ Animación debe seguir fluida

**3.2 - Jugador Empieza a Moverse:**
1. ✅ Mover joystick en cualquier dirección
2. ✅ **INMEDIATAMENTE** debe transicionar a caminar/correr
3. ✅ NO debe quedarse atascado en `Idle_Battle`
4. ✅ Movimiento fluido en todas direcciones

**3.3 - Jugador Se Detiene:**
1. ✅ Soltar joystick
2. ✅ Después de ~0.3s debe volver a `Idle_Battle`
3. ✅ Transición suave

**Resultado Esperado:**
- ✅ Cambio instantáneo a locomoción al mover joystick
- ✅ Sin trabas ni delay perceptible
- ✅ Vuelta suave a Battle Idle al detenerse

**Logs a Verificar:**
```
[PlayerBattleMode] 🏃 Jugador moviéndose - velocidad: 2.45
[PlayerBattleMode] ⏸️ Jugador detuvo movimiento
[PlayerBattleMode] ✅ Cambiado a Battle Idle desde Idle normal
```

**Tiempo Estimado:** 5 minutos

---

### PRUEBA 4: Movimiento del NPC en Combate

**Objetivo:** Verificar que el NPC se mueve en línea recta (no diagonal)

**Setup:**
1. Iniciar combate con NPC
2. Observar el movimiento del NPC

**Pasos de Prueba:**

**4.1 - NPC Retrocede (Jugador Muy Cerca):**
1. ✅ Acercarse mucho al NPC (< 3m)
2. ✅ Observar que el NPC retrocede
3. ✅ **Verificar:** Se mueve en línea recta alejándose
4. ✅ **Verificar:** NO camina en diagonal
5. ✅ **Verificar:** Se gira hacia donde corre (huye)

**4.2 - NPC Se Acerca (Jugador Muy Lejos):**
1. ✅ Alejarse mucho del NPC (> 12m)
2. ✅ Observar que el NPC se acerca
3. ✅ **Verificar:** Camina en línea recta hacia ti
4. ✅ **Verificar:** NO camina en diagonal

**4.3 - NPC Ataca (Distancia Correcta):**
1. ✅ Estar a distancia media (4-8m)
2. ✅ NPC se detiene y mira hacia ti
3. ✅ NPC dispara hechizo
4. ✅ **Verificar:** Está mirando directamente hacia ti al disparar

**Resultado Esperado:**
- ✅ Movimiento siempre en línea recta
- ✅ NUNCA movimiento diagonal
- ✅ Rotación correcta hacia dirección de movimiento
- ✅ Al atacar: rotación hacia el jugador

**Tiempo Estimado:** 5 minutos por NPC  
**NPCs a Probar:** Al menos 2 diferentes

---

### PRUEBA 5: Animación de Muerte NPC

**Objetivo:** Verificar que la animación `Die02_NoWeapon` se reproduce completamente

**Setup:**
1. Iniciar combate con NPC

**Pasos de Prueba:**
1. ✅ Reducir vida del NPC a 0
2. ✅ **Verificar:** Animación `Die02_NoWeapon` empieza **inmediatamente**
3. ✅ **Verificar:** Animación se reproduce durante ~3 segundos **completos**
4. ✅ **Verificar:** NO es interrumpida (no vuelve a Idle)
5. ✅ **Verificar:** NPC permanece en el suelo
6. ✅ Esperar efectos de muerte (slowmo, shake)
7. ✅ Esperar música de victoria
8. ✅ **Verificar:** Jugador hace animación de victoria después

**Resultado Esperado:**
- ✅ Animación de muerte completa sin interrupciones
- ✅ Efectos visuales dramáticos (slowmo, shake)
- ✅ Secuencia fluida: muerte NPC → victoria jugador
- ✅ NPC permanece "muerto" en el suelo

**Logs a Verificar:**
```
[NPCAnimator] 💀 PlayDeath() llamado - dieState: 'Die02_NoWeapon'
[NPCAnimator] 🎬 Reproduciendo animación de muerte: Die02_NoWeapon
[NPCAnimator] SetBattleMode(false) ignorado - NPC está muerto
[NPCCombatLifecycleHandler] ✅ Animación de muerte del NPC completada
```

**Tiempo Estimado:** 10 segundos por intento  
**NPCs a Probar:** Al menos 3 diferentes

---

### PRUEBA 6: Audio de Victoria del Jugador

**Objetivo:** Verificar integración con sistema de audio centralizado

**Setup:**
1. Configurar `AudioGraphProfile` (ScriptableObject)
2. Agregar entrada en "Event SFX":
   - Event Key: `Player_Victory`
   - SFX: Tu AudioClip de fanfarria

**Pasos de Prueba:**
1. ✅ Ganar una batalla
2. ✅ Observar animación de victoria del jugador
3. ✅ **Verificar:** Se escucha el SFX de victoria
4. ✅ **Verificar:** Audio sincronizado con animación

**Resultado Esperado:**
- ✅ SFX de victoria se reproduce automáticamente
- ✅ Audio viene del sistema centralizado (no AudioSource local)
- ✅ Volumen apropiado

**Nota:** Si no hay SFX configurado, debe seguir funcionando sin audio

**Tiempo Estimado:** 2 minutos

---

## 🎭 PRUEBAS DE NUEVA FUNCIONALIDAD (POST-MUERTE)

### PRUEBA 7: NPC Desaparece con VFX

**Objetivo:** Verificar opción "Desaparecer" post-muerte

**Setup:**
1. Crear o abrir `NPCCombatConfig` (ScriptableObject)
2. Configurar:
   - `Post Death Behavior` = **Desaparecer**
   - `Disappear VFX Prefab` = Tu efecto de partículas
   - `Disappear Duration` = 2.0
   - `Dialogue On Defeat` = (Opcional) Frase final

**Pasos de Prueba:**
1. ✅ Derrotar al NPC
2. ✅ Observar animación de muerte (3s)
3. ✅ Observar victoria del jugador (3s)
4. ✅ **Verificar:** Diálogo de derrota se muestra (si existe)
5. ✅ **Verificar:** VFX de desaparición se reproduce
6. ✅ **Verificar:** NPC desaparece gradualmente
7. ✅ **Verificar:** GameObject queda inactivo
8. ✅ Intentar encontrar al NPC → No debe estar visible

**Resultado Esperado:**
- ✅ Secuencia fluida de muerte → victoria → desaparición
- ✅ VFX apropiado se reproduce en la posición del NPC
- ✅ NPC desaparece completamente
- ✅ No se puede volver a interactuar

**Logs a Verificar:**
```
[NPCCombatLifecycleHandler] 👻 Iniciando secuencia de desaparición
[NPCCombatLifecycleHandler] ✨ VFX de desaparición reproducido
[NPCCombatLifecycleHandler] 👻 NPC desaparecido - GameObject desactivado
```

**Tiempo Estimado:** 15 segundos por NPC  
**NPCs a Probar:** Al menos 2 diferentes

---

### PRUEBA 8: NPC Se Levanta Mareado (NUEVA FUNCIONALIDAD)

**Objetivo:** Verificar opción "Levantarse Mareado" post-muerte

**Setup:**
1. Crear o abrir `NPCCombatConfig` (ScriptableObject)
2. Configurar:
   - `Post Death Behavior` = **Levantarse Mareado**
   - `Dialogue On Dizzy` = ⚠️ **REQUERIDO** - "Uff... necesito descansar"
   - `Dialogue After Defeat` = "¡La próxima vez te ganaré!"
3. En `NPCSimpleAnimator` (Componente):
   - Verificar `Dizzy State` = `"Dizzy_NoWeapon"`

**Pasos de Prueba:**

**8.1 - Secuencia de Derrota:**
1. ✅ Derrotar al NPC
2. ✅ Observar animación de muerte (3s)
3. ✅ Observar victoria del jugador (3s)
4. ✅ **Verificar:** NPC permanece en el suelo 1 segundo adicional

**8.2 - Levantarse Mareado:**
5. ✅ **Verificar:** Animación `Dizzy_NoWeapon` **empieza**
6. ✅ **Verificar:** NPC se ve aturdido/mareado
7. ✅ **Verificar:** Animación es fluida (no pausada)
8. ✅ **Verificar:** Diálogo de mareo se muestra
9. ✅ Leer el diálogo completo

**8.3 - Interacción Post-Derrota:**
10. ✅ Acercarse al NPC mareado
11. ✅ **Verificar:** Aparece opción de interactuar (E)
12. ✅ Interactuar con el NPC
13. ✅ **Verificar:** Diálogo post-derrota se muestra
14. ✅ **Verificar:** Puede hablar con él múltiples veces

**Resultado Esperado:**
- ✅ Secuencia fluida: muerte → victoria → levantarse mareado
- ✅ Animación Dizzy se reproduce correctamente
- ✅ Diálogo de mareo apropiado
- ✅ NPC queda interactivo (puede hablar con él)
- ✅ Diálogo repetible funciona

**Logs a Verificar:**
```
[NPCCombatLifecycleHandler] 😵 Iniciando secuencia de mareo
[NPCAnimator] 😵 PlayDizzy() llamado - dizzyState: 'Dizzy_NoWeapon'
[NPCAnimator] 🎬 Reproduciendo animación de mareo: Dizzy_NoWeapon
[NPCCombatLifecycleHandler] 💬 Iniciando diálogo de mareo
[NPCCombatLifecycleHandler] ✅ Diálogo de mareo completado
[NPCCombatLifecycleHandler] 😵 NPC en estado mareado - Puede interactuar
```

**Errores Comunes a Verificar:**
- ❌ Si no aparece diálogo de mareo → Verificar `dialogueOnDizzy` asignado
- ❌ Si no reproduce animación → Verificar `dizzyState` en NPCSimpleAnimator
- ❌ Si no puede interactuar después → Verificar layer Interactable

**Tiempo Estimado:** 30 segundos por NPC  
**NPCs a Probar:** Al menos 2 diferentes (entrenador, rival)

---

## 🔄 PRUEBAS DE REGRESIÓN

### PRUEBA 9: Combate Básico (Sin Cambios)

**Objetivo:** Verificar que el combate básico no se rompió

**Pasos de Prueba:**
1. ✅ Iniciar combate con NPC
2. ✅ Verificar que el NPC ataca normalmente
3. ✅ Verificar que puedes esquivar
4. ✅ Verificar que puedes atacar
5. ✅ Verificar que los hechizos funcionan
6. ✅ Verificar que la vida baja correctamente
7. ✅ Verificar que puedes ganar/perder

**Tiempo Estimado:** 5 minutos

---

### PRUEBA 10: NPCs Existentes (Compatibilidad)

**Objetivo:** Verificar que los NPCs antiguos no se rompieron

**Pasos de Prueba:**
1. ✅ Probar con NPCs que NO tienen configuración nueva
2. ✅ Verificar que funcionan exactamente igual que antes
3. ✅ Verificar combate normal
4. ✅ Verificar muerte normal
5. ✅ Verificar diálogos existentes

**Resultado Esperado:**
- ✅ NPCs antiguos funcionan sin cambios
- ✅ Comportamiento legacy preservado
- ✅ No hay bugs nuevos

**Tiempo Estimado:** 10 minutos

---

## 📊 MATRIZ DE PRUEBAS

### Prioridad CRÍTICA (Hacer PRIMERO)

| # | Prueba | Tiempo | Prioridad | Estado |
|---|--------|--------|-----------|---------|
| 1 | Battle Idle NPC (No Pausada) | 6 min | 🔴 CRÍTICA | ⬜ |
| 2 | Comportamiento de Huida | 6 min | 🔴 CRÍTICA | ⬜ |

### Prioridad ALTA

| # | Prueba | Tiempo | Prioridad | Estado |
|---|--------|--------|-----------|---------|
| 3 | Movimiento Jugador en Batalla | 5 min | 🟠 ALTA | ⬜ |
| 4 | Movimiento NPC en Combate | 10 min | 🟠 ALTA | ⬜ |
| 5 | Animación de Muerte NPC | 5 min | 🟠 ALTA | ⬜ |

### Prioridad MEDIA

| # | Prueba | Tiempo | Prioridad | Estado |
|---|--------|--------|-----------|---------|
| 6 | Audio de Victoria | 2 min | 🟡 MEDIA | ⬜ |
| 7 | NPC Desaparece con VFX | 5 min | 🟡 MEDIA | ⬜ |
| 8 | NPC Se Levanta Mareado | 10 min | 🟡 MEDIA | ⬜ |

### Prioridad BAJA (Regresión)

| # | Prueba | Tiempo | Prioridad | Estado |
|---|--------|--------|-----------|---------|
| 9 | Combate Básico | 5 min | 🟢 BAJA | ⬜ |
| 10 | NPCs Existentes | 10 min | 🟢 BAJA | ⬜ |

**TIEMPO TOTAL ESTIMADO:** ~60 minutos

---

## 🎯 PLAN DE EJECUCIÓN RECOMENDADO

### Fase 1: Pruebas CRÍTICAS (15 min)
```
1. Battle Idle NPC sin pausas ⚠️ MÁXIMA PRIORIDAD
2. Comportamiento de huida natural
```

### Fase 2: Funcionalidad Core (20 min)
```
3. Movimiento jugador en batalla
4. Movimiento NPC en combate
5. Animación de muerte NPC
```

### Fase 3: Nuevas Funcionalidades (20 min)
```
6. Audio de victoria
7. NPC desaparece con VFX
8. NPC se levanta mareado ⭐ NUEVA
```

### Fase 4: Regresión (15 min)
```
9. Combate básico sin cambios
10. NPCs existentes compatibles
```

---

## 📝 CHECKLIST RÁPIDO

### Antes de Empezar
- [ ] Compilación sin errores ✅
- [ ] Escena de prueba lista
- [ ] Al menos 3 NPCs diferentes disponibles
- [ ] AudioGraphProfile configurado
- [ ] Debug Mode activado donde corresponda

### Después de Cada Prueba
- [ ] Marcar estado en matriz (✅ / ❌)
- [ ] Anotar bugs encontrados
- [ ] Screenshots de problemas
- [ ] Logs relevantes guardados

### Al Finalizar
- [ ] Todas las pruebas CRÍTICAS pasadas
- [ ] Bug report si hay fallos
- [ ] Desactivar Debug Mode
- [ ] Commit de cambios si todo funciona

---

## 🐛 REPORTE DE BUGS

Si encuentras problemas, reportar con:

```
BUG #[número]
--------------
Prueba: [Nombre de la prueba]
Prioridad: [CRÍTICA / ALTA / MEDIA / BAJA]
Descripción: [Qué pasó]
Pasos para Reproducir: [1, 2, 3...]
Resultado Esperado: [Qué debería pasar]
Resultado Actual: [Qué pasó en realidad]
Logs: [Copiar logs relevantes]
Screenshots: [Si aplica]
```

---

## 📞 CONTACTO

**Si alguna prueba CRÍTICA falla:**
- 🔴 Reportar INMEDIATAMENTE
- 🔴 NO continuar con otras pruebas
- 🔴 Proporcionar logs completos

**Pruebas CRÍTICAS que NO pueden fallar:**
1. Battle Idle NPC (sin temblor)
2. Comportamiento de huida

---

**Fecha:** 27 de diciembre de 2025  
**Versión de Pruebas:** 1.0  
**Estado:** ✅ LISTO PARA EJECUTAR


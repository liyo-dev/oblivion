# Game Design Document (GDD) - El Sendero de las Estrellas

## 1. Visión General
El juego es una aventura narrativa centrada en completar misiones encadenadas, con énfasis en diálogos localizados y progresión ligera del jugador. Actualmente el foco está en la cadena de misiones de Eldran, con todos los textos (es/en) y lógica de misiones listos a nivel de código y localización.

## 2. Estado Actual del Proyecto
- **Localización completa**: 32 líneas de diálogo y quests traducidas al español e inglés listas en los JSON de localización.【F:docs/RESUMEN_COMPLETO.md†L5-L24】
- **Sistema de misiones funcional**: El script `SimpleQuestNPC` soporta cadenas de quests, 3 modos de completado, métricas de progreso y helpers de depuración.【F:docs/RESUMEN_COMPLETO.md†L61-L70】
- **Cadena Eldran documentada**: Diseño, textos y configuraciones listos; solo falta crear los assets en Unity según las guías.【F:docs/RESUMEN_COMPLETO.md†L73-L129】【F:docs/RESUMEN_COMPLETO.md†L296-L307】
- **Arquitectura de juego definida**: Servicios centrales (`GameBootService`, `GameBootProfile`), preset de jugador y sistemas de localización, salud, maná, spawn, UI y guardado descritos y configurables desde inspector.【F:docs/SISTEMA_JUEGO.md†L19-L185】

## 3. Gameplay Core
- **Loop**: Explorar, hablar con NPCs, aceptar misiones, recoger objetos, entregar y desbloquear diálogos posteriores.
- **Progresión inicial**: Dos misiones de Eldran.
  - *Misión 1*: Hablar con Eldran; se completa automáticamente al conversar y ofrece la misión 2.【F:docs/RESUMEN_COMPLETO.md†L75-L99】
  - *Misión 2*: Buscar la caja de frutas; requiere pickup en el bosque y completar al hablar si se cumplieron los pasos.【F:docs/RESUMEN_COMPLETO.md†L102-L129】
- **Diálogos localizados**: Todos los textos listos para UI/subtítulos, organizados en IDs de localización (personajes, diálogos, quests).【F:docs/RESUMEN_COMPLETO.md†L228-L257】

## 4. Sistemas y Arquitectura Técnica
### 4.1 GameBoot y Presets
- Servicio `GameBootService` en la escena de arranque carga el `GameBootProfile` y notifica a los sistemas dependientes cuando el perfil está listo.【F:docs/SISTEMA_JUEGO.md†L19-L37】
- `GameBootProfile` define escena inicial, anchor por defecto y presets de jugador; soporta modo “usar preset” o cargar desde guardado, creando un `runtimePreset` seguro como fallback.【F:docs/SISTEMA_JUEGO.md†L143-L185】
- Patrón de uso recomendado: coroutine que espera `GameBootService.IsReady()` antes de acceder al preset activo.【F:docs/SISTEMA_JUEGO.md†L186-L200】

### 4.2 Localización
- `LocalizationManager` carga JSON desde `Resources/Localization/`, con fallback al idioma por defecto y cambio dinámico in-game.【F:docs/SISTEMA_JUEGO.md†L40-L55】
- Enumerados `UITextId` y `DialogueId` cubren textos UI y diálogos; componentes `LocalizedUI` y `LocalizedMessage` permiten vincular IDs desde inspector.【F:docs/SISTEMA_JUEGO.md†L56-L91】
- `SubtitleController` muestra líneas por ID y es compatible con Timeline/Animation Events, actualizándose al cambiar el idioma.【F:docs/SISTEMA_JUEGO.md†L100-L113】

### 4.3 Sistema de Misiones
- `SimpleQuestNPC` soporta cadenas con múltiples quests, tres modos de completado (manual, auto al hablar, completar si pasos listos) y métricas de progreso/estado por quest.【F:docs/RESUMEN_COMPLETO.md†L61-L70】
- Configuración de Eldran:
  - Misión 1 usa `Completion Mode: AutoCompleteOnTalk`; diálogos de turn-in/completed y oferta de la misión 2 asignados a IDs específicos.【F:docs/RESUMEN_COMPLETO.md†L86-L92】
  - Misión 2 usa `Completion Mode: CompleteOnTalkIfStepsReady`; incluye diálogos para in-progress, turn-in y completado.【F:docs/RESUMEN_COMPLETO.md†L115-L121】
- La segunda misión requiere condición `GET_FRUIT_CRATE` en el paso 1, conectada a un objeto pickup en el bosque.【F:docs/RESUMEN_COMPLETO.md†L104-L129】【F:docs/RESUMEN_COMPLETO.md†L220-L224】

### 4.4 Player Systems
- **Salud y Maná**: Gestionados por `PlayerHealthSystem` y `ManaPool` contenidos en el `PlayerPresetSO` cargado por el GameBootProfile.【F:docs/SISTEMA_JUEGO.md†L23-L35】
- **Spawn/Anchors**: `SpawnManager` controla posiciones iniciales mediante IDs de anchor definidos en el perfil (`defaultAnchorId`, `startAnchorId`).【F:docs/SISTEMA_JUEGO.md†L23-L35】【F:docs/SISTEMA_JUEGO.md†L148-L185】
- **UI/Feedback**: Sistema de UI enlazado a localización; feedback incluye subtítulos y mensajes localizados.【F:docs/SISTEMA_JUEGO.md†L40-L113】
- **Save/Load**: `GameBootProfile` puede guardar/cargar estado del jugador (nivel, HP/MP, habilidades, hechizos, flags y anchor) en el `runtimePreset`, con asignación inicial de hechizos a slots al cargar.【F:docs/SISTEMA_JUEGO.md†L168-L185】

## 5. Configuración en Unity (pendiente de asseteo)
1. **QuestData Assets**: Crear `Q_ELDRAN_MISSION1.asset` (1 paso) y `Q_ELDRAN_MISSION2.asset` (2 pasos).【F:docs/RESUMEN_COMPLETO.md†L201-L206】
2. **DialogueAssets**: Crear 6 assets y poblarlos con las líneas localizadas usando los IDs mapeados (turn-in, completed, offer, in-progress).【F:docs/RESUMEN_COMPLETO.md†L207-L213】【F:docs/RESUMEN_COMPLETO.md†L228-L257】
3. **NPC Eldran**: En el inspector, añadir `SimpleQuestNPC`, cargar la chain con las dos quests, asignar los diálogos y seleccionar el modo de completado indicado para cada una.【F:docs/RESUMEN_COMPLETO.md†L86-L129】【F:docs/RESUMEN_COMPLETO.md†L215-L218】
4. **Pickup de la caja**: En el bosque, crear un GameObject con `SimpleQuestPickup`, `Quest Id: ELDRAN_MISSION2`, `Step Index: 1` y el ID de condición `GET_FRUIT_CRATE`.【F:docs/RESUMEN_COMPLETO.md†L112-L113】【F:docs/RESUMEN_COMPLETO.md†L220-L224】
5. **GameBoot**: En la escena de inicio, añadir `GameBootService` y asignar `GameBootProfile` con escena objetivo y anchors; definir si se usa preset o carga desde save según necesidades de testing.【F:docs/SISTEMA_JUEGO.md†L116-L185】

## 6. Backlog Técnico
- Integrar los assets faltantes (quests, diálogos, pickup) siguiendo las guías para cerrar el flujo Eldran.【F:docs/RESUMEN_COMPLETO.md†L201-L225】【F:docs/RESUMEN_COMPLETO.md†L296-L307】
- Verificar en escena principal el hook de `SubtitleController` y `LocalizationManager` para asegurar conmutación de idioma en runtime.【F:docs/SISTEMA_JUEGO.md†L40-L113】
- Probar el guardado/carga con `GameBootProfile.SaveCurrentGameState` y `LoadProfile` una vez que haya progreso jugable.【F:docs/SISTEMA_JUEGO.md†L168-L185】
- Ajustar anclajes y spawn si se añaden nuevas zonas; reutilizar `defaultAnchorId` y `startAnchorId` para consistencia de respawn.【F:docs/SISTEMA_JUEGO.md†L148-L185】

## 7. Riesgos y Mitigaciones
- **Falta de assets en Unity**: Sin los ScriptableObjects y pickups, el flujo Eldran no se puede probar. → Mitigar priorizando la creación de assets según sección 5.【F:docs/RESUMEN_COMPLETO.md†L201-L225】
- **Desfase de localización**: Cambios en diálogos sin actualizar JSON generarán IDs faltantes. → Mitigar usando los mapas de IDs existentes y mantener sincronía en ambos idiomas.【F:docs/RESUMEN_COMPLETO.md†L228-L257】
- **Inicialización temprana**: Scripts que accedan al perfil antes de `GameBootService.IsReady()` pueden fallar. → Mitigar siguiendo el patrón de coroutine de inicialización diferida.【F:docs/SISTEMA_JUEGO.md†L186-L200】

## 8. Próximos Pasos Inmediatos
1. Crear y poblar los 2 QuestData y 6 DialogueAssets en Unity, conectando los IDs indicados.
2. Configurar el NPC Eldran con `SimpleQuestNPC` y el objeto de pickup de la caja.
3. Correr una pasada completa del flujo de misiones y registrar bugs/ajustes.

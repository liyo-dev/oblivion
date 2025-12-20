# 🌟 El Sendero de las Estrellas

**Género:** RPG de acción/aventura  
**Motor:** Unity 2020.3+  
**Estado:** En desarrollo activo

---

## 📘 Documentación

### Documento Principal
**[📖 DOCUMENTACION_TECNICA.md](DOCUMENTACION_TECNICA.md)** - **Documento técnico unificado completo**

Este es el único documento que necesitas. Contiene:
- ✅ Arquitectura de escenas
- ✅ Sistema de NPCs (FSM)
- ✅ Sistemas Core (Quest, Dialogue, Input, UI)
- ✅ Localización y guardado
- ✅ Solución de problemas
- ✅ Mejores prácticas
- ✅ Roadmap

### Documentos Específicos
📁 **[docs/](docs/)** - Documentación de referencia específica (localización, sistemas auxiliares)

---

## 🚀 Quick Start

### Para Iniciar el Proyecto:

1. **Abre Unity 2020.3 o superior**
2. **Inicia desde:** `Assets/Scenes/Systems/MainMenu.unity`
3. **NO iniciar desde:** MainWorld directamente (se carga Start automáticamente si lo haces)

### Flujo de Escenas:
```
MainMenu → Start (carga aditiva) → MainWorld (carga aditiva)
         ↓
    Managers persistentes (DontDestroyOnLoad)
```

---

## 🏗️ Arquitectura Clave

### Escena START = Núcleo del Proyecto
La escena `Start.unity` contiene todos los managers persistentes:
- QuestManager
- DialogueManager  
- PlayerInputManager
- LocalizationManager
- SaveSystem
- UIManager

**⚠️ Importante:** Start SIEMPRE debe estar cargada. El sistema `EnsureStartSceneLoaded` la carga automáticamente si falta.

---

## 🎮 Sistemas Principales

### NPCs (NPCBehaviourManagerV2)
- **FSM modular:** IdleState, WanderState, CombatState, CinematicState
- **Configuración:** NPCConfiguration ScriptableObject
- **Módulos:** Wander, Combat, Narrative, Patrol

### Quests
- **Central:** QuestManager con eventos C#
- **NPCs:** SimpleQuestNPC con 3 modos de completado
- **Localizado:** Todo texto en JSON (ES/EN)

### Combate
- **Integrado:** CombatState + NPCCombatBrain
- **Fluido:** Rotación y movimiento suavizado
- **Configurable:** NPCCombatConfig ScriptableObject

### Localización
- **Idiomas:** Español + Inglés
- **Archivos:** `StreamingAssets/Localization/*.json`
- **IDs:** Todo texto visible usa IDs de localización

---

## 📂 Estructura del Proyecto

```
Assets/
├── Scenes/
│   ├── Systems/           ← Start, MainMenu, LoadingScreen
│   └── Main World/        ← MainWorld, Town, Woods, Cave, etc.
├── Scripts/
│   ├── Core/              ← Managers, Services
│   ├── Behaviour NPC/     ← FSM, States, NPCBehaviourManagerV2
│   ├── UI/                ← Menús, HUD
│   └── Quests/            ← QuestManager, QuestData
├── Data/
│   ├── NPCs/
│   │   ├── Configs/       ← NPCConfiguration assets
│   │   └── Dialogues/     ← DialogueAsset assets
│   └── Quests/            ← QuestData assets
└── StreamingAssets/
    └── Localization/      ← JSON de localización (ES/EN)
```

---

## 🛠️ Setup de Desarrollo

### Requisitos
- Unity 2020.3 o superior
- Visual Studio 2019+ o Rider
- Git (recomendado)

### Configuración Inicial
1. Clonar repositorio
2. Abrir proyecto en Unity
3. Verificar que Start scene tiene todos los managers
4. Play desde MainMenu scene

### Testing Rápido
Para testear una escena específica:
1. Añadir `EnsureStartSceneLoaded` component al GameObject raíz
2. Play directamente desde esa escena
3. Start se cargará automáticamente

---

## 🐛 Solución de Problemas Rápida

### "PlayerEquipmentMenu no se abre"
→ Solución: Asegúrate de que Start está cargada (añade `EnsureStartSceneLoaded`)

### "NPC no se mueve suavemente"
→ Solución: Verifica NavMeshAgent (acceleration: 8, angularSpeed: 180)

### "Quest no se completa"
→ Solución: Verifica `completionMode` y que `dlgTurnIn` esté asignado

### "Errores con NPCBehaviourManager"
→ Solución: Usa `NPCBehaviourManagerV2` (la v1 está obsoleta)

**Más soluciones:** Consulta [DOCUMENTACION_TECNICA.md](DOCUMENTACION_TECNICA.md) → Sección 9

---

## 📊 Estado del Proyecto

### ✅ Completado
- Sistema de NPCs refactorizado (FSM)
- Sistema de combate mejorado y fluido
- Sistema de quests funcional
- Localización ES/EN
- Arquitectura de escenas con START
- Sistema de guardado básico

### 🚧 En Desarrollo
- Expansión de inventario
- Más estados FSM (DeathState, FleeState)
- Quest tracker UI
- Cinemáticas con Timeline

### 🔮 Planeado
- Sistema de crafting
- Combat phases por salud
- Editor tools para NPCs
- Sistema de diálogos con branching

---

## 📝 Contribuir

1. Lee **[DOCUMENTACION_TECNICA.md](DOCUMENTACION_TECNICA.md)** completo
2. Sigue las **Mejores Prácticas** (Sección 10)
3. Usa **ServiceLocator** para referencias globales
4. **Localiza** todos los textos visibles
5. **Documenta** cambios mayores en DOCUMENTACION_TECNICA.md

---

## 📞 Recursos

- **Documentación Técnica:** [DOCUMENTACION_TECNICA.md](DOCUMENTACION_TECNICA.md)
- **Documentos Específicos:** [docs/](docs/)
- **Unity Documentation:** https://docs.unity3d.com/

---

## 📜 Licencia

[Especificar licencia aquí]

---

## 🎉 Créditos

**Proyecto:** El Sendero de las Estrellas  
**Desarrollado con:** Unity 2020.3+  
**Última actualización:** Diciembre 2025

---

*¿Perdido? Empieza leyendo [DOCUMENTACION_TECNICA.md](DOCUMENTACION_TECNICA.md)*


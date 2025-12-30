# 🌟 El Sendero de las Estrellas

**Género:** RPG de acción/aventura  
**Motor:** Unity 2020.3+  
**Estado:** En desarrollo activo

---

## 📘 Documentación

**[📖 docs/DOCUMENTACION_TECNICA.md](docs/DOCUMENTACION_TECNICA.md)** - **Documento técnico unificado completo**

Contiene toda la documentación del proyecto:
- ✅ Arquitectura de escenas (START como núcleo)
- ✅ Sistema de NPCs (FSM con NPCBehaviourManagerV2)
- ✅ Sistema de combate
- ✅ Sistema de quests
- ✅ Sistemas Core (Input, Dialogue, Localización)
- ✅ Sistema de guardado
- ✅ Sistema de narrativa
- ✅ Debugging (F3, F4)
- ✅ Solución de problemas
- ✅ Mejores prácticas
- ✅ Roadmap

---

## 🚀 Quick Start

### Para Iniciar el Proyecto:

1. **Abre Unity 2020.3 o superior**
2. **Inicia desde:** `Assets/Scenes/Systems/MainMenu.unity`
3. El sistema carga Start automáticamente (managers persistentes)

### Testing Rápido

Para testear una escena específica:
1. Añadir `EnsureStartSceneLoaded` component al GameObject raíz
2. Play directamente desde esa escena
3. Start se cargará automáticamente

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

**⚠️ Importante:** Start SIEMPRE debe estar cargada.

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
│   ├── NPCs/              ← Configs y Dialogues
│   └── Quests/            ← QuestData assets
├── docs/                  ← DOCUMENTACION_TECNICA.md
└── StreamingAssets/
    └── Localization/      ← JSON de localización (ES/EN)
```

---

## 🐛 Solución de Problemas Rápida

| Problema | Solución |
|----------|----------|
| Menús no se abren | Añade `EnsureStartSceneLoaded` |
| NPC no se mueve suavemente | Verifica NavMeshAgent config |
| Quest no se completa | Verifica `completionMode` y `dlgTurnIn` |
| Targeting no funciona | NPC debe estar en Layer `Enemy` |

**Más soluciones:** Consulta [DOCUMENTACION_TECNICA.md](docs/DOCUMENTACION_TECNICA.md)

---

## 📝 Convenciones de Código

```csharp
// ✅ CORRECTO - Usar Services y Singletons
var dm = DialogueManager.Instance;
if (PlayerService.TryGetComponent(out Inventory inv)) { }

// ❌ INCORRECTO - NUNCA usar FindObject
var dm = FindObjectOfType<DialogueManager>(); // PROHIBIDO
```

---

**Última actualización:** Diciembre 2025  
**Documentación completa:** [docs/DOCUMENTACION_TECNICA.md](docs/DOCUMENTACION_TECNICA.md)


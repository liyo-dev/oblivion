# 🌟 El Sendero de las Estrellas

**Género:** RPG de acción/aventura  
**Motor:** Unity 2020.3+  
**Estado:** En desarrollo activo

---

## 📘 Documentación

### [📖 Documentación Técnica Completa](docs/DOCUMENTACION_TECNICA.md)

**Ubicación:** `docs/DOCUMENTACION_TECNICA.md`

Contiene toda la documentación del proyecto:
- ✅ Arquitectura de escenas (START como núcleo)
- ✅ Sistema de NPCs (FSM con NPCBehaviourManagerV2)
- ✅ Sistema de combate completo
- ✅ Sistema de quests con iconos
- ✅ Sistemas Core (Input, Dialogue, Localización)
- ✅ Sistema de guardado
- ✅ Sistema de narrativa interactiva
- ✅ **Sistema de Puzzles (Burnable, PressurePlate, PlatformElevator)** ⭐ NUEVO
- ✅ **Sistema de Iconos en Diálogos (TextMeshPro)** ⭐ NUEVO
- ✅ **Sistema de Iluminación (Bake Nocturno Optimizado)** ⭐ NUEVO
- ✅ Debugging (F3, F4)
- ✅ Troubleshooting completo
- ✅ Mejores prácticas
- ✅ Roadmap

---

## 🚀 Quick Start

### Para Iniciar el Proyecto:

1. **Abre Unity 2020.3 o superior**
2. **Inicia desde:** `Assets/Scenes/Systems/Start.unity`
3. El sistema carga MainMenu automáticamente (managers persistentes)

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
│   ├── Puzzle/            ← PressurePlate, PlatformElevator, Burnable ⭐
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

## 🎮 Sistemas Principales

### Sistema de NPCs (FSM Modular)
- Arquitectura de estados finitos completamente configurable
- ScriptableObjects reutilizables para configs
- Estados: Idle, Wander, Alert, Combat, Cinematic
- Sistema de combate completo con IA táctica

### Sistema de Combate
- Detección automática de jugador
- AlertState con iconos visuales y diálogos
- Barras de vida instanciadas automáticamente
- Diálogos en combate (alerta, derrota, post-derrota)

### Sistema de Quests
- Cadenas de quests configurables
- Iconos persistentes (!, ?)
- Detección automática de items
- Integración con diálogos

### Sistema de Puzzles ⭐ NUEVO
- **Burnable:** Objetos quemables con elementos mágicos
- **PressurePlate:** Interruptores de presión
- **PlatformElevator:** Plataformas móviles con encadenamiento
- Feedback completo (camera shake, SFX, VFX)
- Gizmos para debugging visual

### Sistema de Diálogos ⭐ NUEVO
- Iconos de botones y items en textos
- TextMeshPro Sprite Assets
- Sintaxis simple: `<sprite name="ButtonA">`
- Herramienta de configuración: `Tools → Dialogue → Setup Icons`

### Sistema de Iluminación ⭐ NUEVO
- Bake nocturno optimizado (2-4 horas)
- Configuración balanceada calidad/tiempo
- Lightmaps de producción con Global Illumination

---

## 🐛 Solución de Problemas Rápida

| Problema | Solución |
|----------|----------|
| Menús no se abren | Añade `EnsureStartSceneLoaded` |
| NPC no se mueve suavemente | Verifica NavMeshAgent config |
| Quest no se completa | Verifica `completionMode` y `dlgTurnIn` |
| Targeting no funciona | NPC debe estar en Layer `Enemy` |
| Interruptor no detecta | Collider debe ser Trigger + objeto con Rigidbody |
| Iconos no aparecen | `Tools → Dialogue → Setup Icons` |
| Errores AI Toolkit | **Ignorar** - Son inofensivos |

**Más soluciones:** Consulta [DOCUMENTACION_TECNICA.md](docs/DOCUMENTACION_TECNICA.md) → Sección 15 (Troubleshooting)

---

## 📝 Convenciones de Código

```csharp
// ✅ CORRECTO - Usar Services y Singletons
var dm = DialogueManager.Instance;
if (PlayerService.TryGetComponent(out Inventory inv)) { }

// ✅ CORRECTO - Eventos C# tipados
QuestManager.OnQuestCompleted += HandleQuestComplete;

// ❌ INCORRECTO - FindObjectOfType en runtime
var qm = FindObjectOfType<QuestManager>(); // NO

// ✅ CORRECTO - ScriptableObjects para configuración
public NPCCombatConfig combatConfig;
```

---

## 🎯 Filosofía del Proyecto

> **"RPG clásico simple: desde el Inspector digo 'narrativa completa misión → activa otra → gana batalla → NPC se mueve'. Sin código denso ni complejidad innecesaria."**

### Principios:
1. **Configuración desde Inspector** - Todo accesible sin código
2. **Eventos C# tipados** - Sistema centralizado
3. **ServiceLocator** - Referencias globales sin `FindObjectOfType`
4. **Localización First** - Todo texto usa IDs
5. **Modular y Extensible** - Sistemas independientes

---

## 🔧 Herramientas de Desarrollo

- **F3** - Toggle debug visual NPCs
- **F4** - Toggle debug UI panel
- **Tools → Dialogue → Setup Icons** - Configurar iconos en diálogos
- **Window → Rendering → Lighting** - Configurar bake de luces

---

## 📚 Documentación Adicional

- **[docs/DOCUMENTACION_TECNICA.md](docs/DOCUMENTACION_TECNICA.md)** - Documentación técnica completa (5000+ líneas)
- Todos los scripts tienen comentarios XML detallados
- Gizmos en Scene view para debugging visual
- Debug panels en runtime (F3, F4)

---

## 🚧 Estado del Proyecto

**Última actualización:** Enero 2026

**Sistemas Completados:**
- ✅ FSM de NPCs modular
- ✅ Sistema de combate con IA
- ✅ Sistema de quests con iconos
- ✅ Sistema de diálogos con localización
- ✅ Sistema de guardado
- ✅ Sistema de puzzles (Burnable, PressurePlate)
- ✅ Sistema de iconos en diálogos
- ✅ Sistema de iluminación optimizado

**En Desarrollo:**
- 🔄 Sistema de inventario expandido
- 🔄 Más tipos de puzzles
- 🔄 Boss fights con fases

---

## 📞 Recursos

**Para más información:**
- Consulta `docs/DOCUMENTACION_TECNICA.md` para guías detalladas
- Revisa los comentarios XML en los scripts
- Usa Gizmos en Scene view para visualizar sistemas
- Activa debug visual (F3) y panel (F4) en runtime

---

**¡Proyecto en desarrollo activo!** 🎮

Consulta la [documentación técnica completa](docs/DOCUMENTACION_TECNICA.md) para información detallada de todos los sistemas.


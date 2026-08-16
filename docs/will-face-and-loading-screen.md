# Will — pantalla de carga (cuerpo + cara)

Notas técnicas de cómo funciona el "showcase" de personajes en la pantalla de
carga, para poder cambiar rápidamente el gesto o la expresión de Will (o de
Estela/Liam) sin tener que volver a investigar todo desde cero.

## 1. Cómo se arma la escena de carga

- Escena: `Assets/Scenes/Systems/LoadingScreen.unity`
- `LoadingScreenController.cs` (en `Assets/Scripts/UI/`) lleva la barra de
  progreso; cuando llega al 100% llama a `characterStage.PlayReveal()`.
- `LoadingCharacterStage.cs` tiene un array `characters` con exactamente 3
  `LoadingShowcaseCharacter` (Will, Estela, Liam) y les dispara el reveal a
  la vez.
- `LoadingShowcaseCharacter.cs`: cada instancia tiene `gestureIndex`
  (0 = Will, 1 = Estela, 2 = Liam). En `Awake()` fija el parámetro int
  `GestureIndex` del Animator; `PlayReveal()` dispara el trigger `Reveal`.
- Los tres personajes que se ven en la carga son **copias "showcase" muy
  recortadas** de sus prefabs de gameplay: la `PrefabInstance` en la escena
  tiene un `m_RemovedComponents` larguísimo que quita ~40 scripts de
  gameplay (diálogo, combate, `NPCEmotionController`, IA, etc.), dejando
  solo malla + Animator + `LoadingShowcaseCharacter`.
- Will en la escena: GameObject `WILL_LoadingShowcase`, `PrefabInstance`
  cuyo `m_SourcePrefab` es `Assets/Prefabs/_WILL.prefab`
  (guid `392e26f6a7263c241a8673d723f24f9a`).

**Importante:** como el showcase quita `NPCEmotionController`, la cara de
Will en la pantalla de carga **no** usa el sistema de emociones normal del
juego (ver sección 3). Se controla a mano con overrides de
`m_IsActive` sobre los GameObjects de malla dentro de la `PrefabInstance`,
directamente en `LoadingScreen.unity`.

## 2. Cambiar el gesto/animación del cuerpo (baile, saludo, etc.)

Animator Controller: `Assets/Art/Characters/Animator/LoadingShowcase.controller`

Tiene 4 estados: `Run` (idle por defecto) y tres estados de gesto, uno por
personaje, cada uno con su propio `m_Motion`:

| Estado                    | Personaje | Motion actual                         |
|---------------------------|-----------|----------------------------------------|
| `Gesture_Will_Dance`      | Will      | `Dance_NoWeapon.fbx` (guid `c0250a82e5a93b34e8e6664afc468072`) |
| `Gesture_Estela_Victory`  | Estela    | guid `7a8447b2e7fad724cb46735e44432fb2` |
| `Gesture_Liam_Greeting`   | Liam      | guid `3d1d4502ced06254488383e237393f12` |

Transición a cada uno: `GestureIndex == 0/1/2` + trigger `Reveal`.

### Receta para poner otra animación en el gesto de Will

1. Elegí el `.fbx` de destino (ej. un clip de la carpeta
   `Assets/Plugins/Kevin Iglesias/Human Animations/...`).
2. Abrí su `.meta` y buscá, dentro de `clipAnimations`, el campo
   `internalID:` (si es `0`, Unity genera el fileID automáticamente y hay
   que sacarlo de otro lado — ver punto 4; si NO es `0`, ese número **es**
   el fileID exacto a usar).
3. En `LoadingShowcase.controller`, en el estado que quieras tocar, cambiá:
   ```yaml
   m_Motion: {fileID: <internalID del .meta>, guid: <guid del .fbx>, type: 3}
   ```
4. Si el `internalID` del `.meta` es `0` (como pasa con los packs tipo "RPG
   Tiny Hero Duo"), Unity asigna el fileID en base a un hash interno que no
   se puede calcular a mano de forma confiable. En ese caso lo más seguro es
   copiar el patrón de otro estado ya funcionando en el propio proyecto que
   use un clip del mismo pack/importer (comprobado empíricamente: varios
   estados de este controller —Run, Dance, Victory, Greeting— comparten el
   mismo fileID `1827226128182048838` a pesar de tener guids distintos,
   porque son todos "single-clip" del mismo tipo de importer), o simplemente
   arrastrar el clip a mano en el Editor de Unity (Animator → seleccionar
   estado → campo Motion) y dejar que Unity escriba el fileID correcto.
5. Ejemplo ya verificado en este proyecto: para animaciones "Kevin Iglesias"
   con `internalID` explícito, el fileID coincide exactamente (confirmado
   cruzando `HumanM@Cry01.fbx.meta` con el estado `Cry01` ya usado en
   `Assets/Art/Characters/Animator/NPC_NoWeapon.controller`).

*(15/08/2026: se probó cambiar el gesto de Will a `HumanM@Laugh01 - Loop.fbx`
—guid `3b50cadea370a8c47b3293a2056f8c58`, fileID
`-7684013397611012306`— pero se revirtió a pedido: Will se queda con el
baile original. Dato queda documentado por si se quiere retomar.)*

## 3. Sistema de emociones normal (gameplay, NO la pantalla de carga)

- Enum: `Assets/Scripts/Dialogue/NPCEmotion.cs`
  ```csharp
  public enum NPCEmotion
  {
      None = -1, Neutral = 0, Happy = 1, Sad = 2, Angry = 3,
      Surprised = 4, Scared = 5, Thinking = 6, Tired = 7, Smirk = 8
  }
  ```
- Datos: `Assets/_EmotionProfile/NpcEmotionProfile.asset` — mapea cada
  emoción a un mesh de ojos, un mesh de boca y un estado de animación de
  cuerpo:

  | Emoción     | Ojos   | Boca     | Animación cuerpo |
  |-------------|--------|----------|-------------------|
  | Neutral (0) | Eye07  | Mouth02  | Idle02 |
  | Happy (1)   | Eye07  | Mouth09  | HeadNod01 |
  | Sad (2)     | Eye10  | Mouth05  | Cry01 |
  | Angry (3)   | Eye04  | Mouth05  | Angry02 |
  | Surprised (4)| Eye05 | Mouth07  | SenseSomethingStart_NoWeapon |
  | Scared (5)  | Eye09  | Mouth08  | Fear01 |
  | Thinking (6)| Eye07  | Mouth11  | InteractWithPeople_NoWeapon |
  | Tired (7)   | Eye09  | Mouth12  | IdleWounded01 |
  | Smirk (8)   | Eye09  | Mouth06  | Talk03 |

- Aplicador: `Assets/Scripts/Behaviour NPC/NPCEmotionController.cs` →
  `ActivateMesh()` cachea todos los hijos `EyeXX`/`MouthXX` del personaje y
  `SetEmotion()` activa uno solo (`SetActive(true)`) y apaga el resto, según
  eventos del `DialogueManager`. Esto es lo que se usa en diálogos normales,
  **no** en la pantalla de carga.

## 4. Todas las mallas de cara disponibles en `_WILL.prefab`

(guid del prefab: `392e26f6a7263c241a8673d723f24f9a` — mismo guid a usar
como `guid:` en cualquier `target:` de un override de `PrefabInstance`)

| Malla | fileID |
|---|---|
| Eye01 (activo por defecto en gameplay) | `5254754835644330641` |
| Eye02 | `7501852539256436559` |
| Eye03 | `1162284283722129840` |
| Eye04 | `6069346623140206473` |
| Eye05 | `9155105934263643200` |
| Eye06 | `5423045624319805736` |
| Eye07 | `197900767370042593` |
| Eye08 | `6948670945204452364` |
| Eye09 | `7559377514189507724` |
| Eye10 | `5726711147477198277` |
| Eye11 | `8875381700379141536` |
| Eye12 | `3043178867043854285` |
| Eyebrow01 | `5419249452531156079` |
| Eyebrow02 | `3341420175712009529` |
| Mouth01 (activo por defecto en gameplay/loading) | `206955243820471005` |
| Mouth02 | `3032424197757487374` |
| Mouth03 | `4539734167150270997` |
| Mouth04 | `4363681766763261340` |
| Mouth05 (= "Sad" en el profile) | `4637064397280096681` |
| Mouth06 | `2372032324012054292` |
| Mouth07 | `5400826773061602255` |
| Mouth08 | `5442141579115179535` |
| Mouth09 (= "Happy" en el profile) | `2675442367618238850` |
| Mouth10 | `8861551751541942426` |
| Mouth11 | `669822185330387839` |
| Mouth12 | `3045638440737357862` |

No hay ningún alias tipo "Sonrisa"/"Triste" en el proyecto — los únicos
nombres semánticos son los de la tabla de la sección 3. Para elegir una
malla nueva sin referencia clara, hay que probarla visualmente en el
Editor.

## 5. Receta: cambiar la cara de Will SOLO en la pantalla de carga

(sin tocar `_WILL.prefab`, que es compartido con el gameplay normal)

En `Assets/Scenes/Systems/LoadingScreen.unity`, buscar el bloque
`PrefabInstance` de `WILL_LoadingShowcase` (`m_SourcePrefab` con guid
`392e26f6a7263c241a8673d723f24f9a`) y, dentro de `m_Modifications`, agregar
dos entradas por cada malla a cambiar: una para apagar la que está activa y
otra para prender la nueva. Formato:

```yaml
- target: {fileID: <fileID de la malla a apagar>, guid: 392e26f6a7263c241a8673d723f24f9a, type: 3}
  propertyPath: m_IsActive
  value: 0
  objectReference: {fileID: 0}
- target: {fileID: <fileID de la malla a prender>, guid: 392e26f6a7263c241a8673d723f24f9a, type: 3}
  propertyPath: m_IsActive
  value: 1
  objectReference: {fileID: 0}
```

**Estado actual (15/08/2026):** se agregó exactamente este override para
apagar `Mouth01` y prender `Mouth09` (boca "Happy"), a pedido del usuario —
"la boca de tristeza" que se veía por defecto (Mouth01, la pose de reposo
del FBX) se cambió por la boca alegre del profile de emociones. El gesto de
cuerpo se dejó en `Gesture_Will_Dance` (sin cambios). No se tocaron los
ojos (`Eye01` sigue activo) — si en algún momento se pide juego de ojos
"felices" a games, sería `Eye07` (ver tabla de la sección 3).

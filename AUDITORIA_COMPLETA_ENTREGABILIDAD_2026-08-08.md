# Auditoría completa de entregabilidad — El Sendero de las Estrellas
**Fecha:** 8 de agosto de 2026 · **Autor:** Claude (Cowork), a petición de Raúl · **Objetivo:** valorar si el proyecto está en condiciones de ser mostrado a un estudio/publisher o de salir como demo pública, y qué falta para ese nivel.

**Método:** esta auditoría no repite desde cero el trabajo de código ya hecho ayer (`AUDITORIA_CODIGO_2026-08-07.md` y `AUDITORIA_SISTEMAS_OBSOLETOS_2026-08-07.md`, 530 archivos revisados) — lo verifica puntualmente y lo integra. El foco de hoy es todo lo que esas dos auditorías **no** cubren: testing/QA, ajustes de rendimiento y FPS a nivel de Project Settings, preparación de build para tienda, higiene de repositorio y paquetes. Verifiqué en vivo contra el código y los `.asset` reales: `ProjectSettings.asset`, `QualitySettings.asset`, `GraphicsSettings.asset`, `DynamicsManager.asset`, `Packages/manifest.json`, `.gitignore`/`.gitattributes`, `EditorBuildSettings.asset`, `git log`, y releí `DialogueManager.cs`/`PlayerActionManager.cs` línea a línea para confirmar que dos de los bugs "críticos" de ayer siguen presentes hoy.

---

## 0. Veredicto general

El código en sí está en **muy buen estado para un proyecto indie en solitario** — la auditoría de ayer ya lo dice y hoy lo confirmo: cero `OverlapSphere` sin `NonAlloc`, buffers cacheados, FSM sólida, pooling de VFX centralizado, sistema de guardado con escritura atómica. Eso no es lo que separa este proyecto de "nivel estudio" ahora mismo.

Lo que sí lo separa son tres cosas que no son bugs de código sino **ausencias de proceso**, y son las que una empresa grande mira primero:

1. **Cero tests automatizados.** El proyecto tiene instalados `com.unity.test-framework`, `test-framework.performance` y `testtools.codecoverage` en `manifest.json` — pero no existe ni un solo archivo de test (`*Test*.cs`, `.asmdef` de tests) en todo `Assets/`. Los paquetes están puestos pero nunca usados.
2. **El identificador de build sigue siendo el del template de Unity.** `applicationIdentifier` es literalmente `com.Unity-Technologies.com.unity.template.urp-blank` (Standalone/iOS) y `com.UnityTechnologies.com.unity.template.urp-blank` (Android). `projectName: Test`. Esto no es un detalle: si mañana se sube un build a Steam o a cualquier tienda tal cual está, sale con la identidad del blank template de Unity, no la del juego.
3. **No hay ningún proceso de verificación automatizado** (CI, build check, ni siquiera un test runner) — ni falta hace decirlo, dado el punto 1, pero es la razón estructural por la que las regresiones se detectan jugando manualmente y no antes de tocar código.

Ninguna de las tres es difícil de arreglar. Ninguna requiere rediseñar nada. Pero las tres son exactamente lo que un revisor externo (publisher, QA de una empresa grande, o un port house) señalaría en los primeros 10 minutos, antes incluso de mirar una línea de gameplay.

Por debajo de esto, el resto del proyecto — arquitectura, rendimiento por frame, higiene de Git — está genuinamente bien y no necesita una "limpieza de choque", solo rematar lo que ya está empezado.

---

## 1. Bloqueadores reales para "entregable" (arreglar antes que nada)

### 1.1 Identidad del proyecto sin configurar — **bloqueante para cualquier build público**
`ProjectSettings/ProjectSettings.asset`

```
applicationIdentifier:
  Android: com.UnityTechnologies.com.unity.template.urp-blank
  Standalone: com.Unity-Technologies.com.unity.template.urp-blank
  iPhone: com.Unity-Technologies.com.unity.template.urp-blank
projectName: Test
organizationId: luarbaz
templateDefaultScene: Assets/Scenes/SampleScene.unity   (vestigio, no afecta al build real)
metroPackageName: Test
metroApplicationDescription: Test
```

El proyecto nace de `com.unity.template.urp-blank` (`clonedFromGUID` + `templatePackageId` lo confirman) y esos campos nunca se tocaron. `companyName: Liyodev` y `productName: El Sendero de las Estrellas` sí están bien puestos — son los que se ven en la ventana del juego y en el `.exe` — pero el `applicationIdentifier` (bundle ID) es el que usan Steam, Google Play y Apple para identificar la app de forma única, y ahora mismo apunta al paquete de ejemplo de Unity. Si se sube así, choca de bruces con cualquier control de calidad de tienda.

**Fix:** Project Settings → Player → Other Settings → Identification. Poner algo tipo `com.liyodev.elsenderodelasestrellas` en las tres plataformas, y `projectName` a un nombre real. Es un cambio de 2 minutos, cero riesgo — pero es el que más "vergüenza empresa grande" causaría de los tres si se pasa por alto.

### 1.2 Cero tests automatizados — infraestructura instalada, cero uso
`Packages/manifest.json` tiene:
```
"com.unity.test-framework": "1.7.0",
"com.unity.test-framework.performance": "3.5.0",
"com.unity.testtools.codecoverage": "1.3.0",
```
Búsqueda exhaustiva en los 445 `.cs` de `Assets/Scripts` (más `NarrativeGraph`, `Editor`): **cero** archivos de test, **cero** `.asmdef` de tipo Tests (de hecho cero `.asmdef` en todo el proyecto — ver §3). `playModeTestRunnerEnabled: 0` en Player Settings. `TDD.md`, el documento que el propio proyecto declara "fuente de verdad", no tiene ninguna sección de testing/QA en su índice (14 secciones: arquitectura, NPCs, jugador, sistemas core, audio, quests, diálogos, guardado, narrativa, UI, rendimiento, bugs, troubleshooting — ninguna de QA).

Esto no significa que el juego no se pruebe — claramente se prueba mucho a mano (los presets de `_BootProfile`, F3/F4 de debug, las escenas de `Test/` lo demuestran) — pero **cero de esa cobertura sobrevive de una sesión a otra**. Cualquier regresión se descubre jugando, no en segundos al guardar un archivo.

Dado que el árbitro de rendimiento y los bugs de ciclo de vida de corrutinas de la auditoría de ayer (C1–C7) son exactamente el tipo de bug que un test de EditMode/PlayMode atraparía en segundos (p. ej. "abrir dos diálogos seguidos no debe perder el callback del primero"), esto no es un "nice to have" de proceso — es la razón concreta por la que esa familia de bugs lleva tiempo sin detectarse.

**Recomendación realista (no "hacer TDD retroactivo de todo el juego", que no es viable para un dev en solitario):**
- Empezar por 5-8 tests de EditMode que cubran los invariantes ya documentados como "no negociables" en CLAUDE.md §4 (los del grafo narrativo) — son los que más cuestan un bug de producción si se rompen.
- Un test de PlayMode que reproduzca exactamente el escenario de C1 (`DialogueManager.StartDialogue` reentrante) y C2 (`PushMode` sin refcount) de la auditoría de ayer — ambos siguen presentes hoy (ver §2), y son perfectos como primer par de tests porque el bug y el fix ya están identificados.
- Activar `com.unity.testtools.codecoverage` en el runner para tener una cifra objetiva de cobertura, aunque empiece en un dígito bajo.

### 1.3 Sin CI ni verificación automática de build
No hay carpeta `.github/` (ni ningún otro CI) en el repo. Ningún hook de pre-commit corre tests o valida compilación. El validador narrativo (`CrossSystemNarrativeValidator`, mencionado en CLAUDE.md §7) existe pero es una `MenuItem` manual del editor, no algo que corra solo. Para un proyecto en solitario esto es razonable hoy, pero es lo primero que pediría un equipo de QA externo antes de aceptar builds regulares: al menos un job que compile el proyecto (o corra las escenas de test) en cada push.

---

## 2. Estado real de los críticos de la auditoría de ayer (verificado hoy, no asumido)

Releí `DialogueManager.cs` y `PlayerActionManager.cs` línea a línea contra el código actual (no contra la copia de ayer):

- **C1 (reentrada en `DialogueManager.StartDialogue`) — CONFIRMADO, sigue abierto hoy.** Línea 319-326: `StartDialogue` sobrescribe `_current`/`_onEnd` sin comprobar `IsOpen` (línea 92) ni invocar el `_onEnd` anterior. El fix propuesto ayer sigue siendo válido y no se ha aplicado.
- **C2 (`PushMode` sin refcount) — CONFIRMADO, sigue abierto hoy.** Línea 251: `if (Top == mode) return;` sigue ahí exactamente como se describió. `PopMode` (línea 259-274) sigue quitando una entrada del stack aunque el segundo `Push` haya sido ignorado por el early-return.

No relanzo el resto de la lista de ayer (sería redundante con `AUDITORIA_CODIGO_2026-08-07.md`, que ya la tiene priorizada con el "orden de ataque" al final). El dato importante es que esta auditoría se apoya en hallazgos reales de hace 24h, no en una foto vieja — y que el fix de más impacto (C2, refcount de `PushMode`) sigue siendo la tarea más rentable: una tarde de trabajo, elimina de un plumazo los conflictos diálogo↔cinemática↔victoria↔stun.

---

## 3. Rendimiento y FPS — lo que no cubrió la auditoría de código

La auditoría de ayer ya cubrió el rendimiento *por frame* (Update/FixedUpdate, NonAlloc, GC). Esto es lo que falta a nivel de configuración de proyecto:

**Sin arquitectura de compilación (`.asmdef`).** Cero archivos `.asmdef` en todo el proyecto — 445 scripts compilan como un único `Assembly-CSharp` monolítico. No afecta al rendimiento en runtime, pero sí a la velocidad de iteración (cualquier cambio en un script recompila los 445) y es una práctica estándar en proyectos "AAA" que aquí falta por completo. Dividir al menos `Core`/`Runtime` de `Editor`/`Tests` sería el primer paso — y además es un prerrequisito real para poder tener tests de EditMode aislados (§1.2).

**Layer Collision Matrix sin configurar — `DynamicsManager.asset`:**
```
m_LayerCollisionMatrix: ffffffff... (todo colisiona con todo, el default de Unity)
```
El proyecto tiene capas dedicadas (`Enemy`, `Player`, `Projectile`, `ProjectileEnemy`, `Interactable`, `Floor`, `Obstacle`, `Climb`, `UI`, `Water`...) pero la matriz de colisión física nunca se personalizó: sigue siendo la que trae Unity por defecto, donde absolutamente todas las capas colisionan entre sí. Esto es tanto un tema de rendimiento (el motor de físicas evalúa pares de colliders que nunca deberían tocarse, p. ej. `UI` contra `Floor`) como de corrección — es la misma raíz del problema que CLAUDE.md ya documenta a mano ("los personajes no tienen capa propia, viven en `Default`"): el proyecto compensa con chequeos por componente (`NPCSimpleAnimator`) en tiempo de ejecución un problema que la matriz de colisión debería filtrar en el motor de físicas, gratis y antes de que el código de gameplay se entere. **Recomendación:** dar a los personajes una capa `Character` propia y configurar la matriz (p. ej. `Projectile` no debería colisionar con `ProjectileEnemy`, `UI` no debería colisionar con nada físico).

**Anti-aliasing desactivado en ambas calidades — `QualitySettings.asset`:**
```
name: Mobile → antiAliasing: 0
name: PC     → antiAliasing: 0
```
Con URP y MSAA/TAA disponibles, un proyecto que aspira a mostrarse con pulido visual "AAA" normalmente al menos ofrece una opción de AA en la calidad PC. Ahora mismo ninguna de las dos calidades la trae por defecto — puede ser intencional (rendimiento en gama baja), pero vale la pena una decisión explícita en vez de heredarlo del blank template.

**`vSyncCount: 0` en ambas calidades.** No hay VSync por defecto en ningún tier — coherente con dejar el framerate sin techo en manos de `Application.targetFrameRate` en código (no verificado aquí a nivel de script; merece una comprobación rápida de que existe un cap explícito, porque sin VSync ni target framerate el juego corre a la FPS que dé la GPU, con el consumo/calentamiento que eso implica en un build de demo).

**Puntos positivos que sí valen la pena mencionar (no solo hay que apuntar problemas):** `gcIncremental: 1` (GC incremental activado — reduce los picos de frame por recolección de basura, justo lo que un juego de acción/combate necesita), `m_MTRendering: 1` (renderizado multihilo activo), `m_BuildTargetBatching` con `m_StaticBatching: 1` para Standalone, y URP con SRP configurado correctamente (`m_CustomRenderPipeline` apuntando al asset URP en ambas calidades). La base de configuración de rendimiento está bien elegida donde importa; lo que falta es terminar de personalizar lo que aún trae el valor por defecto del template.

**Logging en rutas calientes (ya señalado ayer, lo confirmo como el hallazgo de rendimiento más rentable de arreglar antes de un build de demo):** `NPCCombatBrain` (107 logs, 3 guardados con `#if`), `NPCCombatLifecycleHandler` (103, cero guardados) son los focos gordos. Es una regla que el propio CLAUDE.md §2 ya exige ("todo `Debug.Log` bajo `#if UNITY_EDITOR || DEVELOPMENT_BUILD`") y que en estos dos archivos no se cumple — coste real en build de combates con varios NPCs.

---

## 4. Higiene de proyecto y repositorio

**Git — en buen estado, sin acción urgente.** `.gitignore` cubre correctamente `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`, builds y archivos temporales de análisis; ningún artefacto pesado colándose en el repo hoy (los `t2.txt`/`test_delete_me.txt` que señalaba la auditoría de sistemas obsoletos de ayer ya no están en `Assets/`, así que o se limpiaron o el hallazgo ya está resuelto). `.gitattributes` configura Git LFS correctamente para texturas, modelos, audio y vídeo, con normalización de line endings a LF y el merge driver de Unity YAML documentado. El historial de commits es activo y descriptivo (mezcla español/inglés, pero mensajes con sustancia, no "wip" sueltos). Único matiz: 8 de los 25 últimos commits llevan mensaje en inglés pese a que CLAUDE.md §2 pide "mensajes de commit en español" — inconsistencia menor, cero impacto funcional.

**Documentación — mayormente ejemplar, con una fecha que no cuadra.** Tener `CLAUDE.md`/`AGENTS.md`/`TDD.md`/`README.md` tan cuidados y actualizados es, honestamente, mejor práctica de la que tienen muchos estudios pequeños. El único hallazgo real: `TDD.md` se autodescribe como "Motor: Unity 2022.3+" y "Última revisión: Mayo 2026", pero el proyecto corre hoy sobre Unity 6000.5.4f1 y CLAUDE.md/README ya lo tienen actualizado a "Unity 6 + URP 17.5". Si `TDD.md` es la fuente de verdad declarada, vale la pena una pasada de actualización del encabezado — no cambia nada técnico, pero alguien externo que abra ese archivo primero se lleva una impresión de documentación desactualizada que no es real.

**Paquetes — dos candidatos a revisar, no a borrar a ciegas.** `manifest.json` incluye `com.unity.ads` (4.19.0) y `com.unity.analytics` (3.8.2). No pude confirmar desde aquí si se usan en código (requeriría grep de contenido sobre los 445 scripts, fuera del alcance de esta pasada), pero ninguno de los dos aparece mencionado en TDD.md/CLAUDE.md como sistema activo, y `com.unity.analytics` es el paquete legacy que Unity fue sustituyendo por Unity Gaming Services. Vale la pena una comprobación de 5 minutos (`grep -r "UnityEngine.Advertisements\|UnityEngine.Analytics"` sobre `Assets/`) antes de decidir si se quitan — cada paquete de más es superficie de mantenimiento y tiempo de import.

---

## 5. Lo que ya está a nivel de estudio grande (para que quede dicho, no solo lo que falta)

- La disciplina de rendimiento por frame (NonAlloc, buffers cacheados, hashes de animator) está por encima de la media indie, confirmado independientemente hoy en los dos archivos que releí.
- El patrón `ResetStatics` para limpiar estado estático entre sesiones de PlayMode, aplicado en la mayoría de singletons, es exactamente el tipo de disciplina que evita bugs "solo en el editor" — la lista de excepciones (M9 en la auditoría de ayer) es corta y ya está identificada.
- El sistema de guardado con escritura atómica (`.tmp` + `File.Move`) es la práctica correcta para no corromper saves ante un crash.
- La decisión documentada de **no** fusionar los dos motores narrativos (CLAUDE.md §7) tras un intento fallido es exactamente la clase de decisión de arquitectura madura que un equipo grande valora — reconocer cuándo no tocar algo que funciona es tan importante como refactorizar.
- Convención de idioma, estructura de carpetas y nomenclatura consistentes en todo el proyecto.

---

## 6. Plan de acción priorizado para "entregable"

1. **Identidad del build** (§1.1) — 2 minutos, cero riesgo, bloqueante para cualquier subida a tienda. Hacerlo ya, antes de generar ningún build de demo.
2. **C2 — refcount en `PushMode`** (§2) — una tarde, el fix de código más rentable de todo el informe de ayer.
3. **C1 — reentrada en `DialogueManager`** (§2) — el soft-lock narrativo más probable en una sesión de demo real.
4. **Layer Collision Matrix** (§3) — una tarde: crear capa `Character`, revisar la matriz. Beneficio de rendimiento y de corrección a la vez.
5. **Primeros 5-8 tests** (§1.2) — empezar por los invariantes narrativos de CLAUDE.md §4 y por reproducir C1/C2 como test antes de arreglarlos (así queda el test como red de seguridad permanente, no solo el fix puntual).
6. **Logging en rutas calientes de combate** (M13 de ayer / §3 aquí) — antes de cualquier build de rendimiento medido con perfilador.
7. Resto de críticos/altos de `AUDITORIA_CODIGO_2026-08-07.md` en el orden que ya proponía ese documento (C3, C4, C5, C6, C7, A1-A12).
8. Antes de la demo de Steam específicamente: todo lo de `STEAM_DEMO_CHECKLIST.md` sigue siendo el checklist correcto — esta auditoría no lo sustituye, lo complementa (el punto 1 de aquí, identidad del build, es un prerrequisito técnico que ese checklist da por hecho pero no verifica explícitamente).

Nada de esto exige parar el desarrollo ni rehacer sistemas. El patrón general, otra vez: la infraestructura y el código reciente son de buena calidad; lo que falta es peinar la configuración de proyecto heredada del template y cerrar el hueco de proceso (tests, CI) que hoy hace que cada verificación dependa de jugar a mano.

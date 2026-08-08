# Diseño: Cielo unificado, clima dinámico y cielo nocturno temático (nubes, estrellas, arcoíris)

**Proyecto:** El Sendero de las Estrellas
**Fecha:** 8 agosto 2026
**Estado:** Propuesta de diseño — pendiente de aprobación antes de implementar

Punto de partida (tal cual lo has planteado): la mejora reciente de nubes quedó bien y dispara tres ideas más:

1. Un único skybox genérico + jugar con la luz para vender amanecer/día/atardecer/noche, sin tantas franjas como hay ahora.
2. Nubes que se instancian con más variedad de comportamiento: se nubla un poco, se va, vuelve, se pone negro y llueve — no solo "lluvia sí/no".
3. Cielo nocturno temático (coherente con el nombre del juego y con que las estrellas son el destino final, aunque metafórico): estrellas doradas cubriendo el cielo, estrellas fugaces, arcoíris tras la lluvia.

Todo lo citado abajo (rutas, clases, shaders, propiedades) está verificado leyendo el código y los assets reales del proyecto, no asumido.

---

## 0. Diagnóstico previo (por qué esto no es un simple "cambiar un material")

- **El ciclo actual tiene 7 franjas activas, no 4.** `Assets/Scripts/World/DayNightCycle.cs` define el enum `TimeOfDay` con 9 valores (`Morning, BrightMorning, AfterNoon, EarlyDusk, Sunset, Night, Midnight, Cloudy, HaloSky`), y el array `timeSettings[]` configurado en el propio script instancia **7** de ellos (`Cloudy` y `HaloSky` no se usan hoy en el ciclo, aunque existen como valores del enum y como materiales de skybox en disco). Cada franja trae su propio `Material skybox`.
- **Los skyboxes ya son "genéricos" en el sentido técnico que pides — el problema es que hay 9, no 1.** Comprobado en `Assets/Art/Day-Night Skyboxes/Materials/SkyNoon.mat`: el shader es `m_Shader: {fileID: 104, guid: 000...0f000...}`, que es el shader **built-in de Unity `Skybox/6 Sided`** (no un shader custom del asset pack). Este shader trae de fábrica las propiedades `_Tint` (color, ya usado hoy: `{r: 0.5, g: 0.5, b: 0.5, a: 0.5}`), `_Exposure` y `_Rotation`, además de las 6 texturas de cubemap. Es decir: **ya tienes el "skybox genérico de Unity" que pides** — el pack solo le pintó 9 sets de texturas distintos (uno por franja) y hoy `DayNightCycle` cambia el `Material` entero en cada transición en vez de tintar uno solo.
- **No existe ningún sistema de "nublado parcial".** `CloudCoverSpawner.cs` (`Assets/Scripts/World/CloudCoverSpawner.cs`) ya instancia un techo de nubes 3D reales (mallas con shader `Quibli/Cloud3D` o `Quibli/Cloud2D`, ver `Assets/Plugins/Quibli/Shaders/Cloud3D.shadergraph`) en una rejilla alrededor del jugador, con fade de alfa, pool (no vuelve a `Instantiate` en lluvias posteriores) y recentrado automático (fix INC-074). Pero está **enganchado 1:1 a los eventos de `DayNightCycle`**: `CloudsBuildingUp` (aparece) y `RainStopped` (desaparece). No hay ningún estado intermedio — o no hay nubes, o hay techo completo de tormenta. Lo que describes ("se nubla un poquito, se va, vuelve, se pone más negro y llueve") no existe todavía como concepto en el código.
- **El bug de la "línea negra entre dos nubes" — diagnóstico razonado, no confirmado visualmente.** No tengo forma de ver capturas del juego desde aquí, así que esto es una hipótesis fundamentada en el propio `BuildCoverIfNeeded()`, no una causa verificada. Candidatos, de más a menos probable:
  1. **Solape de mallas alfa-recortadas (`QuibliCloud3D`).** `heightJitter` (±12 unidades) y el `jitter` de posición (hasta 50% de `cellSize`) permiten que dos nubes vecinas se solapen en profundidad. El shader `Cloud3D` usa recorte por `_AlphaThreshold` (dithering), y donde dos mallas con recorte por dithering se solapan, los patrones de puntos de cada una pueden interferir y leerse como una línea/borde oscuro — más visible cuanto más perpendicular es el ángulo de solape.
  2. **Sin sombra propia ni recibida (`shadowCastingMode = ShadowCastingMode.Off`, `receiveShadows = false`, línea 306-307).** Esto se hizo a propósito (techo lejano, no merece el coste), pero significa que si la "línea negra" no es un artefacto de dithering sino de iluminación, no viene de sombras — hay que mirar en otro sitio (probablemente normales de la malla del Foliage Generator en el borde de unión, o el propio bake del asset de Quibli).
  3. **Rotación aleatoria en Y sin comprobar solape real.** `CloudRotation()` gira cada nube al azar sin comprobar si eso hace que su silueta invada la de la vecina más de lo esperado.
  - **Antes de tocar código de esto, lo más rentable es una captura o clip corto del momento exacto en que se ve la línea** (con el `[ContextMenu] Activar/Desactivar techo de nubes (debug)` de `CloudCoverSpawner` puedes reproducirlo a demanda). Con eso se puede diferenciar en 30 segundos si es un problema de solape geométrico (se arregla con más espaciado/menos escala máxima) o del shader Quibli en sí (se arregla en el material, o cambiando esas nubes concretas a modo `QuibliCloud2D` con billboard, que no tiene solape 3D real). Lo dejo como primer paso de la Parte B, no como algo que vaya a "arreglar a ciegas".
- **No existe ningún sistema de estrellas de cielo, estrellas fugaces ni arcoíris.** Verificado: no hay coincidencias en `Assets/` para nada tipo "starfield/shooting star/rainbow/arcoiris" salvo `Assets/Scripts/World/StarWorldLighting.cs` y `StarWorldFootprintPool.cs`, que son del **nivel final "mundo estelar"** (la metáfora final del juego) y no tienen relación con el cielo nocturno del mundo normal — de hecho `StarWorldLighting.OnEnable/Start` **desactiva `DayNightCycle` por completo** mientras esa escena esté cargada y lo reactiva al salir. Cualquier cosa que hagamos en las Partes A/C de este documento no debe tocar esa escena: sigue siendo un override total independiente, ya funciona así y no hay motivo para unificarlo.
- **Riesgo real y concreto de tocar el enum `TimeOfDay`:** hay 4 sitios más en el proyecto que referencian valores concretos del enum, y hay que auditarlos antes de reducir franjas (detalle en Parte A.3):
  - `Assets/Scripts/UI/TimeOfDayIndicator.cs` — un sprite de UI por cada periodo (`SpriteForPeriod`).
  - `Assets/Scripts/World/CampfireRestInteractable.cs` — `nightTarget = TimeOfDay.Night`, `dayTarget = TimeOfDay.Morning`, y comprueba `== Night || == Midnight` para "es de noche".
  - `Assets/Scripts/World/DayOnlyInspectionTrigger.cs` — misma comprobación `== Night || == Midnight`.
  - `Assets/NarrativeGraph/Runtime/Graph/NodeTypes/SetTimeOfDayNode.cs` — nodo de grafo narrativo con `targetTime` serializado; probablemente ya colocado en `MainNarrative.asset` con un valor concreto grabado como int.

---

## PARTE A — Cielo unificado: un solo material de skybox + 4 franjas horarias

### A.1 Qué cambia conceptualmente

- Un **único `Material` de skybox** (shader `Skybox/6 Sided`, el mismo que ya usan todos los `.mat` actuales) se queda asignado a `RenderSettings.skybox` de forma permanente. Ya no se cambia la *referencia* al material en cada transición de franja.
- Lo que varía por franja/momento es: `_Tint` (color, ya soportado), `_Exposure` (brillo) y `_Rotation` (gira el cubemap — útil para que el sol "pintado" en la textura seggase aproximadamente la posición del `directionalLight`), combinados con lo que `DayNightCycle` ya hace hoy (color/intensidad/rotación de la luz direccional, ambiente, niebla).
- Reducir de 7 franjas activas a **4: Amanecer, Día, Atardecer, Noche**, tal como pides.

### A.2 Piezas nuevas / modificadas

**1. `DayNightCycle.TimeOfDaySettings` — nuevos campos, sin tocar el enum:**

```csharp
[Header("Skybox único (tint/exposure/rotation)")]
public Color skyboxTint = new Color(0.5f, 0.5f, 0.5f, 0.5f); // _Tint del shader Skybox/6 Sided
[Range(0f, 8f)] public float skyboxExposure = 1f;             // _Exposure
[Range(0f, 360f)] public float skyboxRotation = 0f;           // _Rotation, sincronizado a ojo con sunRotationY
```

El campo `public Material skybox` existente se queda (para no romper el inspector de golpe) pero deja de usarse en el ciclo normal — se documenta como legacy/no usado, o se elimina en una segunda pasada una vez validado en juego.

**2. `DayNightCycle.Awake()` — instanciar el skybox en runtime:**

Punto importante de corrección técnica: `RenderSettings.skybox` **no** auto-instancia el material al asignarlo (a diferencia de `renderer.material`). Si mutamos `_Tint`/`_Exposure` directamente sobre el asset compartido en Play Mode, en el Editor eso **ensucia el `.mat` real** (se queda con el último valor tintado al salir de Play). Hay que crear una copia en memoria una vez:

```csharp
[SerializeField] private Material sharedSkyboxMaterial; // el ÚNICO asset de skybox, arrastrado en el Inspector
private Material _runtimeSkybox;
private static readonly int TintId = Shader.PropertyToID("_Tint");
private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
private static readonly int RotationId = Shader.PropertyToID("_Rotation");

void Awake()
{
    // ...código existente...
    if (sharedSkyboxMaterial != null)
    {
        _runtimeSkybox = new Material(sharedSkyboxMaterial); // instancia propia, nunca toca el asset
        RenderSettings.skybox = _runtimeSkybox;
    }
}

void OnDestroy()
{
    if (_runtimeSkybox != null) Destroy(_runtimeSkybox); // evitar leak del material en memoria
}
```

**3. `ApplySettingsImmediate` / `TransitionToSettings` — sustituir el swap de `Material` por mutar `_runtimeSkybox`:**

Donde hoy dice `RenderSettings.skybox = settings.skybox;`, pasa a:

```csharp
if (_runtimeSkybox != null)
{
    _runtimeSkybox.SetColor(TintId, settings.skyboxTint);
    _runtimeSkybox.SetFloat(ExposureId, settings.skyboxExposure);
    _runtimeSkybox.SetFloat(RotationId, settings.skyboxRotation);
    DynamicGI.UpdateEnvironment();
}
```

Y en la corrutina de transición (`TransitionToSettings`), estas tres se interpolan igual que `lightColor`/`ambientColor` ya se interpolan (Lerp de color y float, LerpAngle para la rotación) — mismo patrón, sin lógica nueva de por medio.

**Ojo con `ApplyStormSkybox()`/`RevertStormSkybox()`:** hoy cambian `RenderSettings.skybox` a `stormSkybox` durante la nubosidad/lluvia (opcional, ver comentario del propio campo: "recomendado dejarlo null si usas `CloudCoverSpawner`"). Con un único skybox instanciado, si se sigue queriendo ese oscurecimiento adicional del fondo lejano durante tormenta, se puede lograr **tinta/expone también el `_runtimeSkybox`** en vez de cambiar de material (mismo mecanismo, un `Lerp` más hacia un tinte de tormenta), en vez de la ruta actual de "cambiar a otro material o forzar `CameraClearFlags.SolidColor`". Se puede dejar la red de seguridad de cámara tal cual está (no lo toca esta propuesta), solo se sustituye la parte de "cambiar el asset de skybox" por "tintar el único skybox".

### A.3 Reducir a 4 franjas sin romper lo que ya depende del enum

**Decisión recomendada: no tocar el enum `TimeOfDay` (dejar los 9 valores tal cual existen hoy).** Renombrar o eliminar miembros del enum desplaza los valores `int` subyacentes de todo lo demás, y eso es justo el tipo de cambio silencioso que rompe datos ya serializados (nodos del grafo narrativo, prefabs con `CampfireRestInteractable` configurado) sin que se note hasta que se juega esa escena en concreto — el mismo tipo de riesgo que ya os hizo abortar un intento de unificación de sistemas en agosto según `CLAUDE.md` §7.

En su lugar:

1. **El array `timeSettings[]` pasa de 7 a 4 entradas**, eligiendo qué miembro del enum representa cada franja nueva:
   - **Amanecer** → `TimeOfDay.Morning` (se queda igual, ya es la franja de entrada).
   - **Día** → `TimeOfDay.AfterNoon` (recomendado sobre `BrightMorning`: ya tiene mayor `lightIntensity` — 1.3 vs 1.4, similar — y `ambientIntensity` más alta; a confirmar a ojo en el editor cuál de las dos gustaba más como "look de día").
   - **Atardecer** → `TimeOfDay.Sunset` (recomendado sobre `EarlyDusk`: colores más saturados/dorados, más "atardecer" reconocible; `EarlyDusk` queda como transición intermedia que ya no hace falta si solo hay 4 franjas).
   - **Noche** → `TimeOfDay.Night` (recomendado sobre `Midnight`: `Midnight` es casi idéntica pero más oscura — se puede recuperar ese "más de noche" simplemente alargando la `duration` de `Night` en vez de mantenerla como franja aparte).
   - `BrightMorning`, `EarlyDusk`, `Midnight` quedan sin usar en el ciclo automático (igual que ya pasa hoy con `Cloudy`/`HaloSky`), pero **sin borrar del enum**.
2. **Auditar antes de dar por cerrado**, uno por uno:
   - `CampfireRestInteractable.cs` — `nightTarget`/`dayTarget` en cualquier prefab de hoguera ya colocado en escena: revisar que sigan apuntando a `Night`/`Morning` (que se mantienen), y simplificar el check `== Night || == Midnight` a solo `== Night` si `Midnight` deja de ser alcanzable por el ciclo automático (puede seguir siendo alcanzable manualmente vía `SetTimeOfDay`, así que no es obligatorio simplificar, solo limpieza opcional).
   - `DayOnlyInspectionTrigger.cs` — mismo check, misma nota.
   - `SetTimeOfDayNode.cs` — **buscar en `MainNarrative.asset` y cualquier otro grafo** si hay algún nodo `SetTimeOfDayNode` apuntando a `BrightMorning`, `EarlyDusk`, `Cloudy` o `HaloSky`. Si existe alguno, `SetTimeOfDay()` (línea 552 de `DayNightCycle.cs`) hace un `for` sobre `timeSettings[]` y si no encuentra el `TimeOfDay` pedido, solo hace `Debug.LogWarning` y **no pasa nada más** — no rompe, pero ese nodo narrativo dejaría de tener efecto silenciosamente. Hay que revisar el grafo a mano (o con el validador que ya usáis, `CrossSystemNarrativeValidator`, si aplica aquí) antes de dar la Parte A por completa.
   - `TimeOfDayIndicator.cs` — tiene un sprite de UI por periodo (`SpriteForPeriod`). Con 4 franjas activas hacen falta a lo sumo 4 sprites (amanecer/día/atardecer/noche); es trabajo de arte, no de código, pero hay que encargarlo.

---

## PARTE B — Nubes con más vida: cobertura parcial + fix de la costura

### B.1 De binario a progresivo

**Nuevo concepto: `CloudCoverage` (float 0-1)**, en vez de solo "hay tormenta / no hay tormenta". 0 = cielo despejado, valores intermedios = nubes ligeras pasando, 1 = techo de tormenta completo (lo que ya existe hoy).

**Nuevo componente `Assets/Scripts/World/AmbientCloudDirector.cs`** (vive en la misma escena que `DayNightCycle`, se suscribe a sus eventos igual que hace hoy `CloudCoverSpawner`, sin referencias directas entre managers — mismo patrón arquitectónico que ya pide `CLAUDE.md` §3):

- Corrutina de fondo, solo activa cuando NO está lloviendo ya (`DayNightCycle.IsRaining == false`), que hace un paseo aleatorio lento de `CloudCoverage` entre 0 y un umbral "ligero" (p. ej. 0.4), con periodos de espera entre cambios — esto es literalmente el "se nubla un poquito, se va, vuelve" que describes.
- Si el paseo aleatorio supera un umbral alto (p. ej. 0.75) **y** toca el sorteo de lluvia de `DayNightCycle` (`rainChance`/`forceRain`, ya existente), se cede el control a `DayNightCycle.StartRain()` tal cual funciona hoy — no se duplica lógica de lluvia, `AmbientCloudDirector` solo maneja la parte "ambiental" de nubes ligeras, la tormenta de verdad la sigue llevando `DayNightCycle`.
- Expone un evento propio, `event Action<float> CloudCoverageChanged`, del que se suscribe `CloudCoverSpawner`.

**2. `CloudCoverSpawner.cs` — nuevo modo de cobertura parcial:**

Hoy `BuildCoverIfNeeded()` construye TODO el techo de golpe la primera vez que se nubla para tormenta. Para nubes ligeras hace falta menos densidad y sin el tinte de tormenta (`stormCloudColor`/`stormShadowAmount` se quedan a 0 mientras `CloudCoverage < umbralTormenta`). La forma más barata de lograrlo reutilizando el pool ya construido: en vez de animar solo el alfa de 0→1, animar también **cuántas de las nubes ya instanciadas están activas**, proporcional a `CloudCoverage` (p. ej. ordenar los renderers una vez por distancia al centro y activar/desactivar un porcentaje de la lista según la cobertura objetivo, en vez de las 300 de golpe). Así "se nubla un poco" se ve como pocas nubes sueltas, no como el techo completo con alfa bajo (que se leería como niebla, no como nubes dispersas).

### B.2 Fix de la costura negra — plan en dos pasos

1. **Repro dirigida primero.** Usar el `[ContextMenu] Activar/Desactivar techo de nubes (debug)` de `CloudCoverSpawner` para forzarlo en el editor, capturar dónde aparece la línea (una nube que solapa contra otra, o algo del propio material Quibli). Sin esto, cualquier cambio de código es un tiro a ciegas sobre un shader de terceros (Quibli).
2. **Mitigaciones candidatas, de menor a mayor invasión** (se elige una vez confirmada la causa):
   - Reducir el solape: bajar `heightJitter` y/o el límite superior de `scaleRange` (hoy 1.5), o aumentar `cellSize` relativo al tamaño máximo de nube — menos solape geométrico entre mallas vecinas.
   - Cambiar las nubes más pegadas al jugador (las que más se notan) a `CloudShaderMode.QuibliCloud2D` con `billboard = true`: al ser quads que siempre miran a cámara, no hay intersección 3D real entre dos nubes, solo alfa-blend por profundidad — elimina la clase entera de artefacto de solape de mallas 3D, a cambio de perder el volumen 3D de `Cloud3D`.
   - Si el artefacto está en el propio material/shader de Quibli (dithering de `_AlphaThreshold`), revisar si el material del demo de Quibli (`Assets/Plugins/Quibli/Demos/Clouds/Clouds Materials`) tiene una variante o ajuste de suavizado de borde que el prefab actual no esté usando.

---

## PARTE C — Cielo nocturno temático: estrellas doradas, estrellas fugaces, arcoíris

Encaja bien con el nombre del juego y la relevancia narrativa de las estrellas — y técnicamente es casi una reutilización directa de lo que ya existe en `CloudCoverSpawner`, cambiando "techo de nubes en un plano" por "domo de estrellas sobre la cabeza del jugador".

### C.1 `Assets/Scripts/World/NightSkyStarSpawner.cs` (nuevo)

Mismo patrón estructural que `CloudCoverSpawner` (rejilla + jitter + pool + fade de alfa vía `MaterialPropertyBlock`, sin `Instantiate`/`Destroy` repetidos), pero:

- Se ancla a un domo (posiciones sobre una esfera de radio fijo centrada en el jugador la primera vez, igual que el techo de nubes se ancla a un plano) en vez de a un plano horizontal.
- Se activa/desactiva por `DayNightCycle.TimeOfDayChanged` (aparece progresivamente entrando en `Noche`, se apaga entrando en `Amanecer`/`Morning`) en vez de por lluvia — evento distinto, mismo mecanismo de suscripción que ya usa `CloudCoverSpawner` con `CloudsBuildingUp`/`RainStopped`.
- Sprite/mesh pequeño con tinte dorado/cálido (en vez de blanco puro), para diferenciarlo visualmente de un cielo estrellado genérico y conectar con la estética de "sendero dorado" que ya mencionas para las nubes.
- Reutiliza el pool: construir una sola vez, ocultar con `SetActive(false)` durante el día, reactivar de noche — igual que ya hace `CloudCoverSpawner.DeactivateCover()`/reactivación en `HandleCloudsBuildingUp()`.

### C.2 `Assets/Scripts/World/ShootingStarSpawner.cs` (nuevo)

Componente pequeño e independiente: mientras `DayNightCycle.CurrentTimeOfDay == Night` y `CloudCoverage` (de la Parte B) esté por debajo de un umbral (no tiene sentido una estrella fugaz con el cielo cubierto de nubes de tormenta), cada X-Y segundos aleatorios anima un objeto (un `TrailRenderer` simple o un `ParticleSystem` de un solo disparo) cruzando el domo en línea recta con fade in/out. Sin física, sin pool complejo — es un evento raro y barato, se puede permitir `Instantiate`/`Destroy` puntual sin herramientas de pooling dedicadas (a diferencia de VFX de combate, donde `CLAUDE.md` sí exige `VfxPoolService` por la frecuencia; aquí la cadencia es de minutos, no de golpes por segundo).

### C.3 `Assets/Scripts/World/RainbowSpawner.cs` (nuevo)

Escucha `DayNightCycle.RainStopped`. Solo si `CurrentTimeOfDay` es una franja de luz suficiente (Amanecer/Día/Atardecer, no Noche — un arcoíris de noche no tiene sentido salvo que se quiera un efecto "lunar" deliberado, a decidir), instancia un arco (mesh curvo con degradado de color, o un `ParticleSystem` en forma de arco) posicionado en el lado opuesto al sol — se puede derivar directamente de `sunRotationY` de la franja actual, ya expuesto en `TimeOfDaySettings`. Fade in/out de unos 20-30s y se destruye (evento raro, no hace falta pool aquí tampoco).

---

## Archivos a crear/tocar (resumen)

| Parte | Acción | Archivo |
|---|---|---|
| A | Tocar | `Assets/Scripts/World/DayNightCycle.cs` (campos `skyboxTint/Exposure/Rotation`, instanciar `_runtimeSkybox` en `Awake`, sustituir swap de material por mutación en `ApplySettingsImmediate`/`TransitionToSettings`, reducir `timeSettings[]` a 4 entradas) |
| A | Auditar (sin tocar código necesariamente) | `Assets/Scripts/UI/TimeOfDayIndicator.cs`, `CampfireRestInteractable.cs`, `DayOnlyInspectionTrigger.cs`, `SetTimeOfDayNode.cs` y cualquier nodo en `MainNarrative.asset` que apunte a franjas retiradas del ciclo |
| A | Arte (no código) | Reducir/confirmar sprites de `TimeOfDayIndicator` a 4; elegir la textura de cubemap "genérica" definitiva para el único skybox |
| B | Crear | `Assets/Scripts/World/AmbientCloudDirector.cs` |
| B | Tocar | `Assets/Scripts/World/CloudCoverSpawner.cs` (modo de cobertura parcial, activar/desactivar % del pool) |
| B | Investigar antes de tocar | Repro visual de la costura negra (usar el `ContextMenu` de debug ya existente) |
| C | Crear | `Assets/Scripts/World/NightSkyStarSpawner.cs`, `Assets/Scripts/World/ShootingStarSpawner.cs`, `Assets/Scripts/World/RainbowSpawner.cs` |
| C | Arte | Mesh/sprite de estrella dorada pequeña, mesh o textura de arco iris, trail/partícula de estrella fugaz |

---

## Orden de implementación recomendado

1. **Parte A primero** (cielo unificado) — es la que más cambia la sensación general del juego con menos código nuevo, y conviene validarla antes de construir Partes B/C encima de un ciclo que todavía podría cambiar de forma. Empezar por la auditoría de enum (A.3) ANTES de tocar `DayNightCycle.cs`, para no descubrir a mitad de implementación que algún nodo narrativo dependía de una franja que se iba a retirar.
2. **B.2 (fix de costura) antes que B.1 (cobertura parcial).** El fix es aislado y de bajo riesgo una vez haya una captura de repro; construir la cobertura parcial encima de un shader/prefab con un artefacto visual conocido solo lo haría más difícil de diagnosticar después.
3. **Parte C al final**, como pulido — es nueva funcionalidad aislada (no modifica nada existente, solo añade componentes que escuchan eventos ya existentes de `DayNightCycle`), así que no bloquea ni depende de que A/B estén terminadas al 100%, pero tiene más sentido narrativo/visual una vez el cielo diurno y las nubes ya están en su forma final.

## Preguntas abiertas para validar antes de programar

- **A:** ¿confirmas el mapeo de franjas — Amanecer=Morning, Día=AfterNoon, Atardecer=Sunset, Noche=Night — o prefieres probar en el editor con BrightMorning/EarlyDusk/Midnight antes de decidir cuál de cada par se queda?
- **A:** ¿qué set de 6 texturas de cubemap quieres como el único skybox "genérico"? ¿Uno de los 9 ya existentes (¿cuál?) o uno nuevo/neutro?
- **B:** manda una captura o clip corto del momento en que se ve la línea negra entre nubes en cuanto puedas reproducirlo — es el paso que más acelera el fix real.
- **C:** ¿las estrellas fugaces y el arcoíris son puramente ambientales/decorativos en v1, o quieres algún gancho de gameplay/narrativa (p. ej. una quest o logro ligado a verlos)?
- **General:** ¿cuál de las tres partes quieres ver jugable primero?

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// 30 ago 2026 — dos cambios pedidos por Raúl el mismo día que el pase de la estrella protagonista
/// (ver NightSkyStarSpawner.cs), para poder probar más rápido:
///
/// 1) **Botones de prueba en el Inspector** ("testeos"): Ir a Amanecer/Día/Atardecer/Noche, Lluvia
/// iniciar/detener, Niebla iniciar/detener, y forzar un sorteo de clima ya — todos como
/// <c>[ContextMenu]</c> con el prefijo "Testeo:" (ver el final de la clase). Los de periodo usan
/// <c>immediate: true</c> a propósito, sin esperar la transición suave, para poder saltar
/// directamente a noche y ver el cielo estrellado sin esperar al ciclo automático.
///
/// 2) **El clima (lluvia/niebla) deja de estar atado al periodo del día**. Petición textual: *"la
/// lluvia esta metida como si fuera parte del dia siempre llueve y luego se hace de noche y eso no
/// es esa [...] deben ocurrir en cualquier momento. Los estados del dia son: amanecer, de dia,
/// atardecer, de noche"*. Antes, <c>ApplyTimeOfDay</c> sorteaba lluvia (<c>forceRain</c>/
/// <c>rainChance</c>) y niebla ocasional (<c>fogChance</c>) UNA SOLA VEZ, en el instante exacto de
/// entrar en cada periodo — así que el clima solo podía "decidirse" en esos 4 instantes concretos
/// del ciclo, dando la sensación de estar pegado al ciclo día/noche en vez de ser un sistema aparte.
/// Se ha quitado ese acoplamiento de raíz: <c>forceRain</c>/<c>rainChance</c>/<c>fogChance</c>
/// desaparecen de <see cref="TimeOfDaySettings"/> (ya no existen "por periodo"), y en su lugar hay un
/// temporizador de clima completamente independiente (<see cref="WeatherRollLoop"/> +
/// <see cref="TryRollWeather"/>, ver Header("Clima — sorteo independiente del periodo")): cada
/// <see cref="weatherCheckIntervalRange"/> segundos (aleatorio dentro del rango, para que no sea
/// predecible) se sortea si arranca lluvia (<see cref="rainChancePerCheck"/>) o niebla
/// (<see cref="mistChancePerCheck"/>), sin importar en qué periodo del día se esté ni si se acaba de
/// entrar en uno o se lleva ya un rato — el clima puede empezar, seguir activo, o terminar a mitad
/// de cualquier periodo. <c>rainLastsWholePeriod</c>/<c>mistLastsWholePeriod</c> también desaparecen
/// (ya no tiene sentido "durar todo el periodo" si el clima no depende del periodo): la lluvia ahora
/// siempre sortea su propia duración dentro de <see cref="rainDurationRange"/> (igual que la niebla
/// ya hacía con <c>mistDurationRange</c>). <c>ApplyTimeOfDay</c> ya no toca el clima en absoluto —
/// si ya estaba lloviendo/con niebla al cambiar de periodo, sigue exactamente igual.
///
/// 30 ago 2026 (mismo día, pasada siguiente) — "añade lo que se quedó fuera de esta tanda": viento y
/// tormenta, inicialmente dejados fuera por falta de assets/VFX (ver más abajo por qué). Añadidos con
/// el mismo patrón que lluvia/niebla, degradando con elegancia donde faltan assets en vez de
/// bloquearse por ellos:
/// - **Viento** (<see cref="IsWindy"/>, <see cref="StartWind"/>/<see cref="StopWind"/>,
///   <see cref="windChancePerCheck"/>/<see cref="windDurationRange"/>): mismo patrón que la niebla
///   ocasional. <see cref="windPrefab"/> es OPCIONAL (null-safe, igual que <c>mistPrefab</c>) — sin
///   asignar, el viento sigue funcionando como evento lógico completo (flag, eventos, SFX en loop)
///   pero sin ningún VFX de hojas/polvo, porque este proyecto no tiene ese prefab todavía. Asignarlo
///   en cuanto exista arte para ello no necesita ningún cambio de código.
/// - **Tormenta** (<see cref="IsThunderstorm"/>, <see cref="StartThunderstorm"/>/
///   <see cref="StopThunderstorm"/>): NO es un sistema de lluvia paralelo — una tormenta ES lluvia
///   normal (reutiliza <see cref="StartRain"/> tal cual) más una capa de rayos periódicos
///   (<see cref="LightningLoop"/>/<see cref="FlashLightningRoutine"/>) que SÍ es un efecto visual
///   real que funciona ya, sin ningún asset nuevo: cada rayo crea una Light direccional temporal a
///   <see cref="thunderstormFlashIntensity"/> durante <see cref="thunderstormFlashDuration"/>
///   segundos y la destruye — DELIBERADAMENTE una luz nueva y no un cambio directo sobre
///   <c>directionalLight.intensity</c>, porque esa propiedad ya se recalcula cada frame en
///   <c>LateUpdate</c> a partir de su propio valor anterior mientras llueve (oscurecimiento por
///   lluvia), así que escribir el destello ahí se pelearía con ese cálculo frame a frame. El trueno
///   (<see cref="thunderstormThunderSfxKey"/>) suena con un retraso aleatorio tras el destello
///   (<see cref="thunderstormThunderDelayRange"/>), como en la realidad.
///
/// Alcance que sigue sin cubrir esta pasada: los VFX de partículas en sí (hojas/polvo para el
/// viento) — el sistema lógico completo ya funciona y se oye/comporta correctamente, pero visualmente
/// el viento no mueve nada todavía porque no hay ningún prefab de partículas en el proyecto para él;
/// asignar <see cref="windPrefab"/> cuando exista ese arte es todo lo que hace falta.
///
/// Además, revisando este script para hacer este cambio se encontró y corrigió una regresión
/// independiente y bastante más vieja: el comentario del propio código (ver más abajo, "13 ago
/// 2026") ya decía que <see cref="timeSettings"/> se había reducido de 7 franjas a 4
/// (Amanecer/Día/Atardecer/Noche) a petición de Raúl — pero las 4 escenas que usan este componente
/// (MainWorld, CandyLand, Sendero, PlayerTest) seguían con el array de 7 franjas ANTIGUO serializado
/// tal cual desde antes de ese cambio (BrightMorning/EarlyDusk/Midnight incluidas), porque Unity no
/// vuelve a sincronizar un array ya serializado en la escena con un array por defecto distinto del
/// código — el mismo tipo de bug ya documentado varias veces en este proyecto (ver
/// `contexto-proyecto.md` y el comentario de clase de NightSkyStarSpawner.cs). Es decir: el ciclo
/// del día llevaba 17 días pasando por 7 franjas en el juego real de Raúl, no por las 4 que el
/// código decía desde el 13 de agosto — probablemente parte de por qué el clima se sentía errático
/// y "pegado" a un ciclo más largo y raro de lo esperado. Corregido en las 4 escenas: se han quitado
/// las 3 franjas obsoletas (BrightMorning/EarlyDusk/Midnight) del array serializado, dejando solo
/// las 4 correctas con los valores que YA tenían en cada escena (ninguna tonalidad de luz/niebla
/// tocada, solo se han quitado las franjas de más).
/// </summary>
[DisallowMultipleComponent]
// 30 ago 2026 — debe ejecutar su Awake() ANTES que AmbientZone (orden no garantizado entre
// scripts sin esto). AmbientZone.CaptureDefaults() guarda RenderSettings.fog tal cual esté en
// ese instante para restaurarlo al salir de una zona — si captura el valor ANTES de que este
// Awake ponga RenderSettings.fog = controlFog, se queda con el "false" horneado en la escena, y
// la niebla (que aquí depende solo de fogDensity, sin mistPrefab) deja de verse en cuanto el
// jugador sale de cualquier interior una vez. Ver comentario de clase.
[DefaultExecutionOrder(-100)]
public class DayNightCycle : MonoBehaviour
{
    public enum TimeOfDay
    {
        [InspectorName("Día (Tarde)")] AfterNoon,
        [InspectorName("Mañana radiante (no usada)")] BrightMorning,
        [InspectorName("Nublado (no usado)")] Cloudy,
        [InspectorName("Media tarde (no usada)")] EarlyDusk,
        [InspectorName("Halo (no usado)")] HaloSky,
        [InspectorName("Medianoche (no usada)")] Midnight,
        [InspectorName("Amanecer")] Morning,
        [InspectorName("Noche")] Night,
        [InspectorName("Atardecer")] Sunset
    }

    [System.Serializable]
    public class TimeOfDaySettings
    {
        public TimeOfDay timeOfDay;

        [Tooltip("LEGACY — ya no se usa en el ciclo (ver más abajo skyboxTint/skyboxIntensity/skyboxExponent/skyboxDirectionYaw/skyboxDirectionPitch y DayNightCycle.sharedSkyboxMaterial). Se deja el campo para no perder la referencia histórica, pero el ciclo actual pinta un único material de skybox en vez de cambiar de asset.")]
        public Material skybox;

        [Header("Skybox único (shader Quibli/Skybox — degradado por ángulo, tintado)")]
        [Tooltip("Color por el que se multiplica el degradado del único skybox del juego (_Tint del shader Quibli/Skybox). Con el material recomendado (City_Skybox, degradado azul claro→azul) esto es lo que pinta amanecer/atardecer cálidos y noche oscura sin cambiar de material ni de textura.")]
        public Color skyboxTint = Color.white;
        [Tooltip("Brillo del cielo en este periodo (_Intensity del shader Quibli/Skybox). Actúa como la 'exposición': bajo de noche, alto de día.")]
        [Range(0f, 5f)] public float skyboxIntensity = 1f;
        [Tooltip("Dureza del degradado (_Exponent). Alto = el color queda concentrado cerca de la dirección marcada por skyboxDirectionYaw/Pitch; bajo = se reparte más uniforme por todo el cielo.")]
        [Range(0f, 5f)] public float skyboxExponent = 1f;
        [Tooltip("Eje horizontal del degradado (_DirectionYaw, 0-1 en el shader). Punto de partida razonable: sunRotationY / 360, luego ajustar a ojo — no hace falta que coincida exactamente con el sol.")]
        [Range(0f, 1f)] public float skyboxDirectionYaw = 0f;
        [Tooltip("Eje vertical del degradado (_DirectionPitch, 0-1 en el shader). Punto de partida razonable: sunRotationX / 180, luego ajustar a ojo.")]
        [Range(0f, 1f)] public float skyboxDirectionPitch = 0f;

        [Header("Luz direccional")]
        public Color lightColor = Color.white;
        [Range(0f, 2f)] public float lightIntensity = 1f;
        [Range(0f, 360f)] public float sunRotationX = 50f;
        [Range(0f, 360f)] public float sunRotationY = 170f;

        [Header("Luz ambiental")]
        public Color ambientColor = new Color(0.2f, 0.2f, 0.25f);
        [Range(0f, 2f)] public float ambientIntensity = 1f;

        [Header("Niebla")]
        public Color fogColor = new Color(0.5f, 0.5f, 0.5f);
        [Range(0f, 0.1f)] public float fogDensity = 0.01f;

        [Header("Ciclo")]
        [Tooltip("Duración de este periodo en segundos. El clima (lluvia/niebla) ya NO depende de esto — ver Header(\"Clima — sorteo independiente del periodo\") más abajo en la clase: el clima corre con su propio temporizador y puede empezar o seguir activo en cualquier periodo, incluido a mitad de uno.")]
        public float duration = 60f;
    }

    // 13 ago 2026 — Reducido de 7 franjas activas a 4 (Amanecer/Día/Atardecer/Noche), a petición
    // directa: "quiero amanecer, dia, atardecer, noche y lluvia FIN". Deliberadamente NO se toca el
    // enum TimeOfDay (ver comentario de la clase) para no romper CampfireRestInteractable.cs,
    // DayOnlyInspectionTrigger.cs ni TimeIconEntry[] de TimeOfDayIndicator.cs, que referencian
    // valores concretos del enum — auditado antes de este cambio, ninguno usa BrightMorning/
    // EarlyDusk/Midnight/Cloudy/HaloSky, así que quitarlas del ciclo automático no les afecta.
    // Mapeo: Amanecer→Morning, Día→AfterNoon, Atardecer→Sunset, Noche→Night (ver TDD.md §16 A.3
    // para la justificación completa de por qué estos 4 y no sus alternativas).
    //
    // Los deltas de lightIntensity/ambientIntensity entre franjas se han suavizado a propósito
    // respecto a las 7 franjas originales (p.ej. Night pasaba de 1.3 a 0.25 de golpe): un cambio de
    // luz tan grande afecta mucho a la dureza/dirección de las sombras y se notaba como un salto, no
    // como una transición. Con transitionDuration también subido (10s → 16s) el cambio se lee como
    // gradual de verdad.
    [Header("Periodos del día")]
    [SerializeField] private TimeOfDaySettings[] timeSettings = new TimeOfDaySettings[]
    {
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Morning,
            duration = 90f,
            lightColor = new Color(1f, 0.88f, 0.72f), lightIntensity = 0.95f,
            sunRotationX = 15f, sunRotationY = 90f,
            ambientColor = new Color(0.3f, 0.31f, 0.4f), ambientIntensity = 0.85f,
            fogColor = new Color(0.7f, 0.79f, 0.9f), fogDensity = 0.007f,
            skyboxTint = new Color(1f, 0.83f, 0.72f), skyboxIntensity = 0.85f, skyboxExponent = 1f,
            skyboxDirectionYaw = 0.25f, skyboxDirectionPitch = 0.08f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.AfterNoon,
            duration = 220f,
            lightColor = new Color(1f, 0.97f, 0.88f), lightIntensity = 1.2f,
            sunRotationX = 60f, sunRotationY = 165f,
            ambientColor = new Color(0.38f, 0.37f, 0.34f), ambientIntensity = 1f,
            fogColor = new Color(0.85f, 0.87f, 0.88f), fogDensity = 0.0045f,
            skyboxTint = new Color(1f, 0.99f, 0.97f), skyboxIntensity = 1.15f, skyboxExponent = 1f,
            skyboxDirectionYaw = 0.46f, skyboxDirectionPitch = 0.33f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Sunset,
            duration = 65f,
            lightColor = new Color(1f, 0.6f, 0.28f), lightIntensity = 0.85f,
            sunRotationX = 95f, sunRotationY = 245f,
            ambientColor = new Color(0.35f, 0.22f, 0.14f), ambientIntensity = 0.78f,
            fogColor = new Color(0.88f, 0.58f, 0.34f), fogDensity = 0.011f,
            skyboxTint = new Color(1f, 0.58f, 0.4f), skyboxIntensity = 0.9f, skyboxExponent = 1.1f,
            skyboxDirectionYaw = 0.68f, skyboxDirectionPitch = 0.53f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Night,
            duration = 150f,
            lightColor = new Color(0.5f, 0.6f, 0.95f), lightIntensity = 0.4f,
            sunRotationX = 150f, sunRotationY = 300f,
            ambientColor = new Color(0.1f, 0.1f, 0.2f), ambientIntensity = 0.55f,
            fogColor = new Color(0.09f, 0.09f, 0.19f), fogDensity = 0.015f,
            skyboxTint = new Color(0.22f, 0.26f, 0.5f), skyboxIntensity = 0.35f, skyboxExponent = 1f,
            skyboxDirectionYaw = 0.83f, skyboxDirectionPitch = 0.83f
        }
    };

    [Header("Skybox único")]
    [Tooltip("El ÚNICO material de skybox del juego. Recomendado: Assets/Plugins/Quibli/Demos/City/Materials/City_Skybox.mat (shader Quibli/Skybox, degradado azul claro→azul, sin sol/nubes pintados — 'cielo liso'). Se instancia una copia en runtime (_runtimeSkybox) para poder animar tint/intensity/exponent/direction por franja sin ensuciar este asset compartido. Hay que arrastrarlo aquí a mano en cada escena que use DayNightCycle (MainWorld, Sendero, CandyLand, PlayerTest...); esto no se puede cablear desde fuera del Editor.")]
    [SerializeField] private Material sharedSkyboxMaterial;

    [Header("Luz direccional")]
    [SerializeField] private Light directionalLight;

    [Header("Transiciones")]
    [Tooltip("Duración de la transición entre periodos del día en segundos.")]
    [SerializeField] private float transitionDuration = 16f;
    [Tooltip("Usar transiciones suaves entre periodos (requiere más recursos).")]
    [SerializeField] private bool useSmoothTransitions = true;

    [Header("Control de entorno")]
    [Tooltip("Si es true, el ciclo controlará la luz ambiental global.")]
    [SerializeField] private bool controlAmbientLight = true;
    [Tooltip("Si es true, el ciclo controlará la niebla global.")]
    [SerializeField] private bool controlFog = true;

    [Header("Clima - Lluvia")]
    [Tooltip("Prefab del sistema de partículas de lluvia.")]
    [SerializeField] private GameObject rainPrefab;
    [Tooltip("Duración aleatoria (min, max) en segundos de un evento de lluvia sorteado por el temporizador de clima independiente (ver Header(\"Clima — sorteo independiente del periodo\") más abajo). No se usa si la lluvia se arranca a mano con una duración explícita (StartRain(duration), narrativa, tests).")]
    [SerializeField] private Vector2 rainDurationRange = new Vector2(40f, 100f);
    [Tooltip("Segundos que tardan en desaparecer las partículas al detener la lluvia.")]
    [SerializeField] private float rainFadeOutTime = 3f;

    [Header("Clima - Nubosidad previa a la lluvia")]
    [Tooltip("OPCIONAL. Skybox de cielo nublado/tormenta que se muestra mientras el cielo se cubre de nubes, antes de que empiece a llover. Con un único skybox persistente (sharedSkyboxMaterial) esto ya NO hace falta para tapar huecos — se deja null y es CloudCoverSpawner (nubes 3D reales) quien cubre el cielo. Solo asigna esto si además quieres tintar el fondo lejano de otra forma durante la tormenta.")]
    [SerializeField] private Material stormSkybox;

    [Header("Clima - Oscurecimiento por lluvia")]
    [Tooltip("Multiplicador de la intensidad de la luz direccional mientras llueve a tope (1 = sin cambio).")]
    [SerializeField, Range(0f, 1f)] private float rainLightIntensityMultiplier = 0.55f;
    [Tooltip("Multiplicador de la densidad de niebla mientras llueve a tope (1 = sin cambio, más alto = más densa).")]
    [SerializeField, Range(1f, 6f)] private float rainFogDensityMultiplier = 2.5f;
    [Tooltip("Color hacia el que se tiñe la niebla mientras llueve (mezclado según rainFogColorBlend).")]
    [SerializeField] private Color rainFogColorTint = new Color(0.45f, 0.47f, 0.5f);
    [Range(0f, 1f)] [SerializeField] private float rainFogColorBlend = 0.5f;
    [Tooltip("Suelo ABSOLUTO de intensidad de la luz direccional mientras llueve a tope. Sin esto, rainLightIntensityMultiplier se aplica sobre la intensidad que ya tenga el periodo actual, así que un periodo ya oscuro (Night) puede quedarse en negro casi total. Con este suelo, la luz nunca baja de este valor por mucho que se multiplique.")]
    [SerializeField, Range(0f, 1f)] private float rainMinLightIntensity = 0.28f;
    [Tooltip("Segundos que tarda el cielo en nublarse (oscurecer + espesar niebla + cubrirse de nubes 3D) ANTES de que arranque la lluvia, y lo que tarda en despejarse otra vez al terminar. La lluvia no empieza a caer hasta que termina esta transición.")]
    [SerializeField] private float rainDarkenTransitionDuration = 4f;

    [Header("Clima - Niebla ocasional")]
    [Tooltip("Prefab opcional de niebla volumétrica (partículas) para el evento de niebla ocasional. Si es null, solo se espesa la niebla global (RenderSettings.fog), sin partículas.")]
    [SerializeField] private GameObject mistPrefab;
    [Tooltip("Duración aleatoria (min, max) en segundos de un evento de niebla ocasional.")]
    [SerializeField] private Vector2 mistDurationRange = new Vector2(20f, 45f);
    [Tooltip("Segundos que tardan en desaparecer las partículas al detener la niebla ocasional.")]
    [SerializeField] private float mistFadeOutTime = 4f;
    [Tooltip("Multiplicador de la densidad de niebla mientras está activa la niebla ocasional a tope (1 = sin cambio).")]
    [SerializeField, Range(1f, 8f)] private float mistFogDensityMultiplier = 4f;
    [Tooltip("Segundos que tarda en espesar/disipar la niebla ocasional.")]
    [SerializeField] private float mistTransitionDuration = 8f;

    [Header("Clima - Viento (30 ago 2026)")]
    [Tooltip("Prefab OPCIONAL de partículas de viento (hojas/polvo en el aire). Si es null, el viento sigue funcionando igual como evento lógico (IsWindy, eventos, SFX en loop) pero sin ningún VFX — mismo patrón de degradado que ya usa mistPrefab. Este proyecto no tiene todavía ningún prefab de este tipo; asignar aquí en cuanto exista arte para ello, no hace falta tocar código.")]
    [SerializeField] private GameObject windPrefab;
    [Tooltip("Duración aleatoria (min, max) en segundos de un evento de viento.")]
    [SerializeField] private Vector2 windDurationRange = new Vector2(30f, 90f);
    [Tooltip("Segundos que tardan en desaparecer las partículas (si hay windPrefab asignado) al detener el viento.")]
    [SerializeField] private float windFadeOutTime = 3f;

    [Header("Clima - Tormenta / Rayos (30 ago 2026)")]
    [Tooltip("Una tormenta es lluvia normal (mismas partículas/oscurecimiento/niebla de siempre, ver StartThunderstorm) más rayos periódicos. Rango (min, max) en segundos entre un rayo y el siguiente mientras dura la tormenta.")]
    [SerializeField] private Vector2 thunderstormLightningIntervalRange = new Vector2(8f, 22f);
    [Tooltip("Intensidad (Light.intensity) del destello de cada rayo. Es una luz direccional NUEVA y temporal creada solo para el destello (ver FlashLightningRoutine), no un multiplicador sobre la luz principal — así no interfiere con el oscurecimiento por lluvia (rainLightIntensityMultiplier), que ya recalcula la luz principal cada frame en LateUpdate y se pelearía con cualquier cambio directo sobre directionalLight.intensity.")]
    [SerializeField] private float thunderstormFlashIntensity = 3.5f;
    [Tooltip("Segundos que dura visible cada destello de rayo.")]
    [SerializeField] private float thunderstormFlashDuration = 0.12f;
    [Tooltip("Retraso (min, max) en segundos entre el destello del rayo y el trueno — la luz llega antes que el sonido, igual que en la realidad. Solo afecta a CUÁNDO suena thunderstormThunderSfxKey, no al destello en sí.")]
    [SerializeField] private Vector2 thunderstormThunderDelayRange = new Vector2(0.3f, 1.8f);
    [Tooltip("Altura (unidades de mundo) desde la que 'cae' el rayo visible, por encima de la posición del jugador. No depende de ningún terreno/collider real (no se hace Raycast): el extremo inferior del rayo se dibuja a la altura actual del jugador, como aproximación razonable del suelo cercano.")]
    [SerializeField] private float thunderstormBoltHeight = 220f;
    [Tooltip("Distancia horizontal (min, max) a la que 'cae' cada rayo respecto al jugador, en una dirección aleatoria — para que se vea caer en el paisaje, no encima del jugador ni siempre en el mismo sitio.")]
    [SerializeField] private Vector2 thunderstormBoltDistanceRange = new Vector2(18f, 55f);
    [Tooltip("Grosor visual del rayo (LineRenderer.widthMultiplier).")]
    [SerializeField] private float thunderstormBoltWidth = 1.4f;
    [Tooltip("Color del rayo — blanco azulado brillante. Canales por encima de 1 a propósito (con Bloom activo aporta el brillo extra del propio rayo), igual que otros elementos emisivos de este proyecto (p.ej. NightSkyStarSpawner.starColor).")]
    [SerializeField] private Color thunderstormBoltColor = new Color(2.2f, 2.3f, 2.7f);
    [Tooltip("Número de segmentos del zigzag del rayo — más segmentos = forma más quebrada/detallada.")]
    [SerializeField, Range(2, 16)] private int thunderstormBoltSegments = 7;
    [Tooltip("Desplazamiento horizontal aleatorio máximo (unidades de mundo) de cada segmento respecto a la línea recta cielo-suelo, para la silueta quebrada característica de un rayo.")]
    [SerializeField] private float thunderstormBoltJitter = 7f;
    [Tooltip("Cuánto tiempo (segundos) queda visible el trazo del rayo en sí, independiente de thunderstormFlashDuration (que solo controla el destello de luz ambiental). 0.12s resultaba casi imperceptible — un solo parpadeo de un frame o dos.")]
    [SerializeField] private float thunderstormBoltVisibleDuration = 0.3f;

    [Header("Clima — sorteo independiente del periodo (30 ago 2026, ver comentario de clase)")]
    [Tooltip("Cada cuánto se vuelve a intentar un cambio de clima (sorteo de lluvia/niebla/viento/tormenta), en segundos — se elige un valor aleatorio dentro de este rango cada vez, para que no sea predecible. Corre en un temporizador propio, totalmente independiente de a qué periodo del día (amanecer/día/atardecer/noche) corresponda ese instante — el clima puede empezar, seguir o terminar en cualquier periodo, incluso a mitad de uno.")]
    [SerializeField] private Vector2 weatherCheckIntervalRange = new Vector2(45f, 110f);
    [Tooltip("Probabilidad (0-1) de que arranque una TORMENTA en cada sorteo (lluvia + rayos) — se comprueba ANTES que rainChancePerCheck, así que si sale, no se vuelve a tirar el dado para lluvia normal ese mismo sorteo. Deliberadamente baja: las tormentas deben ser un evento poco frecuente y notable, no una lluvia más.")]
    [SerializeField, Range(0f, 1f)] private float thunderstormChancePerCheck = 0.05f;
    [Tooltip("Probabilidad (0-1) de que arranque a llover (lluvia normal, sin rayos) en cada sorteo — solo se comprueba si el sorteo de tormenta de ese mismo intento no salió.")]
    [SerializeField, Range(0f, 1f)] private float rainChancePerCheck = 0.18f;
    [Tooltip("Probabilidad (0-1) de que arranque viento en cada sorteo — solo se comprueba si ni tormenta ni lluvia salieron en ese mismo intento.")]
    [SerializeField, Range(0f, 1f)] private float windChancePerCheck = 0.15f;
    [Tooltip("Probabilidad (0-1) de que arranque niebla ocasional en cada sorteo — la última en probarse (solo si tormenta/lluvia/viento no salieron). No se solapan lluvia y niebla ocasional a propósito (la lluvia ya espesa la niebla por su cuenta).")]
    [SerializeField, Range(0f, 1f)] private float mistChancePerCheck = 0.12f;

    [Header("Clima - Audio (grafo sonoro)")]
    [Tooltip("Event Key del AudioGraphProfile (lista 'Event Sfx') que se reproduce cuando el cielo empieza a nublarse, ANTES de que arranque la lluvia. Déjalo vacío para no reproducir nada.")]
    [SerializeField] private string cloudsBuildingUpSfxKey;
    [Tooltip("Event Key del AudioGraphProfile del SFX/ambiente de lluvia. Se reproduce en LOOP (vía AudioService.PlayLoopingSFX) desde que arranca la lluvia de verdad hasta que para, no como one-shot: así no importa si el clip asignado es más largo o más corto que la lluvia real.")]
    [SerializeField] private string rainStartedSfxKey;
    [Tooltip("Event Key opcional del AudioGraphProfile para un one-shot adicional cuando para de llover (p.ej. un cue corto de viento amainando). El loop de rainStartedSfxKey se detiene siempre, tenga o no clave este campo.")]
    [SerializeField] private string rainStoppedSfxKey;
    /// <summary>Clave interna usada en AudioService.PlayLoopingSFX/StopLoopingSFX para el loop de ambiente de lluvia.</summary>
    const string RainWeatherSfxLoopId = "Weather_Rain";
    [Tooltip("Event Key del AudioGraphProfile que se reproduce al empezar un evento de niebla ocasional.")]
    [SerializeField] private string mistStartedSfxKey;
    [Tooltip("Event Key del AudioGraphProfile que se reproduce al disiparse la niebla ocasional.")]
    [SerializeField] private string mistStoppedSfxKey;
    [Tooltip("Event Key del AudioGraphProfile del SFX/ambiente de viento. Igual que rainStartedSfxKey, se reproduce en LOOP (AudioService.PlayLoopingSFX) mientras IsWindy es true, no como one-shot.")]
    [SerializeField] private string windStartedSfxKey;
    [Tooltip("Event Key opcional del AudioGraphProfile para un one-shot al terminar el viento. El loop de windStartedSfxKey se detiene siempre, tenga o no clave este campo.")]
    [SerializeField] private string windStoppedSfxKey;
    [Tooltip("Event Key del AudioGraphProfile del trueno — one-shot, se reproduce con retraso tras cada destello de rayo (ver thunderstormThunderDelayRange).")]
    [SerializeField] private string thunderstormThunderSfxKey;

    [Header("Ciclo")]
    [Tooltip("Si es falso, no avanza automáticamente el ciclo.")]
    [SerializeField] private bool autoAdvance = true;
    [Tooltip("Índice del periodo inicial (0 = primero en la lista).")]
    [SerializeField] private int startingTimeIndex = 0;

    [Header("Eventos")]
    [SerializeField] private UnityEvent<TimeOfDay> onTimeOfDayChanged;
    [SerializeField] private UnityEvent onCloudsBuildingUp;
    [SerializeField] private UnityEvent onRainStarted;
    [SerializeField] private UnityEvent onRainStopped;
    [SerializeField] private UnityEvent onMistStarted;
    [SerializeField] private UnityEvent onMistStopped;
    [SerializeField] private UnityEvent onWindStarted;
    [SerializeField] private UnityEvent onWindStopped;
    [SerializeField] private UnityEvent onThunderstormStarted;
    [SerializeField] private UnityEvent onThunderstormStopped;

    public event Action<TimeOfDay> TimeOfDayChanged;
    /// <summary>Se dispara al empezar a nublarse el cielo, ANTES de que arranque la lluvia (también con tormenta, que arranca como una lluvia normal). Útil para SFX de viento/truenos.</summary>
    public event Action CloudsBuildingUp;
    public event Action RainStarted;
    public event Action RainStopped;
    public event Action MistStarted;
    public event Action MistStopped;
    public event Action WindStarted;
    public event Action WindStopped;
    /// <summary>Se dispara al arrancar/terminar la capa de tormenta (rayos) por encima de la lluvia — ver StartThunderstorm. No confundir con RainStarted/RainStopped, que también se disparan (una tormenta ES lluvia, más rayos).</summary>
    public event Action ThunderstormStarted;
    public event Action ThunderstormStopped;

    public TimeOfDay CurrentTimeOfDay { get; private set; }
    public bool IsRaining { get; private set; }
    public bool IsMisty { get; private set; }
    public bool IsWindy { get; private set; }
    /// <summary>True mientras la capa de rayos está activa (ver StartThunderstorm). Independiente de IsRaining a nivel de flag, aunque en la práctica una tormenta siempre implica IsRaining == true a la vez (StartThunderstorm arranca la lluvia por debajo).</summary>
    public bool IsThunderstorm { get; private set; }
    public float TimeProgress => _currentDuration > 0 ? Mathf.Clamp01(_timeElapsed / _currentDuration) : 1f;

    /// <summary>Segundos que tarda el cielo en nublarse/despejarse. Expuesto para que sistemas
    /// externos (p.ej. un spawner de nubes 3D) sincronicen su propio fundido con este tiempo.</summary>
    public float RainDarkenTransitionDuration => rainDarkenTransitionDuration;

    private int _currentIndex;
    private float _timeElapsed;
    private float _currentDuration;
    private bool _isTransitioning;

    // Instancia propia de sharedSkyboxMaterial (ver Awake/OnDestroy). RenderSettings.skybox no
    // auto-instancia el material al asignarlo (a diferencia de renderer.material): sin esta copia,
    // animar tint/intensity/exponent/direction en Play Mode ensuciaría el .mat compartido de verdad
    // en el Editor.
    private Material _runtimeSkybox;
    private static readonly int SkyboxTintId = Shader.PropertyToID("_Tint");
    private static readonly int SkyboxIntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int SkyboxExponentId = Shader.PropertyToID("_Exponent");
    private static readonly int SkyboxDirectionYawId = Shader.PropertyToID("_DirectionYaw");
    private static readonly int SkyboxDirectionPitchId = Shader.PropertyToID("_DirectionPitch");

    private GameObject _activeRainInstance;
    private Coroutine _rainCoroutine;
    private Coroutine _transitionCoroutine;
    private Coroutine _rainFadeCoroutine;
    private Coroutine _rainDarkenCoroutine;
    private float _rainDarkenAmount;

    // True mientras el cielo se está nublando (nubes 3D de CloudCoverSpawner + oscurecimiento) pero
    // la lluvia todavía no ha empezado a caer (IsRaining sigue en false hasta que termina la transición).
    private bool _isCloudBuildingUp;
    // Skybox que había activo justo antes de aplicar stormSkybox, para poder restaurarlo si ninguna
    // transición de periodo lo ha pisado mientras tanto. Solo se usa si stormSkybox está asignado.
    private Material _preStormSkybox;

    // Cámara principal cacheada en Awake (nunca Camera.main en Update/LateUpdate). Se usa como red
    // de seguridad SOLO cuando no hay un único skybox persistente (_runtimeSkybox == null, es decir,
    // sharedSkyboxMaterial sin asignar): en ese caso el skybox despejado seguiría viéndose en el
    // horizonte más allá del alcance de CloudCoverSpawner. Con _runtimeSkybox activo esto no hace
    // falta — el cielo despejado por encima/alrededor del techo de nubes es el comportamiento
    // deseado (ver tooltip de stormSkybox), no un hueco que tapar.
    private Camera _mainCamera;
    private CameraClearFlags _preStormClearFlags;
    private Color _preStormBackgroundColor;
    private bool _cameraOverrideActive;

    private GameObject _activeMistInstance;
    private Coroutine _mistCoroutine;
    private Coroutine _mistFadeCoroutine;
    private Coroutine _mistDarkenCoroutine;
    private float _mistAmount;

    private GameObject _activeWindInstance;
    private Coroutine _windCoroutine;
    private Coroutine _windFadeCoroutine;
    const string WindWeatherSfxLoopId = "Weather_Wind";

    // La tormenta reutiliza StartRain/StopRain por debajo (mismas partículas/oscurecimiento/niebla
    // de siempre) — _thunderstormCoroutine solo controla CUÁNTO dura la capa extra de rayos,
    // _lightningCoroutine es el bucle que va disparando cada rayo individual mientras tanto.
    private Coroutine _thunderstormCoroutine;
    private Coroutine _lightningCoroutine;
    // Material compartido del trazo visible del rayo (construido en Awake, liberado en OnDestroy) —
    // ver SpawnLightningBolt.
    private Material _lightningBoltMaterial;
    // 30 ago 2026 — true mientras el jugador está dentro de una AmbientZone con forcesMist activo
    // (ver SetZoneMistOverride). Mientras esté a true, TryRollWeather deja de sortear NADA nuevo
    // (ni lluvia, ni tormenta, ni viento) y solo mantiene la niebla — a petición de Raúl, "la
    // niebla manda, bloquea el resto" mientras estás en una zona así. Si ya estaba lloviendo al
    // entrar en la zona, esa lluvia sigue igual (no se le pisa) hasta que termine sola.
    private bool _zoneMistForced;

    // Niebla "base" del periodo actual (sin el oscurecimiento de lluvia/niebla ocasional aplicado).
    // LateUpdate() recalcula el fog final SIEMPRE a partir de esta base, en vez de multiplicar el
    // RenderSettings.fogDensity ya mutado del frame anterior — eso causaba un crecimiento exponencial
    // frame a frame (density *= multiplier cada frame) que saturaba la pantalla entera de niebla en
    // cuestión de segundos.
    private float _baseFogDensity;
    private Color _baseFogColor;

    // Suprime la lluvia y la niebla VISUALMENTE mientras el jugador está en un interior
    // (AnchorEnvironment.isInterior), sin tocar el ciclo lógico (IsRaining/IsMisty, temporizadores)
    // para que al salir se reanuden si el clima sigue activo.
    // OJO: esto solo se actualiza vía OnInteriorEntered/OnInteriorExited, que EnvironmentController
    // dispara únicamente desde ApplyInterior/ApplyExterior (el flujo "real" de entrar/salir andando).
    // NO se actualiza durante un override cinemático (BeginCinematicOverride + ApplyInteriorForCinematic,
    // ver CinematicSequencerBase/SimpleCinematicDirector), porque ese flujo no toca _mode a propósito.
    // Por eso IsSkyboxLockedByEnvironment() de abajo comprueba también IsCinematicOverrideActive.
    private bool _outdoorWeatherSuppressedIndoors;

    // Para detectar cuándo un override cinemático termina y poder re-aplicar el skybox correcto
    // (periodo actual o tormenta) que se haya quedado pendiente mientras estaba bloqueado.
    private bool _wasCinematicOverrideActive;

    // Para detectar el inicio de un minijuego (TagMinigameController.IsAnyMinigameActive) y cortar
    // la lluvia que ya estuviera cayendo en ese instante. StartRain() ya bloquea que arranque lluvia
    // NUEVA mientras haya un minijuego activo, pero eso no cubre el caso de que empezara a llover
    // justo antes de que el jugador entrara en el minijuego.
    private bool _wasMinigameActive;

    void Awake()
    {
        if (timeSettings == null || timeSettings.Length == 0)
        {
            Debug.LogError("[DayNightCycle] No hay periodos configurados en timeSettings.");
            enabled = false;
            return;
        }

        _currentIndex = Mathf.Clamp(startingTimeIndex, 0, timeSettings.Length - 1);

        _mainCamera = Camera.main;

        if (sharedSkyboxMaterial != null)
        {
            _runtimeSkybox = new Material(sharedSkyboxMaterial);
            RenderSettings.skybox = _runtimeSkybox;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning("[DayNightCycle] No hay sharedSkyboxMaterial asignado (recomendado: Assets/Plugins/Quibli/Demos/City/Materials/City_Skybox.mat); el ciclo día/noche no podrá animar el skybox único y se queda con el que ya hubiera en RenderSettings.skybox.");
        }
#endif

        if (controlAmbientLight)
            RenderSettings.ambientMode = AmbientMode.Flat;

        // Si controlFog está desactivado, forzamos el fog a apagado en vez de dejarlo tal cual
        // estuviera horneado en la escena — así "desactivar niebla" en el Inspector apaga de
        // verdad la niebla, en lugar de depender de lo último que hubiera en Lighting Settings.
        RenderSettings.fog = controlFog;

        // Material del rayo visible (30 ago 2026) — construido UNA vez y compartido por todos los
        // rayos, igual que _starMaterial en NightSkyStarSpawner.cs. Sprites/Default: mismo shader ya
        // confirmado compatible con URP en este proyecto (ver comentario de clase de
        // NightSkyStarSpawner), sin arriesgarse a un shader nuevo sin Editor para comprobarlo.
        _lightningBoltMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    void OnDestroy()
    {
        // _runtimeSkybox es una copia en memoria de sharedSkyboxMaterial (ver Awake), no el asset
        // compartido: hay que liberarla explícitamente o queda huérfana hasta la siguiente carga
        // de escena/recolección de basura.
        if (_runtimeSkybox != null)
        {
            Destroy(_runtimeSkybox);
            _runtimeSkybox = null;
        }
        if (_lightningBoltMaterial != null)
        {
            Destroy(_lightningBoltMaterial);
            _lightningBoltMaterial = null;
        }
    }

    void OnEnable()
    {
        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        // Si ya estábamos en un interior al activarnos (p.ej. carga directa a una escena de
        // interior), arrancar ya suprimidos para no mostrar/oír lluvia un frame de más.
        var ec = EnvironmentController.Instance;
        _outdoorWeatherSuppressedIndoors = ec != null && ec.CurrentMode == EnvironmentMode.Interior;

        // Un frame de margen antes de aplicar el periodo inicial. Si esta escena se abre y
        // se le da Play directamente (AutoBootstrapOnPlay carga 'Start' aditivamente ANTES de
        // entrar en PlayMode, ver Editor/AutoBootstrapOnPlay.cs), el orden de Awake/OnEnable entre
        // la escena 'Start' y esta escena no está garantizado, y tampoco lo está el orden relativo
        // a otros scripts de esta misma escena con prioridad por defecto (0), como
        // CloudCoverSpawner. Si InitializeCycle() corriera ya mismo de forma síncrona:
        //  - CloudCoverSpawner.OnEnable() podría no haberse suscrito aún a CloudsBuildingUp/
        //    RainStopped → el evento se dispara al vacío y el techo de nubes nunca aparece.
        //  - AudioService.Awake() (en 'Start') podría no haber corrido aún → AudioService.Instance
        //    sigue siendo null y PlayLoopingSFX/PlaySFX no hacen nada.
        // Resultado: "le doy a Play y sale lluvia directamente pero sin nubes y sin sfx". Esperar
        // un frame garantiza que todos los Awake/OnEnable de la carga inicial ya han corrido (mismo
        // patrón que usa WorldBootstrap.InitializeWorldDelayed / AmbientZone.CheckInitialOverlapNextFrame).
        StartCoroutine(InitializeCycleDelayed());

        // 30 ago 2026: temporizador de clima independiente del ciclo día/noche — ver
        // Header("Clima — sorteo independiente del periodo") y comentario de clase. Arranca aquí
        // (no hace falta esperar ningún frame de margen como InitializeCycleDelayed: el primer
        // sorteo no llega hasta pasado weatherCheckIntervalRange.x segundos como mínimo, tiempo de
        // sobra para que todo lo demás — AudioService, CloudCoverSpawner, etc. — ya esté listo).
        // Se detiene solo con StopAllCoroutines() en OnDisable, igual que el resto de corrutinas de
        // este componente.
        StartCoroutine(WeatherRollLoop());
    }

    IEnumerator InitializeCycleDelayed()
    {
        yield return null;
        InitializeCycle();
    }

    /// <summary>
    /// Bucle infinito con su PROPIO temporizador (weatherCheckIntervalRange), sin ninguna relación
    /// con las transiciones de periodo del día — ver TryRollWeather. Antes de este fix, ApplyTimeOfDay
    /// sorteaba lluvia/niebla una única vez cada vez que se ENTRABA en un periodo nuevo, así que el
    /// clima solo podía cambiar en esos instantes concretos; ahora puede cambiar en cualquier momento,
    /// esté el ciclo del día en amanecer/día/atardecer/noche o a mitad de transición entre dos.
    /// </summary>
    IEnumerator WeatherRollLoop()
    {
        while (true)
        {
            float wait = UnityEngine.Random.Range(weatherCheckIntervalRange.x, weatherCheckIntervalRange.y);
            yield return new WaitForSeconds(wait);
            TryRollWeather();
        }
    }

    /// <summary>
    /// Un único sorteo de clima, completamente independiente de qué periodo del día esté activo en
    /// este instante. No hace nada si ya hay clima en marcha (lluvia, nublándose, o niebla ocasional)
    /// — así un evento de clima activo no se ve interrumpido/reemplazado por el siguiente sorteo, se
    /// deja que termine solo (StartRain/StartMist ya gestionan su propia duración y desvanecido). El
    /// guard de minijuego activo ya lo hace StartRain() por su cuenta, pero se comprueba aquí también
    /// para no gastar el sorteo de niebla en ese caso (que StartMist SÍ dejaría arrancar, al no tener
    /// ese guard — la niebla no afecta a los minijuegos como sí lo hace la lluvia).
    /// </summary>
    void TryRollWeather()
    {
        if (IsRaining || _isCloudBuildingUp || IsMisty || IsWindy || IsThunderstorm) return;
        if (TagMinigameController.IsAnyMinigameActive) return;

        // Zona con niebla forzada (30 ago 2026, ver SetZoneMistOverride): nada de sorteo nuevo
        // (ni lluvia, ni tormenta, ni viento) mientras esté activa, solo se asegura de que la
        // niebla esté puesta — "la niebla manda, bloquea el resto" mientras estés dentro.
        if (_zoneMistForced) { TryActivateZoneMist(); return; }

        // Escalera de exclusión mutua (30 ago 2026): un único tipo de clima a la vez en esta
        // pasada — se prueba tormenta primero (la más rara/notable), luego lluvia normal, luego
        // viento, y por último niebla ocasional. Si uno "sale", los siguientes de ESTE mismo
        // sorteo ya no se comprueban (el siguiente sorteo llega solo en weatherCheckIntervalRange).
        if (UnityEngine.Random.value < thunderstormChancePerCheck)
        {
            StartThunderstorm(UnityEngine.Random.Range(rainDurationRange.x, rainDurationRange.y));
        }
        else if (UnityEngine.Random.value < rainChancePerCheck)
        {
            StartRain(UnityEngine.Random.Range(rainDurationRange.x, rainDurationRange.y));
        }
        else if (UnityEngine.Random.value < windChancePerCheck)
        {
            StartWind(UnityEngine.Random.Range(windDurationRange.x, windDurationRange.y));
        }
        else if (UnityEngine.Random.value < mistChancePerCheck)
        {
            StartMist(UnityEngine.Random.Range(mistDurationRange.x, mistDurationRange.y));
        }
    }

    void OnDisable()
    {
        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        StopAllCoroutines();
        IsRaining = false;
        _isCloudBuildingUp = false;
        _rainDarkenAmount = 0f;
        _rainDarkenCoroutine = null;
        _preStormSkybox = null;
        // Corta el loop de ambiente de lluvia (ver ActivateRain/BeginRainFadeOut). Sin esto, al
        // desactivarse este componente (p.ej. escena descargada al ir al menú principal) el SFX de
        // lluvia sigue sonando porque vive en una fuente dedicada de AudioService, en la escena
        // persistente 'Start' — no se para solo al desaparecer esta escena. Corte inmediato (sin
        // fade) porque la escena ya se está descargando.
        AudioService.Instance?.StopLoopingSFX(RainWeatherSfxLoopId);
        if (_cameraOverrideActive && _mainCamera != null)
        {
            _mainCamera.clearFlags = _preStormClearFlags;
            _mainCamera.backgroundColor = _preStormBackgroundColor;
            _cameraOverrideActive = false;
        }
        if (_activeRainInstance != null)
        {
            Destroy(_activeRainInstance);
            _activeRainInstance = null;
        }

        IsMisty = false;
        _mistAmount = 0f;
        _mistDarkenCoroutine = null;
        if (_activeMistInstance != null)
        {
            Destroy(_activeMistInstance);
            _activeMistInstance = null;
        }

        IsWindy = false;
        _windFadeCoroutine = null;
        AudioService.Instance?.StopLoopingSFX(WindWeatherSfxLoopId);
        if (_activeWindInstance != null)
        {
            Destroy(_activeWindInstance);
            _activeWindInstance = null;
        }

        IsThunderstorm = false;
        _thunderstormCoroutine = null;
        _lightningCoroutine = null;
    }

    /// <summary>
    /// Aplica el oscurecimiento por lluvia DESPUÉS de que Update haya fijado los valores del
    /// periodo del día actual, para no pelearse con la lógica de transición existente: primero se
    /// pone el "look" base del periodo, y aquí se atenúa por encima si está lloviendo.
    /// </summary>
    void LateUpdate()
    {
        if (_rainDarkenAmount <= 0f && _mistAmount <= 0f) return;

        if (_rainDarkenAmount > 0f && directionalLight != null)
        {
            float baseIntensity = directionalLight.intensity;
            float darkened = baseIntensity * rainLightIntensityMultiplier;
            // Suelo absoluto: en periodos ya oscuros (Night...) el multiplicador por sí solo puede
            // dejar la luz casi a cero. Nunca baja de rainMinLightIntensity.
            float floored = Mathf.Max(darkened, rainMinLightIntensity);
            directionalLight.intensity = Mathf.Lerp(baseIntensity, floored, _rainDarkenAmount);
        }

        if (controlFog)
        {
            // Siempre se parte de la densidad/color BASE del periodo actual, nunca del valor ya
            // escrito en RenderSettings el frame anterior (eso era lo que compondía exponencialmente).
            float density = _baseFogDensity;
            Color color   = _baseFogColor;

            if (_rainDarkenAmount > 0f)
            {
                density *= Mathf.Lerp(1f, rainFogDensityMultiplier, _rainDarkenAmount);
                color    = Color.Lerp(color, rainFogColorTint, _rainDarkenAmount * rainFogColorBlend);
            }

            if (_mistAmount > 0f)
                density *= Mathf.Lerp(1f, mistFogDensityMultiplier, _mistAmount);

            RenderSettings.fogDensity = density;
            RenderSettings.fogColor   = color;
        }
    }

    void HandleInteriorEntered()
    {
        _outdoorWeatherSuppressedIndoors = true;
        SetRainVisualActive(false);
        SetMistVisualActive(false);
        SetWindVisualActive(false);
        SetWeatherAudioSuppressed(true);
    }

    void HandleInteriorExited()
    {
        _outdoorWeatherSuppressedIndoors = false;
        SetRainVisualActive(true);
        SetMistVisualActive(true);
        SetWindVisualActive(true);
        SetWeatherAudioSuppressed(false);

        // Si la tormenta arrancó mientras estábamos dentro, o cambió el periodo del día,
        // ApplyStormSkybox()/ApplySettingsImmediate()/TransitionToSettings() se saltaron el cambio
        // de skybox (ver IsSkyboxLockedByEnvironment). Al volver a exterior hay que aplicarlo ahora,
        // si no el cielo se queda con el look de antes de entrar aunque haya cambiado mientras dentro.
        ReapplyPendingSkybox();
    }

    void SetRainVisualActive(bool active)
    {
        if (_activeRainInstance != null)
            _activeRainInstance.SetActive(active);
    }

    void SetMistVisualActive(bool active)
    {
        if (_activeMistInstance != null)
            _activeMistInstance.SetActive(active);
    }

    void SetWindVisualActive(bool active)
    {
        if (_activeWindInstance != null)
            _activeWindInstance.SetActive(active);
    }

    // FIX (30 ago 2026, incidencia reportada por Raúl: "se escuchaba como caía la lluvia" estando
    // dentro del castillo): HandleInteriorEntered/Exited y el sondeo cinemático de más abajo ya
    // suprimían la lluvia/niebla/viento VISUALMENTE al entrar en un interior, pero nunca tocaban
    // el audio — el loop de ambiente (PlayLoopingSFX, RainWeatherSfxLoopId/WindWeatherSfxLoopId)
    // es un AudioSource global sin spatial blend, así que seguía sonando igual de fuerte estando
    // dentro de cualquier interior, incluida una cinemática. Silenciar (no detener) el loop existente
    // conserva su posición de reproducción y el temporizador/fade-out real de la lluvia (IsRaining,
    // BeginRainFadeOut) intactos: si deja de llover mientras el jugador está dentro, el loop se para
    // igualmente vía StopLoopingSFX en su momento; esto solo evita que se OIGA mientras dure.
    void SetWeatherAudioSuppressed(bool suppressed)
    {
        AudioService.Instance?.SetLoopingSFXMuted(RainWeatherSfxLoopId, suppressed);
        AudioService.Instance?.SetLoopingSFXMuted(WindWeatherSfxLoopId, suppressed);
    }

    /// <summary>
    /// Reproduce un SFX por Event Key del AudioGraphProfile (AudioService.PlaySFX), si hay una
    /// clave configurada. Llamada directa a AudioService.Instance, igual que hace AmbientZone
    /// para sus propios sonidos de ambiente.
    /// </summary>
    void PlayWeatherSfx(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) return;
        AudioService.Instance?.PlaySFX(eventKey);
    }

    void RaiseCloudsBuildingUp()
    {
        onCloudsBuildingUp?.Invoke();
        CloudsBuildingUp?.Invoke();
        PlayWeatherSfx(cloudsBuildingUpSfxKey);
    }

    void Update()
    {
        // Detectar el final de un override cinemático (cinemática en un interior vía
        // CinematicSequencerBase/SimpleCinematicDirector) para re-aplicar aquí el skybox que se
        // haya quedado pendiente (ver IsSkyboxLockedByEnvironment). EnvironmentController no avisa
        // de esto con un evento, así que se sondea igual que hace el propio EnvironmentController
        // con su _cinematicReapplyPending.
        var ec = EnvironmentController.Instance;
        bool cinematicActiveNow = ec != null && ec.IsCinematicOverrideActive;
        if (_wasCinematicOverrideActive && !cinematicActiveNow)
            ReapplyPendingSkybox();
        _wasCinematicOverrideActive = cinematicActiveNow;

        // FIX: la supresión visual de lluvia/niebla (_outdoorWeatherSuppressedIndoors) solo se
        // actualizaba vía OnInteriorEntered/OnInteriorExited, que EnvironmentController dispara
        // únicamente desde ApplyInterior/ApplyExterior (entrar andando). El flujo cinemático
        // (CinematicSequencerBase → BeginCinematicOverride + ApplyInteriorForCinematic, ver p.ej.
        // TabernaSequencer) nunca pasa por ahí, así que si llovía justo al entrar en una cinemática
        // de interior, la lluvia seguía cayendo "dentro" durante toda la secuencia. Sondeamos aquí
        // IsEffectivelyInterior (que sí tiene en cuenta el override cinemático) y sincronizamos la
        // supresión con el mismo patrón edge-triggered que ya usa el resto de este método.
        bool effectivelyInteriorNow = ec != null && ec.IsEffectivelyInterior;
        if (effectivelyInteriorNow != _outdoorWeatherSuppressedIndoors)
        {
            _outdoorWeatherSuppressedIndoors = effectivelyInteriorNow;
            SetRainVisualActive(!effectivelyInteriorNow);
            SetMistVisualActive(!effectivelyInteriorNow);
            SetWindVisualActive(!effectivelyInteriorNow);
            SetWeatherAudioSuppressed(effectivelyInteriorNow);
            if (!effectivelyInteriorNow) ReapplyPendingSkybox();
        }

        // Minijuegos: durante un minijuego activo no puede llover (ver StartRain). Ese guard
        // solo bloquea lluvia NUEVA; aquí cortamos también la que ya estuviera cayendo justo al
        // entrar en el minijuego. Sondeo edge-triggered porque IsAnyMinigameActive es un flag
        // estático sin evento propio de inicio/fin (mismo patrón que el resto de este método).
        bool minigameActiveNow = TagMinigameController.IsAnyMinigameActive;
        if (minigameActiveNow && !_wasMinigameActive)
        {
            // Si era una tormenta, StopThunderstorm() ya para la lluvia de debajo (ver su cuerpo) —
            // llamarla en vez de StopRain() a secas evita dejar IsThunderstorm/los rayos colgados
            // con la lluvia ya parada.
            if (IsThunderstorm) StopThunderstorm();
            else if (IsRaining || _isCloudBuildingUp) StopRain();
        }
        _wasMinigameActive = minigameActiveNow;

        if (!autoAdvance || _isTransitioning) return;

        _timeElapsed += Time.deltaTime;

        if (_timeElapsed >= _currentDuration)
            AdvanceToNextPeriod();
    }

    /// <summary>
    /// True mientras algo ajeno al ciclo día/noche debe tener el control exclusivo de
    /// RenderSettings.skybox: el jugador está físicamente en un interior (AnchorEnvironment), o hay
    /// una cinemática con override activo (BeginCinematicOverride, típicamente con un anchor de
    /// interior propio vía ApplyInteriorForCinematic). En ambos casos escribir el skybox del periodo
    /// o de la tormenta aquí pisaría lo que EnvironmentController ya está mostrando — el bug de
    /// "sale un azul de fondo en medio de la secuencia" era justo esto: la transición de periodo o el
    /// inicio de lluvia ignoraban por completo el override cinemático.
    /// </summary>
    bool IsSkyboxLockedByEnvironment()
    {
        return _outdoorWeatherSuppressedIndoors
            || (EnvironmentController.Instance != null && EnvironmentController.Instance.IsCinematicOverrideActive);
    }

    /// <summary>
    /// Re-aplica el skybox correcto (tormenta si está lloviendo/nublando, si no el único skybox con
    /// los valores del periodo actual) cuando algo que lo tenía bloqueado (interior real o
    /// cinemática) deja de bloquearlo.
    /// </summary>
    void ReapplyPendingSkybox()
    {
        if (IsSkyboxLockedByEnvironment()) return; // seguimos bloqueados por otro motivo, no tocar

        if (IsRaining || _isCloudBuildingUp)
        {
            ApplyStormSkybox();
        }
        else if (_runtimeSkybox != null && RenderSettings.skybox != _runtimeSkybox)
        {
            // _runtimeSkybox ya tiene los valores correctos del periodo actual (se han seguido
            // actualizando en ApplySettingsImmediate/TransitionToSettings aunque estuviéramos
            // bloqueados), solo hace falta restaurar la referencia.
            RenderSettings.skybox = _runtimeSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }

    void InitializeCycle()
    {
        ApplyTimeOfDay(_currentIndex, immediate: true, invokeEvents: false);
    }

    public void AdvanceToNextPeriod()
    {
        int nextIndex = (_currentIndex + 1) % timeSettings.Length;
        ApplyTimeOfDay(nextIndex, immediate: false, invokeEvents: true);
    }

    public void SetTimeOfDay(TimeOfDay timeOfDay, bool immediate = false)
    {
        for (int i = 0; i < timeSettings.Length; i++)
        {
            if (timeSettings[i].timeOfDay == timeOfDay)
            {
                ApplyTimeOfDay(i, immediate, invokeEvents: true);
                return;
            }
        }
        Debug.LogWarning($"[DayNightCycle] TimeOfDay '{timeOfDay}' no encontrado en la configuración.");
    }

    public void SetNight() => SetTimeOfDay(TimeOfDay.Night, immediate: false);

    public void SetTimeOfDayByIndex(int index, bool immediate = false)
    {
        if (index < 0 || index >= timeSettings.Length)
        {
            Debug.LogWarning($"[DayNightCycle] Índice {index} fuera de rango.");
            return;
        }
        ApplyTimeOfDay(index, immediate, invokeEvents: true);
    }

    public void ToggleRain()
    {
        if (IsRaining || _isCloudBuildingUp) StopRain();
        else StartRain();
    }

    /// <summary>
    /// Arranca la lluvia. Por defecto, primero se nubla el cielo (nubes 3D de CloudCoverSpawner +
    /// oscurecimiento, ver rainDarkenTransitionDuration) y solo cuando termina esa transición
    /// empiezan a caer las partículas. Con immediate=true (carga de escena / test mode) se salta
    /// la nubosidad previa y la lluvia queda activa desde el primer frame.
    /// </summary>
    public void StartRain(float? duration = null, bool immediate = false)
    {
        if (rainPrefab == null)
        {
            Debug.LogWarning("[DayNightCycle] StartRain() no ha hecho nada: rainPrefab no está asignado en este GameObject/escena.");
            return;
        }
        if (IsRaining || _isCloudBuildingUp)
        {
            Debug.Log("[DayNightCycle] StartRain() no ha hecho nada: ya está lloviendo o nublándose (IsRaining/_isCloudBuildingUp). Usa StopRain() primero si quieres reiniciarla.");
            return;
        }

        // Durante los minijuegos no puede llover (p.ej. TagMinigameController): bloqueamos aquí
        // cualquier intento de arrancar lluvia, tanto el sorteo automático de ApplyTimeOfDay como
        // una llamada manual/narrativa a StartRain o ToggleRain.
        if (TagMinigameController.IsAnyMinigameActive)
        {
            Debug.Log("[DayNightCycle] StartRain() no ha hecho nada: hay un minijuego activo (TagMinigameController.IsAnyMinigameActive).");
            return;
        }

        if (_rainCoroutine != null)
            StopCoroutine(_rainCoroutine);

        float rainDuration = duration ?? 60f;
        _rainCoroutine = StartCoroutine(RainRoutine(rainDuration, immediate));
    }

    public void StopRain()
    {
        if (!IsRaining && !_isCloudBuildingUp) return;

        if (_rainCoroutine != null)
        {
            StopCoroutine(_rainCoroutine);
            _rainCoroutine = null;
        }

        if (_isCloudBuildingUp)
        {
            // Se canceló mientras el cielo aún se estaba nublando: la lluvia nunca llegó a caer,
            // así que solo revertimos el look sin disparar onRainStarted/onRainStopped.
            _isCloudBuildingUp = false;
            RevertStormSkybox();
            StartRainDarken(0f);
            return;
        }

        BeginRainFadeOut();
    }

    public void ToggleMist()
    {
        if (IsMisty) StopMist();
        else StartMist();
    }

    /// <summary>Arranca un evento de niebla ocasional, independiente de la lluvia.</summary>
    public void StartMist(float? duration = null)
    {
        if (IsMisty) return;

        if (_mistCoroutine != null)
            StopCoroutine(_mistCoroutine);

        float mistDuration = duration ?? UnityEngine.Random.Range(mistDurationRange.x, mistDurationRange.y);
        _mistCoroutine = StartCoroutine(MistRoutine(mistDuration));
    }

    public void StopMist()
    {
        if (!IsMisty) return;

        if (_mistCoroutine != null)
        {
            StopCoroutine(_mistCoroutine);
            _mistCoroutine = null;
        }

        BeginMistFadeOut();
    }

    /// <summary>
    /// Llamado por AmbientZone (ver AmbientPreset.forcesMist, 30 ago 2026) al entrar/salir de una
    /// zona con niebla forzada — por ejemplo el Bosque. Con active=true fuerza la niebla ocasional
    /// mientras dure la zona (y bloquea nuevos sorteos de lluvia/tormenta/viento vía TryRollWeather,
    /// "la niebla manda, bloquea el resto"); si ya estaba lloviendo al entrar, esa lluvia sigue tal
    /// cual (no se apila niebla encima) hasta que termine sola — ver BeginRainFadeOut, que llama a
    /// TryActivateZoneMist() al terminar por si sigue activa esta zona. Con active=false libera el
    /// bloqueo y apaga la niebla si la había puesto la zona. IMPORTANTE: esto NO se salta el
    /// bloqueo de interiores — sigue pasando por ActivateMist()/BeginMistFadeOut(), que ya
    /// respetan IsSkyboxLockedByEnvironment() igual que la niebla ocasional normal, así que no
    /// puede reintroducir niebla/lluvia dentro de un interior.
    /// </summary>
    public void SetZoneMistOverride(bool active)
    {
        if (_zoneMistForced == active) return;
        _zoneMistForced = active;

        if (active)
        {
            TryActivateZoneMist();
        }
        else if (IsMisty)
        {
            if (_mistCoroutine != null)
            {
                StopCoroutine(_mistCoroutine);
                _mistCoroutine = null;
            }
            BeginMistFadeOut();
        }
    }

    /// <summary>Activa la niebla YA (sin coroutine de duración) si toca — ver SetZoneMistOverride.</summary>
    void TryActivateZoneMist()
    {
        if (!_zoneMistForced) return;
        if (IsMisty || IsRaining || _isCloudBuildingUp || IsThunderstorm) return;
        if (_mistCoroutine != null)
        {
            StopCoroutine(_mistCoroutine);
            _mistCoroutine = null;
        }
        ActivateMist();
    }

    void ApplyTimeOfDay(int index, bool immediate, bool invokeEvents)
    {
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
            _isTransitioning = false;
        }

        _currentIndex = index;
        var settings = timeSettings[index];
        _currentDuration = Mathf.Max(1f, settings.duration);
        _timeElapsed = 0f;

        if (immediate || !useSmoothTransitions || !Application.isPlaying)
        {
            CurrentTimeOfDay = settings.timeOfDay;
            ApplySettingsImmediate(settings);
            if (invokeEvents)
            {
                onTimeOfDayChanged?.Invoke(settings.timeOfDay);
                TimeOfDayChanged?.Invoke(settings.timeOfDay);
            }
        }
        else
            _transitionCoroutine = StartCoroutine(TransitionToSettings(settings, invokeEvents));

        // 30 ago 2026: el clima (lluvia/niebla) YA NO se sortea aquí — ver
        // Header("Clima — sorteo independiente del periodo") y WeatherRollLoop/TryRollWeather más
        // abajo. Antes, cambiar de periodo era el ÚNICO momento en que se sorteaba lluvia/niebla, lo
        // que hacía que el clima se sintiera "pegado" al ciclo del día (petición explícita de Raúl:
        // el clima debe poder ocurrir en cualquier momento, no solo al entrar en un periodo nuevo).
        // Si ya estaba lloviendo/con niebla al cambiar de periodo, simplemente sigue como estaba —
        // ninguna transición de periodo interrumpe ni fuerza clima por sí sola.
    }

    void ApplySettingsImmediate(TimeOfDaySettings settings)
    {
        if (_runtimeSkybox != null)
        {
            _runtimeSkybox.SetColor(SkyboxTintId, settings.skyboxTint);
            _runtimeSkybox.SetFloat(SkyboxIntensityId, settings.skyboxIntensity);
            _runtimeSkybox.SetFloat(SkyboxExponentId, settings.skyboxExponent);
            _runtimeSkybox.SetFloat(SkyboxDirectionYawId, settings.skyboxDirectionYaw);
            _runtimeSkybox.SetFloat(SkyboxDirectionPitchId, settings.skyboxDirectionPitch);

            // No pisar la REFERENCIA de RenderSettings.skybox si un interior (real o cinemático)
            // tiene el control ahora mismo — ver IsSkyboxLockedByEnvironment. Los valores de arriba
            // ya han quedado guardados en _runtimeSkybox y se verán en cuanto ReapplyPendingSkybox
            // restaure la referencia al salir/terminar, sin esperar a la siguiente transición.
            if (!IsSkyboxLockedByEnvironment())
            {
                if (RenderSettings.skybox != _runtimeSkybox)
                    RenderSettings.skybox = _runtimeSkybox;
                DynamicGI.UpdateEnvironment();
            }
        }

        if (directionalLight != null)
        {
            directionalLight.color = settings.lightColor;
            directionalLight.intensity = settings.lightIntensity;
            directionalLight.transform.eulerAngles = new Vector3(settings.sunRotationX, settings.sunRotationY, 0f);
        }

        if (controlAmbientLight)
            RenderSettings.ambientLight = settings.ambientColor * settings.ambientIntensity;

        if (controlFog)
        {
            RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogDensity = settings.fogDensity;
            _baseFogColor = settings.fogColor;
            _baseFogDensity = settings.fogDensity;
        }
    }

    IEnumerator TransitionToSettings(TimeOfDaySettings target, bool invokeEvents)
    {
        _isTransitioning = true;

        // Asegurar que RenderSettings.skybox apunta a nuestra instancia ANTES de animar sus
        // propiedades, salvo que un interior/cinemática tenga el control ahora mismo (ver
        // IsSkyboxLockedByEnvironment). _runtimeSkybox se sigue actualizando cada frame más abajo
        // aunque estemos bloqueados: en cuanto se libere, ReapplyPendingSkybox restaura la
        // referencia y ya se ve con los valores correctos, sin esperar a la siguiente transición.
        if (_runtimeSkybox != null && RenderSettings.skybox != _runtimeSkybox && !IsSkyboxLockedByEnvironment())
            RenderSettings.skybox = _runtimeSkybox;

        var light = directionalLight;
        Color startLightColor = light ? light.color : Color.white;
        float startIntensity = light ? light.intensity : 1f;
        float startRotX = light ? light.transform.eulerAngles.x : 0f;
        float startRotY = light ? light.transform.eulerAngles.y : 0f;
        Color startAmbient = RenderSettings.ambientLight;
        // Partimos de la base guardada, no de RenderSettings.fogColor/fogDensity: si hay lluvia o
        // niebla ocasional activa en ese instante, esos valores ya están inflados por LateUpdate()
        // y arrastrarían el multiplicador a la transición.
        Color startFogColor = _baseFogColor;
        float startFogDensity = _baseFogDensity;

        Color startSkyboxTint = _runtimeSkybox != null ? _runtimeSkybox.GetColor(SkyboxTintId) : target.skyboxTint;
        float startSkyboxIntensity = _runtimeSkybox != null ? _runtimeSkybox.GetFloat(SkyboxIntensityId) : target.skyboxIntensity;
        float startSkyboxExponent = _runtimeSkybox != null ? _runtimeSkybox.GetFloat(SkyboxExponentId) : target.skyboxExponent;
        float startSkyboxYaw = _runtimeSkybox != null ? _runtimeSkybox.GetFloat(SkyboxDirectionYawId) : target.skyboxDirectionYaw;
        float startSkyboxPitch = _runtimeSkybox != null ? _runtimeSkybox.GetFloat(SkyboxDirectionPitchId) : target.skyboxDirectionPitch;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration));

            if (light != null)
            {
                light.color = Color.Lerp(startLightColor, target.lightColor, t);
                light.intensity = Mathf.Lerp(startIntensity, target.lightIntensity, t);
                light.transform.eulerAngles = new Vector3(
                    Mathf.LerpAngle(startRotX, target.sunRotationX, t),
                    Mathf.LerpAngle(startRotY, target.sunRotationY, t),
                    0f
                );
            }

            if (controlAmbientLight)
                RenderSettings.ambientLight = Color.Lerp(startAmbient, target.ambientColor * target.ambientIntensity, t);

            if (controlFog)
            {
                Color lerpedColor   = Color.Lerp(startFogColor, target.fogColor, t);
                float lerpedDensity = Mathf.Lerp(startFogDensity, target.fogDensity, t);

                // Actualizamos la base ya, no solo RenderSettings: si en este mismo frame hay
                // lluvia/niebla ocasional activa, LateUpdate() (que corre después) recalculará el
                // fog final multiplicando sobre esta base, en vez de dejar el valor sin oscurecer.
                _baseFogColor   = lerpedColor;
                _baseFogDensity = lerpedDensity;

                RenderSettings.fogColor   = lerpedColor;
                RenderSettings.fogDensity = lerpedDensity;
            }

            if (_runtimeSkybox != null)
            {
                _runtimeSkybox.SetColor(SkyboxTintId, Color.Lerp(startSkyboxTint, target.skyboxTint, t));
                _runtimeSkybox.SetFloat(SkyboxIntensityId, Mathf.Lerp(startSkyboxIntensity, target.skyboxIntensity, t));
                _runtimeSkybox.SetFloat(SkyboxExponentId, Mathf.Lerp(startSkyboxExponent, target.skyboxExponent, t));
                _runtimeSkybox.SetFloat(SkyboxDirectionYawId, Mathf.Lerp(startSkyboxYaw, target.skyboxDirectionYaw, t));
                _runtimeSkybox.SetFloat(SkyboxDirectionPitchId, Mathf.Lerp(startSkyboxPitch, target.skyboxDirectionPitch, t));
            }

            yield return null;
        }

        // Asegurar valores exactos al finalizar y actualizar el estado lógico cuando la visual ya coincide
        ApplySettingsImmediate(target);
        CurrentTimeOfDay = target.timeOfDay;

        if (invokeEvents)
        {
            onTimeOfDayChanged?.Invoke(target.timeOfDay);
            TimeOfDayChanged?.Invoke(target.timeOfDay);
        }

        _isTransitioning = false;
        _transitionCoroutine = null;
    }

    IEnumerator RainRoutine(float duration, bool immediate)
    {
        if (immediate)
        {
            // Carga de escena / test mode: sin nubosidad previa, igual que el resto del "look"
            // del periodo se aplica de golpe con ApplySettingsImmediate.
            if (_rainDarkenCoroutine != null)
            {
                StopCoroutine(_rainDarkenCoroutine);
                _rainDarkenCoroutine = null;
            }
            ApplyStormSkybox();
            _rainDarkenAmount = 1f;
            RaiseCloudsBuildingUp();
        }
        else
        {
            yield return CloudBuildUpRoutine();
        }

        ActivateRain();
        yield return new WaitForSeconds(duration);
        BeginRainFadeOut();
        _rainCoroutine = null;
    }

    /// <summary>
    /// Cubre el cielo de nubes (nubes 3D vía CloudsBuildingUp + empieza a oscurecer luz/niebla) ANTES
    /// de que arranque la lluvia. Se puede cancelar desde StopRain() mientras está en curso.
    /// </summary>
    IEnumerator CloudBuildUpRoutine()
    {
        _isCloudBuildingUp = true;
        // Momento señalado en la revisión de rendimiento del 24/08 como el más caro del clima
        // (formación del techo de nubes + DynamicGI.UpdateEnvironment) — se anota aparte de
        // "LluviaInicio" para poder distinguir en el .json la transición de la lluvia ya establecida.
        GameplayEventLog.Log("TormentaFormandoseInicio");

        ApplyStormSkybox();
        RaiseCloudsBuildingUp();
        StartRainDarken(1f);

        yield return new WaitForSeconds(Mathf.Max(0.01f, rainDarkenTransitionDuration));

        _isCloudBuildingUp = false;
    }

    void ApplyStormSkybox()
    {
        // No tocar cámara/skybox mientras el jugador está en un interior (real o cinemático):
        // EnvironmentController ya está aplicando el fondo/skybox de la AnchorEnvironment actual
        // (ver ApplyInteriorTo / ApplyInteriorForCinematic). Pisarlo aquí sin comprobar esto causaba
        // el bug "llueve dentro de la casa" / "sale un azul de fondo en medio de la secuencia": la
        // lluvia VISUAL sí se suprimía (ver ActivateRain), pero el fondo de cámara se sobrescribía
        // con el stormSkybox / el tinte gris de lluvia igualmente, en cuanto empezaba a nublarse.
        if (IsSkyboxLockedByEnvironment()) return;

        if (stormSkybox != null && RenderSettings.skybox != stormSkybox)
        {
            _preStormSkybox = RenderSettings.skybox;
            RenderSettings.skybox = stormSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // Red de seguridad HEREDADA del sistema de skyboxes por franja: sin un stormSkybox asignado,
        // el skybox despejado (con sol y rayos) seguía viéndose en el horizonte, más allá de donde
        // llega CloudCoverSpawner. Con un único skybox persistente (_runtimeSkybox) esto ya NO hace
        // falta — es justo el "como pasaría de verdad" que ya explicaba el tooltip de stormSkybox:
        // el cielo despejado por encima/alrededor del techo de nubes es el comportamiento deseado,
        // no un hueco que tapar. Forzar aquí un color sólido literalmente OCULTA el skybox único que
        // tanto costó dejar bonito. Por eso este fallback ahora solo se activa si NO hay
        // _runtimeSkybox (es decir, si sharedSkyboxMaterial no está asignado).
        if (stormSkybox == null && _runtimeSkybox == null && _mainCamera != null && !_cameraOverrideActive)
        {
            _preStormClearFlags = _mainCamera.clearFlags;
            _preStormBackgroundColor = _mainCamera.backgroundColor;
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = rainFogColorTint;
            _cameraOverrideActive = true;
        }
    }

    void RevertStormSkybox()
    {
        if (stormSkybox != null && RenderSettings.skybox == stormSkybox)
        {
            // Si ninguna transición de periodo cambió el skybox mientras tanto, volvemos al que
            // había antes de nublarse (o al único skybox persistente si no se guardó ninguno).
            RenderSettings.skybox = _preStormSkybox != null ? _preStormSkybox : _runtimeSkybox;
            DynamicGI.UpdateEnvironment();
            _preStormSkybox = null;
        }

        if (_cameraOverrideActive && _mainCamera != null)
        {
            _mainCamera.clearFlags = _preStormClearFlags;
            _mainCamera.backgroundColor = _preStormBackgroundColor;
            _cameraOverrideActive = false;
        }
    }

    void ActivateRain()
    {
        if (IsRaining || rainPrefab == null) return;

        // Cancelar fade-out activo antes de instanciar nueva lluvia
        if (_rainFadeCoroutine != null)
        {
            StopCoroutine(_rainFadeCoroutine);
            _rainFadeCoroutine = null;
            if (_activeRainInstance != null)
            {
                Destroy(_activeRainInstance);
                _activeRainInstance = null;
            }
        }

        Transform parent = PlayerService.Player != null ? PlayerService.Player.transform :
                           Camera.main != null ? Camera.main.transform : null;

        if (parent != null)
        {
            _activeRainInstance = Instantiate(rainPrefab, parent);
            _activeRainInstance.transform.localPosition = Vector3.zero;
        }
        else
        {
            _activeRainInstance = Instantiate(rainPrefab, transform.position, Quaternion.identity);
            Debug.LogWarning("[DayNightCycle] No se encontró jugador ni cámara, lluvia instanciada sin padre.");
        }

        // Si el jugador ya está en un interior (real o cinemático) cuando empieza a llover, que no
        // se vea/oiga hasta que salga (evita el problema de "llueve dentro de la casa").
        if (IsSkyboxLockedByEnvironment())
            _activeRainInstance.SetActive(false);

        IsRaining = true;
        onRainStarted?.Invoke();
        RainStarted?.Invoke();
        GameplayEventLog.Log("LluviaInicio");
        // El SFX de lluvia (rain-sfx.mp3 en el profile) es una pista de ambiente, no un one-shot
        // corto: si se reproduce con PlaySFX normal, el AudioSource se autodevuelve al pool cuando
        // el CLIP termina, no cuando deja de llover. Con lluvias cortas o interrumpidas por
        // StopRain(), eso deja el SFX sonando de fondo mucho después de que IsRaining ya es false
        // (bug reportado: "ha terminado de llover y no ha parado el sfx"). PlayLoopingSFX usa una
        // fuente dedicada que solo se detiene explícitamente en BeginRainFadeOut vía StopLoopingSFX.
        AudioService.Instance?.PlayLoopingSFX(RainWeatherSfxLoopId, rainStartedSfxKey);
        // FIX (30 ago 2026): el comentario de arriba ("que no se vea/oiga hasta que salga") solo se
        // cumplía a medias — _activeRainInstance.SetActive(false) oculta el visual, pero el loop de
        // audio arrancaba igual de audible aunque el jugador ya estuviera dentro de un interior (o de
        // una cinemática con override de interior) en el instante exacto en que empieza a llover.
        if (IsSkyboxLockedByEnvironment())
            AudioService.Instance?.SetLoopingSFXMuted(RainWeatherSfxLoopId, true);
        // El oscurecimiento (luz + niebla) y la cobertura de nubes ya se aplicaron durante la
        // nubosidad previa (CloudBuildUpRoutine) o de golpe si immediate=true, así que aquí solo
        // queda instanciar las partículas de lluvia.
    }

    void BeginRainFadeOut()
    {
        if (!IsRaining) return;

        IsRaining = false;
        onRainStopped?.Invoke();
        RainStopped?.Invoke();
        GameplayEventLog.Log("LluviaFin");
        // Corta el loop de ambiente arrancado en ActivateRain (ver comentario allí). Fundido con
        // la misma duración que el fade-out visual de las partículas para que no se note el corte.
        AudioService.Instance?.StopLoopingSFX(RainWeatherSfxLoopId, rainFadeOutTime);
        PlayWeatherSfx(rainStoppedSfxKey);
        StartRainDarken(0f);
        RevertStormSkybox();

        if (_rainFadeCoroutine != null)
            StopCoroutine(_rainFadeCoroutine);

        if (_activeRainInstance != null)
            _rainFadeCoroutine = StartCoroutine(RainFadeOutRoutine(_activeRainInstance));

        // Si la lluvia termina mientras seguimos dentro de una zona con niebla forzada (ver
        // SetZoneMistOverride) — porque ya estaba lloviendo al entrar en la zona, así que no se le
        // pisó — la niebla de zona toma el relevo aquí mismo, sin esperar al siguiente sorteo.
        TryActivateZoneMist();
    }

    void StartRainDarken(float target)
    {
        if (_rainDarkenCoroutine != null)
            StopCoroutine(_rainDarkenCoroutine);
        _rainDarkenCoroutine = StartCoroutine(RainDarkenRoutine(target));
    }

    IEnumerator RainDarkenRoutine(float target)
    {
        float start = _rainDarkenAmount;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, rainDarkenTransitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _rainDarkenAmount = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        _rainDarkenAmount = target;
        _rainDarkenCoroutine = null;
    }

    IEnumerator RainFadeOutRoutine(GameObject rainInstance)
    {
        // Detener emisión para que las partículas existentes terminen de caer
        var particles = rainInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(rainFadeOutTime);

        if (rainInstance != null)
            Destroy(rainInstance);

        if (_activeRainInstance == rainInstance)
            _activeRainInstance = null;

        _rainFadeCoroutine = null;
    }

    IEnumerator MistRoutine(float duration)
    {
        ActivateMist();
        yield return new WaitForSeconds(duration);
        BeginMistFadeOut();
        _mistCoroutine = null;
    }

    void ActivateMist()
    {
        if (IsMisty) return;

        // Cancelar fade-out activo antes de instanciar nueva niebla
        if (_mistFadeCoroutine != null)
        {
            StopCoroutine(_mistFadeCoroutine);
            _mistFadeCoroutine = null;
            if (_activeMistInstance != null)
            {
                Destroy(_activeMistInstance);
                _activeMistInstance = null;
            }
        }

        if (mistPrefab != null)
        {
            Transform parent = PlayerService.Player != null ? PlayerService.Player.transform :
                               Camera.main != null ? Camera.main.transform : null;

            if (parent != null)
            {
                _activeMistInstance = Instantiate(mistPrefab, parent);
                _activeMistInstance.transform.localPosition = Vector3.zero;
            }
            else
            {
                _activeMistInstance = Instantiate(mistPrefab, transform.position, Quaternion.identity);
                Debug.LogWarning("[DayNightCycle] No se encontró jugador ni cámara, niebla instanciada sin padre.");
            }

            // Igual que con la lluvia: si el jugador ya está en un interior (real o cinemático),
            // que no se vea hasta salir.
            if (IsSkyboxLockedByEnvironment())
                _activeMistInstance.SetActive(false);
        }

        IsMisty = true;
        onMistStarted?.Invoke();
        MistStarted?.Invoke();
        // SFX de inicio de niebla quitado a petición de Raúl (no le gustaba) — 1 sep 2026.
        StartMistAmount(1f);
    }

    void BeginMistFadeOut()
    {
        if (!IsMisty) return;

        IsMisty = false;
        onMistStopped?.Invoke();
        MistStopped?.Invoke();
        // SFX de fin de niebla quitado a petición de Raúl (no quiere que suene nada al
        // salir la niebla) — 1 sep 2026. Mismo criterio que el SFX de inicio, ya quitado
        // antes (ver ActivateMist). mistStoppedSfxKey se deja serializado sin más, por si se
        // quiere reactivar más adelante.
        StartMistAmount(0f);

        if (_mistFadeCoroutine != null)
            StopCoroutine(_mistFadeCoroutine);

        if (_activeMistInstance != null)
            _mistFadeCoroutine = StartCoroutine(MistFadeOutRoutine(_activeMistInstance));
    }

    void StartMistAmount(float target)
    {
        if (_mistDarkenCoroutine != null)
            StopCoroutine(_mistDarkenCoroutine);
        _mistDarkenCoroutine = StartCoroutine(MistAmountRoutine(target));
    }

    IEnumerator MistAmountRoutine(float target)
    {
        float start = _mistAmount;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, mistTransitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _mistAmount = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        _mistAmount = target;
        _mistDarkenCoroutine = null;
    }

    IEnumerator MistFadeOutRoutine(GameObject mistInstance)
    {
        var particles = mistInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(mistFadeOutTime);

        if (mistInstance != null)
            Destroy(mistInstance);

        if (_activeMistInstance == mistInstance)
            _activeMistInstance = null;

        _mistFadeCoroutine = null;
    }

    // ==================== Viento (30 ago 2026) ====================
    // Mismo patrón que la niebla ocasional (StartMist/StopMist/MistRoutine/ActivateMist/
    // BeginMistFadeOut/MistFadeOutRoutine) pero más simple: el viento no oscurece luz ni engorda
    // niebla en esta pasada, así que no hace falta ningún "amount" gradual — solo instancia
    // opcional (windPrefab, null-safe) + evento/SFX en loop + fundido de las partículas al parar.

    public void ToggleWind()
    {
        if (IsWindy) StopWind();
        else StartWind();
    }

    /// <summary>Arranca un evento de viento, independiente de la lluvia/niebla/tormenta.</summary>
    public void StartWind(float? duration = null)
    {
        if (IsWindy) return;

        if (_windCoroutine != null)
            StopCoroutine(_windCoroutine);

        float windDuration = duration ?? UnityEngine.Random.Range(windDurationRange.x, windDurationRange.y);
        _windCoroutine = StartCoroutine(WindRoutine(windDuration));
    }

    public void StopWind()
    {
        if (!IsWindy) return;

        if (_windCoroutine != null)
        {
            StopCoroutine(_windCoroutine);
            _windCoroutine = null;
        }

        BeginWindFadeOut();
    }

    IEnumerator WindRoutine(float duration)
    {
        ActivateWind();
        yield return new WaitForSeconds(duration);
        BeginWindFadeOut();
        _windCoroutine = null;
    }

    void ActivateWind()
    {
        if (IsWindy) return;

        if (_windFadeCoroutine != null)
        {
            StopCoroutine(_windFadeCoroutine);
            _windFadeCoroutine = null;
            if (_activeWindInstance != null)
            {
                Destroy(_activeWindInstance);
                _activeWindInstance = null;
            }
        }

        if (windPrefab != null)
        {
            Transform parent = PlayerService.Player != null ? PlayerService.Player.transform :
                               Camera.main != null ? Camera.main.transform : null;

            if (parent != null)
            {
                _activeWindInstance = Instantiate(windPrefab, parent);
                _activeWindInstance.transform.localPosition = Vector3.zero;
            }
            else
            {
                _activeWindInstance = Instantiate(windPrefab, transform.position, Quaternion.identity);
                Debug.LogWarning("[DayNightCycle] No se encontró jugador ni cámara, viento instanciado sin padre.");
            }

            if (IsSkyboxLockedByEnvironment())
                _activeWindInstance.SetActive(false);
        }

        IsWindy = true;
        onWindStarted?.Invoke();
        WindStarted?.Invoke();
        GameplayEventLog.Log("VientoInicio");
        // Igual que la lluvia (ver ActivateRain): loop dedicado, no un one-shot que se autodevuelve
        // al pool cuando termina el CLIP en vez de cuando termina el viento de verdad.
        AudioService.Instance?.PlayLoopingSFX(WindWeatherSfxLoopId, windStartedSfxKey);
        // FIX (30 ago 2026): mismo hueco que ActivateRain (ver comentario ahí) — silenciar también
        // si ya estamos en un interior/cinemática cuando arranca el viento.
        if (IsSkyboxLockedByEnvironment())
            AudioService.Instance?.SetLoopingSFXMuted(WindWeatherSfxLoopId, true);
    }

    void BeginWindFadeOut()
    {
        if (!IsWindy) return;

        IsWindy = false;
        onWindStopped?.Invoke();
        WindStopped?.Invoke();
        GameplayEventLog.Log("VientoFin");
        AudioService.Instance?.StopLoopingSFX(WindWeatherSfxLoopId, windFadeOutTime);
        PlayWeatherSfx(windStoppedSfxKey);

        if (_windFadeCoroutine != null)
            StopCoroutine(_windFadeCoroutine);

        if (_activeWindInstance != null)
            _windFadeCoroutine = StartCoroutine(WindFadeOutRoutine(_activeWindInstance));
    }

    IEnumerator WindFadeOutRoutine(GameObject windInstance)
    {
        var particles = windInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(windFadeOutTime);

        if (windInstance != null)
            Destroy(windInstance);

        if (_activeWindInstance == windInstance)
            _activeWindInstance = null;

        _windFadeCoroutine = null;
    }

    // ==================== Tormenta / Rayos (30 ago 2026) ====================
    // Una tormenta ES lluvia (reutiliza StartRain/StopRain tal cual — mismas partículas,
    // oscurecimiento, niebla, nubosidad previa y SFX de siempre) MÁS una capa de rayos periódicos
    // por encima. No se ha creado un sistema de lluvia paralelo: solo se añade la parte de rayos.

    /// <summary>
    /// Arranca una tormenta: lluvia normal (StartRain) + rayos periódicos (LightningLoop) durante
    /// <paramref name="duration"/> segundos. Si ya está lloviendo (narrativa, u otra llamada), no
    /// hace nada — igual que StartRain, para no pisar una lluvia ya en marcha por otro motivo.
    /// </summary>
    public void StartThunderstorm(float? duration = null, bool immediate = false)
    {
        if (IsThunderstorm)
        {
            Debug.Log("[DayNightCycle] StartThunderstorm() no ha hecho nada: ya hay una tormenta activa.");
            return;
        }
        if (IsRaining || _isCloudBuildingUp)
        {
            Debug.Log("[DayNightCycle] StartThunderstorm() no ha hecho nada: ya está lloviendo o nublándose por otro motivo (StartRain/StartThunderstorm no se pueden solapar). Usa StopRain() primero si quieres forzar la tormenta.");
            return;
        }
        if (TagMinigameController.IsAnyMinigameActive)
        {
            Debug.Log("[DayNightCycle] StartThunderstorm() no ha hecho nada: hay un minijuego activo.");
            return;
        }

        if (_thunderstormCoroutine != null)
            StopCoroutine(_thunderstormCoroutine);

        float stormDuration = duration ?? UnityEngine.Random.Range(rainDurationRange.x, rainDurationRange.y);
        _thunderstormCoroutine = StartCoroutine(ThunderstormRoutine(stormDuration, immediate));
    }

    /// <summary>
    /// Para la tormenta YA (rayos + lluvia), sin esperar a que acabe su temporizador. Si la lluvia
    /// de una tormenta se para con StopRain() en vez de con esta función, los rayos seguirán
    /// sonando/destellando hasta que acabe el temporizador de la tormenta — usar siempre
    /// StopThunderstorm() para pararla del todo de golpe (p.ej. narrativa, minijuego).
    /// </summary>
    public void StopThunderstorm()
    {
        if (!IsThunderstorm) return;

        if (_thunderstormCoroutine != null)
        {
            StopCoroutine(_thunderstormCoroutine);
            _thunderstormCoroutine = null;
        }

        FinishThunderstorm();
        StopRain();
    }

    IEnumerator ThunderstormRoutine(float duration, bool immediate)
    {
        IsThunderstorm = true;
        onThunderstormStarted?.Invoke();
        ThunderstormStarted?.Invoke();
        GameplayEventLog.Log("TormentaInicio");

        StartRain(duration, immediate);

        if (_lightningCoroutine != null)
            StopCoroutine(_lightningCoroutine);
        // El primer rayo llega YA (sin esperar thunderstormLightningIntervalRange) — una tormenta
        // que empieza con un trueno inmediato se lee mejor que un silencio de hasta 22s, y de paso
        // da feedback instantáneo al testear ("Testeo: Tormenta (iniciar)"). Los rayos siguientes
        // dentro del mismo LightningLoop sí respetan el intervalo normal.
        _lightningCoroutine = StartCoroutine(LightningLoop(firstStrikeImmediate: true));

        yield return new WaitForSeconds(duration);

        // La lluvia de debajo ya se para sola (StartRain(duration) programó su propio fin) — aquí
        // solo queda cerrar la capa extra de rayos.
        FinishThunderstorm();
        _thunderstormCoroutine = null;
    }

    void FinishThunderstorm()
    {
        if (!IsThunderstorm) return;

        IsThunderstorm = false;
        onThunderstormStopped?.Invoke();
        ThunderstormStopped?.Invoke();
        GameplayEventLog.Log("TormentaFin");

        if (_lightningCoroutine != null)
        {
            StopCoroutine(_lightningCoroutine);
            _lightningCoroutine = null;
        }
    }

    IEnumerator LightningLoop(bool firstStrikeImmediate = false)
    {
        bool first = true;
        while (true)
        {
            if (first && firstStrikeImmediate)
            {
                // Nada de espera para el primer rayo — ver comentario en ThunderstormRoutine.
            }
            else
            {
                float wait = UnityEngine.Random.Range(thunderstormLightningIntervalRange.x, thunderstormLightningIntervalRange.y);
                yield return new WaitForSeconds(wait);
            }
            first = false;
            yield return FlashLightningRoutine();
        }
    }

    /// <summary>
    /// Un único rayo: destello + trueno con retraso. El destello es una Light DIRECCIONAL NUEVA y
    /// temporal (no toca directionalLight para nada) — ver el tooltip de thunderstormFlashIntensity
    /// para el porqué: directionalLight.intensity ya se recalcula cada frame en LateUpdate a partir
    /// de SU PROPIO valor actual mientras llueve (oscurecimiento por lluvia, ver
    /// rainLightIntensityMultiplier), así que escribir ahí directamente para el destello se pelearía
    /// con ese cálculo y dejaría un resultado impredecible en vez de un destello limpio.
    /// </summary>
    /// <summary>
    /// El trazo del rayo en sí (el "zigzag" que cae del cielo), no solo el destello de luz
    /// ambiental de FlashLightningRoutine — sin esto un "rayo" era indistinguible de un simple
    /// parpadeo de brillo general, que es justo lo que Raúl señaló que faltaba ("debe verse con
    /// rayos caer"). GameObject temporal con LineRenderer en world space (mismo patrón de líneas
    /// que NightSkyConstellationSpawner), material compartido _lightningBoltMaterial (ver Awake).
    /// Nace en el cielo (thunderstormBoltHeight) y "golpea" un punto en el suelo a una distancia y
    /// ángulo aleatorios alrededor del jugador (thunderstormBoltDistanceRange) — los extremos
    /// (cielo/impacto) quedan fijos, y solo los puntos intermedios se desvían horizontalmente
    /// (thunderstormBoltJitter) para dar la forma quebrada característica de un rayo, en vez de una
    /// línea recta. Se autodestruye a los pocos instantes, igual que el destello de luz.
    /// </summary>
    void SpawnLightningBolt()
    {
        if (_lightningBoltMaterial == null)
            return;

        Transform reference = PlayerService.Player != null ? PlayerService.Player.transform :
                              Camera.main != null ? Camera.main.transform : transform;

        // Sesgado hacia donde mira la cámara (±65°) en vez de 360° completos — con ángulo
        // totalmente libre, la mayoría de los rayos nacían fuera de la vista de la cámara y era
        // imposible verlos por pura geometría, sin que hubiera nada mal en el material/shader.
        float cameraYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : reference.eulerAngles.y;
        float boltYaw = cameraYaw + UnityEngine.Random.Range(-65f, 65f);
        float distance = UnityEngine.Random.Range(thunderstormBoltDistanceRange.x, thunderstormBoltDistanceRange.y);
        Vector3 horizontalOffset = (Quaternion.Euler(0f, boltYaw, 0f) * Vector3.forward) * distance;

        Vector3 groundPoint = reference.position + horizontalOffset;
        Vector3 topPoint = groundPoint + Vector3.up * thunderstormBoltHeight;

        int segments = Mathf.Max(2, thunderstormBoltSegments);

        var boltObj = new GameObject("[ThunderstormLightningBolt]");
        var line = boltObj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.material = _lightningBoltMaterial;
        line.startColor = thunderstormBoltColor;
        line.endColor = thunderstormBoltColor;
        line.widthMultiplier = thunderstormBoltWidth;
        line.numCapVertices = 2;
        line.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 point = Vector3.Lerp(topPoint, groundPoint, t);

            // Los extremos se dejan sin desviar (nace arriba, golpea el punto elegido) — el
            // zigzag va solo en los puntos intermedios.
            if (i != 0 && i != segments)
            {
                float jitterX = UnityEngine.Random.Range(-thunderstormBoltJitter, thunderstormBoltJitter);
                float jitterZ = UnityEngine.Random.Range(-thunderstormBoltJitter, thunderstormBoltJitter);
                point += new Vector3(jitterX, 0f, jitterZ);
            }

            line.SetPosition(i, point);
        }

        Destroy(boltObj, Mathf.Max(0.05f, thunderstormBoltVisibleDuration));
    }

    IEnumerator FlashLightningRoutine()
    {
        SpawnLightningBolt();

        var flashObj = new GameObject("[ThunderstormLightningFlash]");
        var flash = flashObj.AddComponent<Light>();
        flash.type = LightType.Directional;
        flash.color = Color.white;
        flash.intensity = thunderstormFlashIntensity;
        flash.shadows = LightShadows.None;
        if (directionalLight != null)
            flashObj.transform.rotation = directionalLight.transform.rotation;

        yield return new WaitForSeconds(Mathf.Max(0.01f, thunderstormFlashDuration));

        if (flashObj != null)
            Destroy(flashObj);

        float thunderDelay = UnityEngine.Random.Range(thunderstormThunderDelayRange.x, thunderstormThunderDelayRange.y);
        yield return new WaitForSeconds(thunderDelay);
        PlayWeatherSfx(thunderstormThunderSfxKey);
    }

    // 30 ago 2026 — Raúl pidió botones de prueba en el Inspector ("testeos") para poder forzar cada
    // periodo del día y el clima sin esperar al ciclo automático, sobre todo para probar cambios de
    // arte del cielo (p.ej. el domo de estrellas, ver NightSkyStarSpawner) sin tener que esperar a
    // que le toque la noche por turno. Todos con el prefijo "Testeo:" para que aparezcan agrupados y
    // se distingan a simple vista de las acciones "de verdad" (SetTimeOfDay/StartRain/etc., ya
    // públicas y usadas por narrativa). Los periodos usan immediate:true a propósito (sin la
    // transición suave de transitionDuration, ~10-16s) — para testear rápido interesa el cambio ya,
    // no verlo animarse.
    [ContextMenu("Testeo: Ir a Amanecer")]
    public void DebugGoToMorning() => SetTimeOfDay(TimeOfDay.Morning, immediate: true);

    [ContextMenu("Testeo: Ir a Día")]
    public void DebugGoToDay() => SetTimeOfDay(TimeOfDay.AfterNoon, immediate: true);

    [ContextMenu("Testeo: Ir a Atardecer")]
    public void DebugGoToSunset() => SetTimeOfDay(TimeOfDay.Sunset, immediate: true);

    [ContextMenu("Testeo: Ir a Noche")]
    public void DebugGoToNight() => SetTimeOfDay(TimeOfDay.Night, immediate: true);

    [ContextMenu("Testeo: Avanzar al siguiente periodo")]
    public void DebugAdvanceTime() => AdvanceToNextPeriod();

    // Antes había un único "Activar/Desactivar lluvia" (ToggleRain) — con un solo ítem que hace lo
    // contrario según el estado interno, es fácil pulsar esperando que empiece y que en realidad
    // pare (o al revés) si no se tiene clara la etiqueta ni el estado actual. Separado en dos
    // acciones explícitas: cada una hace SIEMPRE lo que dice, sin depender de IsRaining. StartRain()/
    // StopRain() ya no hacen nada si ya está en ese estado (no hace falta guardia extra aquí).
    [ContextMenu("Testeo: Lluvia (iniciar)")]
    public void DebugStartRain() => StartRain();

    [ContextMenu("Testeo: Lluvia (detener)")]
    public void DebugStopRain() => StopRain();

    [ContextMenu("Testeo: Niebla (iniciar)")]
    public void DebugStartMist() => StartMist();

    [ContextMenu("Testeo: Niebla (detener)")]
    public void DebugStopMist() => StopMist();

    [ContextMenu("Testeo: Viento (iniciar)")]
    public void DebugStartWind() => StartWind();

    [ContextMenu("Testeo: Viento (detener)")]
    public void DebugStopWind() => StopWind();

    [ContextMenu("Testeo: Tormenta (iniciar)")]
    public void DebugStartThunderstorm() => StartThunderstorm(immediate: true);

    [ContextMenu("Testeo: Tormenta (detener)")]
    public void DebugStopThunderstorm() => StopThunderstorm();

    [ContextMenu("Testeo: Rayo ya (un solo destello + trueno)")]
    public void DebugStrikeLightning() => StartCoroutine(FlashLightningRoutine());

    [ContextMenu("Testeo: Forzar sorteo de clima ya (sin esperar weatherCheckIntervalRange)")]
    public void DebugForceWeatherRoll() => TryRollWeather();
}

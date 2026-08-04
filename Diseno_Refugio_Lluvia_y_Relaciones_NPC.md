# Diseño: Refugio de NPCs bajo la lluvia + Relaciones sociales dinámicas

**Proyecto:** El Sendero de las Estrellas
**Fecha:** 4 agosto 2026
**Estado:** Propuesta de diseño — pendiente de aprobación antes de implementar

Decisiones ya tomadas contigo:
- Refugio en casas = el NPC desaparece al llegar a la puerta (no hay interior real que visitar).
- Relaciones dinámicas = persistentes desde la v1 (se guardan en el save).

Todo lo citado abajo (rutas, clases, métodos, líneas) está verificado leyendo el código real del proyecto, no asumido.

---

## 0. Diagnóstico previo (por qué esto no es trivial)

- **No existe ningún sistema de clima consultable por NPCs.** La lluvia vive en `Assets/Scripts/World/DayNightCycle.cs`, con eventos C# públicos (`event Action RainStarted`, `event Action RainStopped`, `bool IsRaining`), pero hoy **cero scripts de NPC están suscritos**. `DayNightCycle` no es un singleton (`FindAnyObjectByType`, sin `Instance`, no vive en `Start.unity`) — probablemente hay una instancia por escena de pueblo.
- **No existe el concepto de "punto de refugio"** (árbol, porche, puerta) en ningún tag/capa/registro. Hay que crearlo desde cero.
- **El sistema social existe en código, pero en la práctica no se nota nunca — verificado, no es solo "a medias".** `NPCSocialConfig` (SO), `NPCRelationship.cs`, `WanderState.CheckSocialEncounter()`, `NPCSocialEncounterState`, `NPCBehaviourManagerV2.TryAcceptSocialEncounter()` están correctamente enlazados (capas, colliders, componentes, todo revisado en el prefab real `TownNpc#1.prefab` y la escena `MainWorld.unity`). El problema es que **cuatro condiciones tienen que coincidir a la vez** para que un encuentro se dispare, y casi nunca coinciden:
  1. Solo hay **13 NPCs "ambientales" que vagan de verdad** en toda `MainWorld.unity` (10 `TownNpc#` + 3 `Guerrero#`), repartidos por un pueblo entero — poca densidad, pocas coincidencias de proximidad.
  2. El escaneo (`CheckSocialEncounter`) **solo corre mientras el NPC está caminando en `WanderState`**, nunca en `IdleState` ni sentado en un banco (`WalkToActivityState`), y solo cada 3 segundos.
  3. Cada intento tira un dado contra `personality.sociability` (0.8 en el arquetipo "Amigable", pero puede ser bajo en otros), y el NPC receptor tiene que estar libre de su propio `socialCooldown` (25-45s) en ese instante exacto.
  4. Bug de identidad confirmado: `Assets/_NPCs/Social/NPC_Social_Archetype_Friendly.asset` (y previsiblemente sus hermanos Reserved/Energetic/Lazy/Grumpy) tienen **`npcId: ''` (vacío)**. Esto no impide que el encuentro se dispare, pero hace que los 13 NPCs de relleno que comparten ese arquetipo sean indistinguibles entre sí a efectos de relación.
  Además, `relationships[]` es un array estático de diseño dentro del `ScriptableObject`, vacío en los 27 SO reales del proyecto — nunca se escribe en runtime, así que aunque un encuentro se dispare, jamás evoluciona a nada.
  **Conclusión:** no basta con "arreglar" la forja de relaciones (Parte B original) — sin subir la frecuencia/visibilidad de los encuentros, el arreglo sería invisible. Por eso lo que antes era la mejora opcional B.6 (radar de amistad + escaneo también en Idle) pasa a ser parte del núcleo de esta feature, no un pulido final.
- **Problema adicional no obvio**: varios NPCs de "relleno" (`TownNpc#1`, `TownNpc#5`, `TownNpc#10`, `Guerrero#1`...) **comparten el mismo asset `NPCSocialConfig`** (mismo `npcId`) porque reutilizan un arquetipo genérico. Si escribimos relaciones nuevas directamente en ese SO compartido, todos los NPCs que comparten arquetipo heredarían la misma relación con el mismo tercero — un bug de identidad, no solo de datos. La solución no puede tocar el SO en runtime.

---

## PARTE A — Refugio de NPCs bajo la lluvia

### A.1 Alcance de la v1

- Cuando empieza a llover, los NPCs ambientales interrumpen lo que están haciendo (vagar, sentados en un banco) y caminan hacia el punto de refugio más cercano.
- Si el punto es un árbol/porche exterior: se quedan ahí parados (idle) hasta que pare de llover.
- Si el punto es una puerta de casa: caminan hasta la puerta y **desaparecen** (se desactiva el GameObject), simulando que han entrado. Cuando deja de llover, reaparecen en esa misma puerta y retoman su rutina.
- NPCs en combate, cinemática, diálogo o interactuando **no se ven afectados** — las prioridades de transición ya existentes (`IsInCinematic > IsInCombat > WasDefeatedInCombat > IsInteracting`) se respetan tal cual.
- NPCs "importantes" (mercaderes con puesto fijo, guardias apostados, NPCs con diálogo de quest activo) deben poder **desactivar** este comportamiento con un flag, para no romper su disponibilidad narrativa.

### A.2 Piezas nuevas

**1. `Assets/Scripts/Behaviour NPC/Common/NPCWeatherAwareness.cs` (nuevo, clase estática)**

Evita que cada NPC haga su propio `FindAnyObjectByType<DayNightCycle>()` (caro si hay decenas de NPCs por escena). Un único punto de suscripción por escena, expuesto como evento estático barato:

```csharp
public static class NPCWeatherAwareness
{
    public static event Action RainStarted;
    public static event Action RainStopped;
    public static bool IsRaining { get; private set; }

    private static DayNightCycle _cycle;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _cycle = null;
        IsRaining = false;
        RainStarted = null;
        RainStopped = null;
    }
#endif

    // Llamado por WorldBootstrap (execution order +200) tras cargar cada escena aditiva,
    // y también desde NPCBehaviourManagerV2.Awake() como fallback idempotente.
    public static void Resubscribe()
    {
        if (_cycle != null)
        {
            _cycle.RainStarted -= OnRainStarted;
            _cycle.RainStopped -= OnRainStopped;
        }

        _cycle = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        if (_cycle == null) return; // escena sin ciclo día/noche (interiores, mazmorras)

        _cycle.RainStarted += OnRainStarted;
        _cycle.RainStopped += OnRainStopped;
        IsRaining = _cycle.IsRaining;
    }

    private static void OnRainStarted() { IsRaining = true;  RainStarted?.Invoke(); }
    private static void OnRainStopped() { IsRaining = false; RainStopped?.Invoke(); }
}
```

**Punto a decidir en implementación:** ¿quién llama a `Resubscribe()` tras cada carga aditiva de escena? Candidato natural: `WorldBootstrap.cs` (execution order +200, ya orquesta el mundo tras cargar). Si `DayNightCycle` no cambia de instancia entre escenas del mismo pueblo, una sola llamada en `Start()` de `WorldBootstrap` basta.

**2. `Assets/Scripts/Behaviour NPC/NPCShelterPoint.cs` (nuevo)**

Calcado deliberadamente de `NPCWorldPoint.cs` (mismo patrón: registro estático `OnEnable/OnDisable`, `TryFindNearest`, gizmos) para que cualquiera que conozca `NPCWorldPoint` entienda este de inmediato:

```csharp
public enum NPCShelterType { TreeCanopy, HouseDoor }

public class NPCShelterPoint : MonoBehaviour
{
    public NPCShelterType shelterType = NPCShelterType.TreeCanopy;
    public Transform interactionPoint;      // igual que NPCWorldPoint
    public int capacity = 3;                // TreeCanopy: varios NPCs caben bajo el mismo árbol
                                             // HouseDoor: normalmente 1 (o pocos) para no saturar

    private readonly List<Transform> _occupants = new(); // NO se allocan en Update, solo en TryOccupy/Release

    public bool IsFull => _occupants.Count >= capacity;
    public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;
    public Quaternion InteractionRotation => interactionPoint != null ? interactionPoint.rotation : transform.rotation;

    private static readonly List<NPCShelterPoint> _all = new();
    public static IReadOnlyList<NPCShelterPoint> All => _all;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _all.Clear();
#endif

    void OnEnable()  => _all.Add(this);
    void OnDisable() { _all.Remove(this); _occupants.Clear(); }

    public bool TryOccupy(Transform occupant)
    {
        if (IsFull || _occupants.Contains(occupant)) return IsFull ? false : true;
        _occupants.Add(occupant);
        return true;
    }

    public void Release(Transform occupant) => _occupants.Remove(occupant);

    public static bool TryFindNearest(Vector3 position, NPCShelterType? filter, float maxDist, out NPCShelterPoint result)
    {
        result = null;
        float bestSqr = maxDist * maxDist;
        foreach (var sp in _all)
        {
            if (sp == null || sp.IsFull) continue;
            if (filter.HasValue && sp.shelterType != filter.Value) continue;
            float sqr = (sp.InteractionPosition - position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; result = sp; }
        }
        return result != null;
    }
}
```

**3. `Assets/Scripts/Behaviour NPC/States/SeekShelterState.cs` (nuevo `INPCState`)**

Calcado del esqueleto de `WalkToActivityState.cs` (caminar → llegar → ocupar → esperar → liberar), con la rama especial de "desaparecer en la puerta":

- `OnEnter`: `NPCShelterPoint.TryFindNearest(pos, null, maxDist: ~25f, out point)`. Si no hay ninguno libre en rango, no forzamos nada raro: el NPC se queda en `IdleState` bajo la lluvia (mejor que un NPC vagando sin rumbo buscando algo que no existe). Si hay punto, `SetDestination` igual que `WalkToActivityState`.
- `OnUpdate`: igual que `WalkToActivityState` — comprobar `HasReachedDestination`. Al llegar:
  - `TreeCanopy` → `TryOccupy`, `StopMovement`, orientar hacia el tronco/interior de la copa, reproducir una animación idle existente (no hace falta animación nueva: basta con parar el movimiento y, opcionalmente, `Animator.PlaySocialGesture("Question01")` ocasional para dar vida — evaluar en producción si merece una animación dedicada de "resguardarse").
  - `HouseDoor` → tras un pequeño delay (0.6–1s, tiempo de "abrir la puerta"), desactivar el NPC: **guardar el punto de refugio en el contexto (`context.CurrentShelter`) y hacer `gameObject.SetActive(false)`**, respetando la regla de CLAUDE.md de no llamar `SetActive` sin comprobar el estado previo (guardar un bool `_hidden` y solo desactivar una vez).
- `CheckTransitions`: mismas prioridades que siempre (`IsInCinematic > IsInCombat > WasDefeatedInCombat > IsInteracting`) primero. Después: `if (!context.ShouldSeekShelter) return HandleReturn(context);` — donde `HandleReturn`:
  - Si el NPC está oculto (`HouseDoor`), lo reactiva en la posición de la puerta (`gameObject.SetActive(true)`, reposicionar con `Agent.Warp(shelterPoint.InteractionPosition)` para evitar el mismo bug INC-046 de agentes "flotando" documentado en `WalkToActivityState.OnExit`), libera el punto, transiciona a `WanderState`.
  - Si estaba en `TreeCanopy`, simplemente libera el punto y transiciona a `WanderState` desde donde esté parado.
- `OnExit`: liberar el `NPCShelterPoint` (`Release(context.Transform)`) siempre, por si se interrumpe a media transición (p.ej. entra en combate estando bajo el árbol).

**4. Cambios en `NPCStateContext.cs`**

Añadir junto a los campos `Pending Social*` ya existentes:

```csharp
public bool ShouldSeekShelter { get; set; }
public NPCShelterPoint CurrentShelter { get; set; }
```

**5. Cambios en `NPCBehaviourManagerV2.cs`**

En `Awake()` (junto al resto de inicialización): `NPCWeatherAwareness.Resubscribe()` si aún no se ha llamado esta escena (idempotente), y suscribirse a `NPCWeatherAwareness.RainStarted/RainStopped` para setear `_context.ShouldSeekShelter = true/false`. Desuscribir en `OnDestroy`.

Guard opcional por NPC: nuevo campo en `NPCAmbientConfig` (el módulo ya existente, mismo sitio que `enableWander`), p.ej. `public bool canSeekShelter = true;`. Vendedores con puesto fijo, guardias apostados, NPCs con diálogo de quest activo → desmarcar en el inspector. Este patrón replica exactamente cómo ya se controla `enableWander` hoy (`NPCConfiguration.enableWander`, línea 144 de `NPCConfiguration.cs`).

### A.3 Integración con estados existentes

Añadir la misma comprobación en las tres puertas de entrada donde hoy también se comprueba `PendingSocialPartner`, con **menor prioridad que combate/cinemática/interacción pero mayor que "seguir vagando"**:

- `IdleState.CheckTransitions()`
- `WanderState.CheckTransitions()`
- `WalkToActivityState.CheckTransitions()` (un NPC sentado en un banco debe levantarse a refugiarse, igual que hoy se levanta si otro NPC quiere socializar con él)

Ejemplo de línea a añadir (idéntica forma a la ya existente para `PendingSocialPartner`):

```csharp
if (context.ShouldSeekShelter && context.CurrentShelter == null && config.canSeekShelter)
    return new SeekShelterState();
```

### A.4 Casos límite a resolver en implementación

- **Rain empieza mientras el NPC ya está en `SeekShelterState` yendo hacia otro sitio** (imposible salvo bug, pero por seguridad `ShouldSeekShelter` solo dispara la transición si `context.CurrentShelter == null`, evitando reentradas).
- **Un NPC queda "atrapado" dentro de una casa si la escena se descarga/recarga con la lluvia aún activa**: al reactivar tras cargar, `NPCBehaviourManagerV2.Awake()` debe comprobar `NPCWeatherAwareness.IsRaining` y, si sigue lloviendo, mantenerlo oculto o (más simple y robusto) simplemente no ocultar nada al recargar — cada carga de escena empieza "fresca" reevaluando el estado real.
- **NPCs relevantes para el grafo narrativo**: si un `NarrativeRunner` espera interactuar con un NPC concreto y este se ha desactivado por la lluvia, la interacción fallaría silenciosamente. Mitigación: `canSeekShelter = false` en cualquier NPC con `narrativeID` asignado en `NPCRegistry` (se puede automatizar: si `NPCBehaviourManagerV2` tiene un `narrativeID` no vacío, forzar `canSeekShelter = false` salvo override explícito). Anotar esto como checklist antes de dar por cerrada la feature.
- **Colocación de puntos de refugio**: trabajo manual de nivel, no de código — colocar `NPCShelterPoint` bajo árboles y en puertas de casas de cada escena de pueblo. Ninguna automatización razonable para esto sin analizar la geometría de cada escena.

### A.5 Archivos a crear/tocar (Parte A)

| Acción | Archivo |
|---|---|
| Crear | `Assets/Scripts/Behaviour NPC/Common/NPCWeatherAwareness.cs` |
| Crear | `Assets/Scripts/Behaviour NPC/NPCShelterPoint.cs` |
| Crear | `Assets/Scripts/Behaviour NPC/States/SeekShelterState.cs` |
| Tocar | `Assets/Scripts/Behaviour NPC/Common/NPCStateContext.cs` (2 propiedades nuevas) |
| Tocar | `Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs` (suscripción a eventos de lluvia) |
| Tocar | `Assets/Scripts/Behaviour NPC/States/IdleState.cs`, `WanderState.cs`, `WalkToActivityState.cs` (una línea cada uno en `CheckTransitions`) |
| Tocar | Módulo `NPCAmbientConfig` (nuevo flag `canSeekShelter`) |
| Tocar (posible) | `Assets/Scripts/Core/WorldBootstrap.cs` (llamada a `NPCWeatherAwareness.Resubscribe()`) |
| Trabajo de nivel | Colocar `NPCShelterPoint` en escenas de pueblo (árboles, puertas) |

---

## PARTE B — Relaciones sociales dinámicas entre NPCs

### B.1 Qué se arregla exactamente

Hoy: `WanderState.CheckSocialEncounter()` (línea 308) llama `socialConfig.GetRelationshipWith(partnerId)`, que lee el array estático `relationships[]` del `ScriptableObject` — vacío siempre, así que todo resuelve a `Stranger`. El encuentro (`NPCSocialEncounterState`) es puramente cosmético: elige gestos según relación, pero **nunca la modifica ni la crea**.

Objetivo: que hablar repetidamente haga que dos NPCs concretos pasen de `Stranger` → `Acquaintance` → `Friend` → `BestFriend`, que eso se note (duración/gestos ya varían según el enum, así que el pago visual ya existe gratis), y que sobreviva a guardar/cargar.

### B.2 Decisión de identidad (el problema no obvio — verificado, no es hipotético)

`npcId` vive en el `NPCSocialConfig` (SO). Comprobado en el asset real `Assets/_NPCs/Social/NPC_Social_Archetype_Friendly.asset`: **`npcId: ''` (vacío)**, y es el que usan los 13 NPCs de relleno que vagan en `MainWorld.unity` (`TownNpc#1-10`, `Guerrero#1-3`). Con `npcId` vacío, ni siquiera se puede aplicar la solución "compartir progreso entre figurantes" que había planteado como aceptable — un `npcId` vacío es indistinguible de "sin identidad", así que ninguno de los 13 NPCs que más vagan por el pueblo podría forjar ninguna relación tal cual está montado hoy. Dado que además son literalmente el grueso de la población ambiental de la escena, dejar esto sin arreglar deja la feature entera sin sujetos sobre los que demostrarse.

**Decisión recomendada para v1 (cambiada respecto a la primera versión de este documento):** no basta con "aceptar" el `npcId` compartido — hay que garantizar que **todo NPC con `NPCSocialConfig` asignado tenga una identidad única en runtime**, aunque el SO de personalidad sea compartido. Opción de menor riesgo: en `NPCBehaviourManagerV2.Awake()`, si `configuration.socialConfig.npcId` está vacío, generar y cachear un id estable derivado de algo que ya identifica a esa instancia concreta — candidato directo: `persistenceId` si existe (los NPCs narrativos ya lo tienen, ej. `NPC_Eldran`), y si no, `gameObject.name + "_" + GetInstanceID()` (estable durante la sesión, se re-genera cada partida pero eso es aceptable para relleno anónimo — lo importante es que sea único *entre* los 13, no que sea el mismo ID entre sesiones). Este id runtime-only vive en el propio `NPCBehaviourManagerV2`/`NPCStateContext` (nunca se escribe de vuelta al SO compartido), y es el que se usa como clave en `NPCRelationshipRegistry`, no el `npcId` crudo del SO.

Los NPCs con nombre propio (Eldran, Sofía...) siguen usando su `npcId` real del SO individual, que ya es único — sin cambios para ellos.

Esto se documenta como comentario explícito en el código (`NPCBehaviourManagerV2`) para que quien lo lea entienda por qué hay dos fuentes de identidad (SO para autoría, runtime-id para forja) y no se intente "simplificar" fusionándolas.

### B.3 Registro runtime nuevo: `NPCRelationshipRegistry`

**`Assets/Scripts/Behaviour NPC/NPCRelationshipRegistry.cs` (nuevo, estático — mismo patrón que `ActiveCombatRegistry.cs`)**

No se puede escribir en el `ScriptableObject` compartido (corrompería a todos los NPCs del arquetipo). El estado dinámico vive aparte, indexado por el par de `npcId`:

```csharp
public static class NPCRelationshipRegistry
{
    private struct Bond
    {
        public int encounterCount;
        public float bondScore;          // 0-100, acumulado en encuentros completados
        public NPCRelationType? forgedType; // null = usar el valor autor (relationships[] del SO)
    }

    private static readonly Dictionary<(string, string), Bond> _bonds = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _bonds.Clear();
#endif

    private static (string, string) Key(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    // Se llama SOLO cuando el encuentro social se completa de forma natural
    // (NPCSocialEncounterState.OnExit, _timer >= _duration), nunca si se interrumpe por combate/cinemática.
    public static NPCRelationType RegisterEncounterCompleted(string idA, string idB, float avgFriendliness)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB) || idA == idB)
            return NPCRelationType.Stranger;

        var key = Key(idA, idB);
        _bonds.TryGetValue(key, out var bond);

        bond.encounterCount++;
        bond.bondScore += Mathf.Lerp(2f, 8f, avgFriendliness); // más simpáticos, vínculo crece más rápido

        // No promocionar relaciones ya marcadas como Rival/Enemy por diseño (esas son fijas, autor-only en v1)
        var authored = ResolveAuthored(idA, idB);
        if (authored != NPCRelationType.Rival && authored != NPCRelationType.Enemy)
        {
            bond.forgedType = bond.bondScore switch
            {
                >= 60f => NPCRelationType.BestFriend,
                >= 30f => NPCRelationType.Friend,
                >= 10f => NPCRelationType.Acquaintance,
                _      => bond.forgedType, // no degradar antes de tiempo
            };
        }

        _bonds[key] = bond;
        return Resolve(idA, idB);
    }

    // Resolución que reemplaza a socialConfig.GetRelationshipWith en los puntos de consulta:
    // 1) override runtime forjado, 2) valor autor del SO, 3) Stranger.
    public static NPCRelationType Resolve(string idA, string idB, Func<string, NPCRelationType> authoredLookup = null)
    {
        if (_bonds.TryGetValue(Key(idA, idB), out var bond) && bond.forgedType.HasValue)
            return bond.forgedType.Value;
        return authoredLookup != null ? authoredLookup(idB) : NPCRelationType.Stranger;
    }

    private static NPCRelationType ResolveAuthored(string idA, string idB) => NPCRelationType.Stranger; // ver B.4

    // Persistencia — ver B.5
    public static List<PlayerSaveData.NpcRelationshipEntry> ToSaveEntries() { /* ... */ return null; }
    public static void LoadFromSaveEntries(List<PlayerSaveData.NpcRelationshipEntry> entries) { /* ... */ }
}
```

*(Pseudocódigo de diseño — los detalles de `ResolveAuthored` se resuelven en implementación real pasando el `NPCSocialConfig` del NPC iniciador, ver B.4.)*

Umbrales (10/30/60) son un punto de partida, no un valor cerrado — tunable en producción jugando unas cuantas sesiones.

### B.4 Punto de integración: `WanderState.CheckSocialEncounter()`

Cambio mínimo, línea 308 (`NPCRelationType relation = socialConfig.GetRelationshipWith(partnerId);`) pasa a:

```csharp
NPCRelationType relation = NPCRelationshipRegistry.Resolve(
    socialConfig.npcId, partnerId,
    otherId => socialConfig.GetRelationshipWith(otherId)); // fallback al valor autor del SO
```

Y en `NPCSocialEncounterState.cs`, añadir un `OnExit` (hoy no existe override, hereda el de `NPCStateBase`) que, **solo si el encuentro terminó de forma natural** (no interrumpido por combate/cinemática — comprobar `_timer >= _duration` antes de salir), llame:

```csharp
NPCRelationshipRegistry.RegisterEncounterCompleted(myId, partnerId, avgFriendliness);
```

`avgFriendliness` = promedio de `personality.friendliness` de ambos NPCs (se puede pasar por `context` o recuperar de `configuration.socialConfig` de ambos lados).

### B.5 Persistencia (siguiendo el patrón exacto de `npcPositions`)

El proyecto ya tiene un patrón claro y probado para "estado runtime de NPCs que debe sobrevivir al save" — es literalmente `npcPositions`, presente en tres sitios que se mantienen sincronizados:

1. `PlayerSaveData.cs` — struct `NpcPosEntry` + `List<NpcPosEntry> npcPositions`.
2. `PlayerPresetSO.cs` — su propio `NpcPosEntry` espejo, que es el estado "vivo" durante la sesión.
3. `GameBootProfile.cs` — copia entre ambos en `SetRuntimePresetFromSave()` (~línea 214-222), en la construcción del save (~línea 364-370) y en la ruta de test mode (~línea 405-420).

Para relaciones, replicar exactamente esa estructura:

1. **`PlayerSaveData.cs`** — nuevo struct y lista:
   ```csharp
   [Serializable]
   public struct NpcRelationshipEntry
   {
       public string npcIdA;
       public string npcIdB;
       public NPCRelationType type;
       public int encounterCount;
       public float bondScore;
   }
   public List<NpcRelationshipEntry> npcRelationships = new();
   ```
   En `FromGameBootProfile()`: `d.npcRelationships = NPCRelationshipRegistry.ToSaveEntries();`
   En `ApplyToGameBootProfile()` (vía `SetRuntimePresetFromSave`): `NPCRelationshipRegistry.LoadFromSaveEntries(data.npcRelationships);`

2. **`PlayerPresetSO.cs`** — mismo struct espejo (`NpcRelationshipEntry`) + lista, igual que ya existe para `NpcPosEntry`.

3. **`GameBootProfile.cs`** — añadir el mismo bloque de copia ida/vuelta que ya existe para `npcPositions` en los 3 puntos citados arriba.

4. **Sanitización de saves antiguos**: en `SaveSystem.LoadFromPath()` (línea 76-82), añadir `data.npcRelationships ??= new List<PlayerSaveData.NpcRelationshipEntry>();` junto a las demás listas saneadas, para que saves guardados antes de esta feature no rompan al cargar.

**Respetar la Regla 1 de CLAUDE.md** (modo test = volcado exacto del `bootPreset`, sin mezclar con JSON): las relaciones deben fluir por el mismo camino que `npcPositions` en modo test (`EnsureRuntimePresetFromTemplate` + `ApplyPresetAsLoadedGame`), nunca leerse del JSON real en ese modo. Como se está clonando el patrón ya existente literalmente campo por campo, esto sale gratis si se sigue la plantilla — pero es el punto a verificar con más cuidado al implementar, porque romper esa regla afecta al grafo narrativo persistente completo, no solo a esta feature.

### B.6 Por qué esto **no es opcional**: sin esto, el arreglo de B.1-B.5 sigue siendo invisible

En la primera versión de este documento esto estaba planteado como "mejora opcional". Al verificar los números reales (13 NPCs ambientales en total en `MainWorld.unity`, escaneo social solo en `WanderState`, ventana de 3s, doble dado de sociabilidad/cooldown — ver diagnóstico al inicio del documento), queda claro que **arreglar solo la forja de relaciones (B.1-B.5) no resuelve tu queja**: seguiría pasando casi nunca, solo que ahora "casi nunca" acumularía progreso en vez de no acumular nada. Estos dos puntos pasan a ser parte obligatoria de la v1, no un pulido posterior:

1. **Radar de amistad** (obligatorio): en `WanderState.OnEnter()` (donde hoy se decide si ir a un `NPCWorldPoint` o vagar), añadir una probabilidad (ponderada por `sociability`) de que el NPC intente *buscar específicamente* a un `Friend`/`BestFriend` conocido dentro de un radio ampliado (p. ej. `socialDetectionRange * 2.5`) en vez de solo detectar a quien pase cerca por azar. Necesita un pequeño registro espacial "dónde está cada NPC ahora" — un registro nuevo tipo `ActiveCombatRegistry` pero para NPCs ambientales (`NPCAmbientRegistry`: `Dictionary<string runtimeId, Transform>` actualizado en `OnEnable`/`OnDisable` de `NPCBehaviourManagerV2`, coste O(1)). Sin esto, con solo 13 NPCs en todo el pueblo, dos con vínculo fuerte pueden pasarse la partida entera sin volver a cruzarse por pura geografía aleatoria.
2. **Activar el escaneo social también en `IdleState`** (obligatorio): hoy solo ocurre mientras el NPC camina (`WanderState`), nunca en `IdleState` ni sentado en un banco. Dado que un NPC pasa una parte grande de su ciclo de vida en Idle/actividades, dejarlo fuera reduce las oportunidades reales a la mitad o menos. Coste: mover `CheckSocialEncounter()` a un método compartido en `NPCStateBase` y llamarlo también desde `IdleState.OnUpdate`.
3. **Subir `socialDetectionRange` y bajar `socialCooldown` en los arquetipos de relleno** (ajuste de datos, no de código): `NPC_Social_Archetype_Friendly.asset` hoy tiene 5m/25s. Con solo 13 NPCs en todo el pueblo, valores conservadores pensados para una escena más poblada dejan la feature muerta por pura estadística. Subir a ~8-10m y bajar cooldown a ~15-20s para los arquetipos de relleno (no necesariamente para NPCs nombrados, donde el ritmo pausado puede ser intencional).

Con los tres puntos, la combinación (más alcance, más ventanas de detección, búsqueda activa de amigos) es lo que convierte "existe en el código" en "se ve en la partida".

### B.7 Archivos a crear/tocar (Parte B)

| Acción | Archivo |
|---|---|
| Crear | `Assets/Scripts/Behaviour NPC/NPCRelationshipRegistry.cs` |
| Crear | `Assets/Scripts/Behaviour NPC/NPCAmbientRegistry.cs` (registro espacial para el radar de amistad, B.6.1) |
| Tocar | `Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs` (id runtime cuando `npcId` está vacío, ver B.2; registro/desregistro en `NPCAmbientRegistry`) |
| Tocar | `Assets/Scripts/Behaviour NPC/States/WanderState.cs` (línea 308, resolución de relación; `OnEnter`, radar de amistad B.6.1) |
| Tocar | `Assets/Scripts/Behaviour NPC/States/IdleState.cs` (activar escaneo social, B.6.2) |
| Tocar | `Assets/Scripts/Behaviour NPC/Common/NPCStateBase.cs` (extraer `CheckSocialEncounter` a método compartido) |
| Tocar | `Assets/Scripts/Behaviour NPC/States/NPCSocialEncounterState.cs` (nuevo `OnExit`, registrar encuentro completado) |
| Tocar (datos) | `Assets/_NPCs/Social/NPC_Social_Archetype_*.asset` (subir `socialDetectionRange`, bajar `socialCooldown`, B.6.3) |
| Tocar | `Assets/Scripts/Player/PlayerSaveData.cs` (struct + lista + sanitización) |
| Tocar | `PlayerPresetSO.cs` (struct espejo + lista) |
| Tocar | `Assets/Scripts/Core/GameBootProfile.cs` (copia ida/vuelta, 3 puntos) |
| Tocar | `Assets/Scripts/Core/SaveSystem.cs` (línea ~76-82, sanitización de listas null) |
| Opcional (B.6) | `Assets/Scripts/Behaviour NPC/States/WanderState.cs`, `IdleState.cs`, nuevo registro espacial de NPCs |

---

## Orden de implementación recomendado

1. **Parte B primero** (relaciones) — es la que te da más rabia y el riesgo es menor (no toca movimiento/NavMesh, solo datos + un `OnExit`). Empezar por B.3-B.4 sin persistencia, jugar y validar que las relaciones evolucionan bien en una sesión. Añadir B.5 (persistencia) una vez el comportamiento en runtime se sienta bien — así no hay que retocar el guardado dos veces si cambian los umbrales.
2. **Parte A después** (refugio de lluvia) — más trabajo de nivel (colocar puntos manualmente) y más superficie de casos límite (NPCs narrativos, agentes de NavMesh). Construir `NPCShelterPoint` + `SeekShelterState` con solo `TreeCanopy` primero, verificar que el ciclo completo (lluvia → refugio → vuelta) funciona bien, y añadir `HouseDoor` (con el `SetActive`) después.
3. **B.6** (radar de amistad / idle social) al final, como pulido, una vez lo esencial de ambas partes esté verificado en el juego real.

## Preguntas abiertas para validar antes de programar

- Umbrales de `bondScore` (10/30/60) y velocidad de acumulación: valores de partida, se ajustan jugando.
- ¿Quién llama a `NPCWeatherAwareness.Resubscribe()` tras cada carga aditiva de escena? Propuesto `WorldBootstrap`, a confirmar mirando su `Start()` real.
- Lista de NPCs que deben quedar excluidos del refugio de lluvia (guardias apostados, vendedores con puesto fijo, cualquiera con `narrativeID` activo) — se puede generar automáticamente o requerir marcado manual en el inspector; recomendado automático con override manual.

using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Se activa cuando empieza a llover (NPCWeatherAwareness.RainStarted, propagado a
    /// context.ShouldSeekShelter por NPCBehaviourManagerV2). El NPC camina hasta el
    /// NPCShelterPoint más cercano (TreeCanopy en el bosque o RoofedSpot bajo un GO con techo del
    /// pueblo, sin distinción de comportamiento entre ambos — el NPC nunca desaparece ni "entra"
    /// a ningún sitio, siempre queda visible) y, al llegar, decide entre dos roles para dar variedad visual:
    ///   - Sitter: se sienta en el suelo (PlayAmbientActivity(SitGround), ya usado en otras
    ///     actividades ambientales) y se queda así hasta que deje de llover.
    ///   - Stander: se queda de pie alternando entre el idle normal (con sus propias variaciones
    ///     automáticas, ver NPCSimpleAnimator.IdleVariationLoop) y gestos cortos de "tener frío"
    ///     (Fear01, con más peso) u otros gestos sueltos (Fidget, HeadShake01/02, Question01) a
    ///     intervalos aleatorios.
    /// Si no hay ningún punto de refugio libre en rango, el NPC se queda en IdleState bajo la
    /// lluvia en vez de forzar un comportamiento sin sentido.
    /// Cuando deja de llover, el NPC NO desaparece ni se queda vagando desde el refugio: pasa a
    /// ReturnFromShelterState, que lo lleva de vuelta al punto exacto donde estaba antes de que
    /// empezara la tormenta (context.ShelterOriginPosition).
    /// Ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § A.2-A.4.
    /// </summary>
    public class SeekShelterState : NPCStateBase
    {
        public override string StateName => "SeekShelter";

        private const float MaxSearchDistance = 25f;

        // Distancia por debajo de la cual se considera que ya se llegó al punto real (aunque el
        // NavMeshAgent haya redondeado ligeramente al punto más cercano del NavMesh).
        private const float ArrivedAtRealPointThreshold = 0.15f;

        // Tope de seguridad para la aproximación manual fuera del NavMesh (ver
        // NPCStateBase.BeginManualApproach). Si el punto real está más lejos que esto del borde
        // del NavMesh, algo va raro (punto mal colocado / geometría rara) y es más seguro
        // quedarse en el borde que caminar a ciegas sin comprobación de obstáculos.
        private const float MaxManualApproachDistance = 4f;

        // Probabilidad de que este NPC, en concreto, decida sentarse en vez de quedarse de pie.
        private const float SitChance = 0.35f;

        // Intervalo entre gestos de "tener frío"/variedad mientras está de pie refugiado.
        private const float MinGestureInterval = 4f;
        private const float MaxGestureInterval = 9f;

        // "Fear01" aparece dos veces a propósito: es el gesto principal de "tener frío" bajo la
        // lluvia, el resto son variaciones sueltas para que no todos los NPCs hagan lo mismo.
        private static readonly string[] ColdGestures =
        {
            "Fear01", "Fear01", "Fidget", "FidgetIndex", "HeadShake01", "HeadShake02", "Question01"
        };

        private NPCShelterPoint _shelterPoint;
        private bool _arrived;
        private bool _isSitter;
        private float _nextGestureTimer;
        private bool _manualApproachActive;

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            _arrived = false;
            _manualApproachActive = false;
            context.CurrentShelter = null;
            context.HasShelterEdgePosition = false;

            // Por si venimos de un tramo de aproximación manual interrumpido a medias (ver
            // BeginManualApproach): asegurar que el agente vuelve a estar bajo control normal
            // del NavMesh antes de intentar fijar un nuevo destino.
            EndManualApproach(context);
            if (context.Agent != null && !context.Agent.isOnNavMesh)
                NavMeshAgentUtility.EnsureAgentOnNavMesh(context.Agent, context.Transform.position, 5f);

            if (!NPCShelterPoint.TryFindNearest(context.Transform.position, null, MaxSearchDistance, out _shelterPoint))
            {
                // No hay refugio libre en rango: quedarse en Idle bajo la lluvia en vez de forzar
                // un comportamiento sin sentido (vagar buscando algo que no existe).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogNoShelterFound(context);
#endif
                context.Brain?.ChangeState(new IdleState());
                return;
            }

            // Guardar el punto de partida solo la primera vez: si Arrive() reintenta este mismo
            // estado (el punto se ocupó justo antes de llegar), no queremos sobreescribir el
            // origen real con una posición intermedia de camino al refugio.
            if (!context.HasShelterOrigin)
            {
                context.ShelterOriginPosition = context.Transform.position;
                context.HasShelterOrigin      = true;
            }

            if (context.Agent != null && context.Agent.isOnNavMesh)
            {
                float speed = context.Config?.walkSpeed ?? 1.5f;
                context.Agent.speed     = speed;
                context.Agent.isStopped = false;
            }

            SetDestination(context, _shelterPoint.InteractionPosition);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (!_arrived)
            {
                if (_manualApproachActive)
                {
                    float approachSpeed = context.Config?.walkSpeed ?? 1.5f;
                    if (ManualApproachStep(context, _shelterPoint.InteractionPosition, approachSpeed, _shelterPoint.OwnerCollider))
                        Arrive(context);
                    return;
                }

                if (HasReachedDestination(context))
                {
                    Vector3 truePos = _shelterPoint.InteractionPosition;
                    float offDist = Vector3.Distance(context.Transform.position, truePos);

                    if (offDist <= ArrivedAtRealPointThreshold)
                    {
                        Arrive(context);
                    }
                    else if (offDist <= MaxManualApproachDistance)
                    {
                        // El NavMeshAgent ha llegado al borde del área caminable, pero el punto
                        // real sigue más adentro (típico bajo copas de árbol, ver
                        // NPCStateBase.BeginManualApproach): completar el resto a mano.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        float verticalDiff = truePos.y - context.Transform.position.y;
                        if (Mathf.Abs(verticalDiff) > 0.3f)
                        {
                            context.LogWarning($"[{StateName}] Aproximación manual a '{_shelterPoint.name}' con {verticalDiff:F2}m de diferencia vertical " +
                                $"(interactionPoint {(verticalDiff < 0 ? "por debajo" : "por encima")} del suelo del NPC) — revisar la altura Y del interactionPoint si el NPC se hunde/rebota al llegar.");
                        }
#endif
                        context.ShelterEdgePosition      = context.Transform.position;
                        context.HasShelterEdgePosition   = true;
                        context.ShelterEdgeOwnerCollider = _shelterPoint.OwnerCollider;
                        BeginManualApproach(context);
                        _manualApproachActive = true;
                    }
                    else
                    {
                        context.LogWarning($"[{StateName}] Punto de refugio a {offDist:F1}m del borde del NavMesh (máximo {MaxManualApproachDistance}m). Quedándose en el borde.");
                        Arrive(context);
                    }
                }
                return;
            }

            if (_isSitter) return; // sentado no hace nada más mientras dure la lluvia

            _nextGestureTimer -= Time.deltaTime;
            if (_nextGestureTimer <= 0f)
            {
                _nextGestureTimer = Random.Range(MinGestureInterval, MaxGestureInterval);

                // No interrumpir un gesto/variación de idle que ya esté en marcha.
                if (context.Animator != null && !context.Animator.IsPlayingAnimation())
                {
                    string gesture = ColdGestures[Random.Range(0, ColdGestures.Length)];
                    context.Animator.PlaySocialGesture(gesture);
                }
            }
        }

        public override void OnExit(NPCStateContext context)
        {
            if (_manualApproachActive)
            {
                EndManualApproach(context);
                _manualApproachActive = false;
            }

            if (_shelterPoint != null)
                _shelterPoint.Release(context.Transform);

            if (_isSitter)
                context.Animator?.StopAmbientActivity(NPCAmbientActivity.SitGround);

            context.CurrentShelter = null;

            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic)        return new CinematicState();
            if (context.IsInCombat)           return new CombatState();
            if (context.WasDefeatedInCombat)  return new DeadState();
            if (context.IsInteracting)        return new IdleState();

            if (!context.ShouldSeekShelter)
                return new ReturnFromShelterState();

            if (!_arrived && !_manualApproachActive && IsPathBlocked(context))
                return new IdleState();

            return null;
        }

        private void Arrive(NPCStateContext context)
        {
            if (!_shelterPoint.TryOccupy(context.Transform))
            {
                if (_manualApproachActive)
                {
                    EndManualApproach(context);
                    _manualApproachActive = false;
                }
                // Otro NPC ocupó el punto justo antes de que llegáramos: buscar otro desde aquí.
                context.Brain?.ChangeState(new SeekShelterState());
                return;
            }

            _arrived = true;
            context.CurrentShelter = _shelterPoint;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Diagnóstico (14 ago 2026): Arrive() no dejaba ningún rastro en consola al llegar,
            // así que "entra en SeekShelter y sale enseguida sin refugiarse" (p.ej. porque dejó de
            // llover a mitad de camino, ShouldSeekShelter pasa a false y CheckTransitions manda
            // directo a ReturnFromShelterState) era indistinguible de "llegó bien y se quedó poco
            // porque la lluvia paró justo después". Este log deja claro cuándo SÍ se llegó.
            context.Log($"[{StateName}] Llegó a refugio '{_shelterPoint.name}' ({_shelterPoint.shelterType}).");
#endif

            if (_manualApproachActive)
            {
                // FIX INC-NPCS-EN-ARBOLES (14 ago 2026): NO llamar EndManualApproach aquí. El
                // punto real está fuera del NavMesh a propósito (bajo la copa del árbol, ver
                // cabecera de BeginManualApproach en NPCStateBase). EndManualApproach pone
                // agent.updatePosition = true, y NavMeshAgentUtility.HardStop (llamado justo
                // después por StopMovement) hacía además agent.nextPosition = transform.position
                // incondicionalmente — con el NPC posicionado fuera del NavMesh, el propio
                // NavMeshAgent reproyecta esa posición sobre la malla en cuanto puede y, al
                // recuperar el control del transform, lo arrastra de vuelta a ese punto proyectado:
                // visualmente, el NPC se "escupe" fuera del árbol justo al llegar (bug reportado
                // por Raúl: "se hunden en el suelo y automáticamente se escupen hacia afuera").
                // Mientras esté aquí sentado/de pie esperando a que pare de llover no hace falta
                // el NavMeshAgent para nada — se deja el transform bajo control manual (congelado,
                // sin más llamadas a ManualApproachStep ya que _arrived corta ese camino en
                // OnUpdate) y solo se reactiva el agente en ReturnFromShelterState, que ya hace su
                // propio BeginManualApproach antes de necesitarlo — sin hueco intermedio con
                // updatePosition=true que pueda disparar el mismo salto.
                context.Animator?.ResetMovement();
            }
            else
            {
                StopMovement(context);
            }

            if (_shelterPoint.overrideFacing)
                context.Transform.rotation = _shelterPoint.InteractionRotation;

            _isSitter = Random.value < SitChance;

            if (_isSitter)
            {
                context.Animator?.PlayAmbientActivity(NPCAmbientActivity.SitGround);
            }
            else
            {
                context.Animator?.TransitionToIdle();
            }

            // Escalonar el primer gesto para que no todos los NPCs de un mismo punto de refugio
            // tiemblen de frío exactamente a la vez nada más llegar.
            _nextGestureTimer = Random.Range(MinGestureInterval, MaxGestureInterval);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnóstico añadido tras el aviso de Raúl (14 ago 2026) de que ningún NPC del pueblo
        // llegaba a los NPCShelterPoint recién recolocados bajo GO con techo: antes de este log,
        // "no hay refugio libre en rango" era un fallo silencioso (se volvía a IdleState sin
        // ningún rastro en consola), indistinguible de "no estaba lloviendo" o cualquier otro
        // motivo. Recorre TODOS los puntos registrados (ignora maxDist/capacity, a diferencia de
        // TryFindNearest) para poder decir exactamente por qué ninguno sirvió: o no hay ninguno en
        // la escena, o el más cercano está fuera de MaxSearchDistance, o está lleno.
        private static void LogNoShelterFound(NPCStateContext context)
        {
            float bestDist = float.MaxValue;
            NPCShelterPoint nearest = null;
            foreach (var sp in NPCShelterPoint.All)
            {
                if (sp == null) continue;
                float d = Vector3.Distance(context.Transform.position, sp.InteractionPosition);
                if (d < bestDist) { bestDist = d; nearest = sp; }
            }

            if (nearest == null)
            {
                context.LogWarning($"[{nameof(SeekShelterState)}] No hay ningún NPCShelterPoint registrado en la escena.");
                return;
            }

            context.LogWarning($"[{nameof(SeekShelterState)}] Ningún refugio libre a menos de {MaxSearchDistance}m. " +
                $"El más cercano ('{nearest.name}', {nearest.shelterType}) está a {bestDist:F1}m" +
                (nearest.IsFull ? " y además está lleno." : "."));
        }
#endif
    }
}

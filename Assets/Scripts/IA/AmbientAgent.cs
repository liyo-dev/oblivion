// Cambios clave dentro de tu AmbientAgent:

using UnityEngine;

[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
public class AmbientAgent : MonoBehaviour
{
    public AmbientSchedule schedule;
    public float wanderRadius = 12f;
    public float minWaitAtIdle = 1.5f;
    public float maxWaitAtIdle = 3.5f;
    public float searchRadius = 18f;
    public LayerMask spotMask = ~0;
    public string[] preferredTags;

    UnityEngine.AI.NavMeshAgent _agent;
    IAmbientAnim _anim;              // << usa el puente
    SmartSpot _currentSpot;
    float _useTimer;
    int _hourDebug = 10;

    enum State { Idle, Moving, UsingSpot, Wandering }
    State _state;

    void Awake()
    {
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _anim  = GetComponentInChildren<IAmbientAnim>();   // busca el bridge
    }

    void OnEnable(){ StartCoroutine(BrainLoop()); }

    void OnDisable() { StopAllCoroutines(); }

    System.Collections.IEnumerator BrainLoop()
    {
        yield return new WaitForSeconds(Random.Range(0f, 0.4f));
        _anim?.PlayIdle();

        // Repetir mientras el componente esté activo y habilitado; esto permite que la corrutina termine
        // cuando el comportamiento se desactiva, y evita un bucle estático aparentemente infinito.
        while (isActiveAndEnabled)
        {
            switch (_state)
            {
                case State.Idle:
                    yield return new WaitForSeconds(Random.Range(minWaitAtIdle, maxWaitAtIdle));
                    if (!TryGoToScheduleSpot()) StartWander();
                    break;

                case State.Moving:
                    _anim?.PlayWalk(Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.01f, _agent.speed)));
                    if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                    {
                        if (_currentSpot)
                        {
                            transform.rotation = _currentSpot.GetFacingRotation(transform.position);
                            _useTimer = _currentSpot.GetRandomUseTime();
                            _anim?.PlayPose(_currentSpot.pose);
                            _agent.isStopped = true;
                            _state = State.UsingSpot;
                        }
                        else { _anim?.PlayIdle(); _state = State.Idle; }
                    }
                    break;

                case State.UsingSpot:
                    _useTimer -= Time.deltaTime;
                    if (_useTimer <= 0f)
                    {
                        _currentSpot?.Release();
                        _currentSpot = null;
                        _agent.isStopped = false;
                        _anim?.ClearPose();
                        _state = State.Idle;
                    }
                    break;

                case State.Wandering:
                    _anim?.PlayWalk(1f);
                    if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
                    { _anim?.PlayIdle(); _state = State.Idle; }
                    break;
            }
            yield return null;
        }

        // Si salimos del bucle, la corrutina termina naturalmente al llegar al final del método.
    }

    // … (resto igual: SetHour, StartWander, TryGoToScheduleSpot, etc.)

    // Nuevo: método público que permite ajustar la hora desde CrowdDirector u otros
    public void SetHour(int h)
    {
        // Evitamos la advertencia comprobando si el valor cambia y usándolo en la comparación
        if (_hourDebug == h) return;
        _hourDebug = h;

        // Reevalúa si hay un spot programado para la nueva hora
        // Si encuentra uno, TryGoToScheduleSpot() debería iniciar el movimiento y devolver true.
        TryGoToScheduleSpot();
    }

    // Intención mínima: intentar ir a un spot según la agenda.
    // Actualmente actúa como stub seguro: si no hay agenda definida devuelve false.
    bool TryGoToScheduleSpot()
    {
        if (schedule == null) return false;
        // Aquí podrías buscar un SmartSpot compatible con "schedule" y preferredTags.
        // Implementación completa depende de la estructura de AmbientSchedule y SmartSpot.
        return false;
    }

    // Implementación básica de vagar: elige un punto aleatorio en un radio y lo usa como destino NavMesh.
    void StartWander()
    {
        if (_agent == null)
        {
            _state = State.Idle;
            return;
        }

        Vector3 randomPoint = Random.insideUnitSphere * wanderRadius + transform.position;
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(randomPoint, out hit, wanderRadius, UnityEngine.AI.NavMesh.AllAreas))
        {
            _currentSpot = null;
            _agent.isStopped = false;
            _agent.SetDestination(hit.position);
            _state = State.Wandering;
        }
        else
        {
            // Si no encuentra punto navegable, esperar un poco y volver a intentar
            _agent.isStopped = true;
            _state = State.Idle;
        }
    }
}

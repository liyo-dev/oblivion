using UnityEngine;

/// <summary>
/// Head-look procedural estilo IK para diálogos: gira la CABEZA (y parte del cuello) del
/// personaje hacia un objetivo, por encima de la animación en curso, sin necesitar Animation
/// Rigging ni assets adicionales. Es la capa fina que los AAA ponen encima del giro de cuerpo:
/// la cabeza anticipa y "escucha" mientras el cuerpo rota más lento por su lado.
///
/// Funcionamiento:
/// - Se añade en runtime (DialogueCinematicController.SetHeadLook) al root del personaje.
/// - En Awake localiza los huesos: primero vía Animator humanoide (GetBoneTransform), y si no,
///   por nombre exacto "head" / "neck_01" (convención del rig POLYGON usado por todos los
///   personajes del proyecto — comprobado en _LIAM.prefab y Eldran.prefab).
/// - En LateUpdate (después de que el Animator escriba la pose) aplica una rotación de mundo
///   repartida cuello+cabeza hacia el objetivo, con límites de ángulo (nada de giros de
///   exorcista: si el objetivo queda fuera de rango, el peso hace fade a 0 y manda el cuerpo).
/// - El peso hace fade in/out suave, y el componente se desactiva solo (coste cero) cuando
///   no tiene objetivo y el peso llega a 0.
///
/// Cumple las reglas del proyecto: huesos y buffers cacheados en Awake, sin allocs por frame,
/// sin reflection, logs bajo UNITY_EDITOR || DEVELOPMENT_BUILD.
/// </summary>
public class DialogueHeadLook : MonoBehaviour
{
    [Header("Límites de giro (relativos al forward del personaje)")]
    [Tooltip("Giro horizontal máximo de la cabeza en grados")]
    [SerializeField] private float maxYaw = 60f;
    [Tooltip("Inclinación vertical máxima de la cabeza en grados")]
    [SerializeField] private float maxPitch = 25f;
    [Tooltip("Margen extra sobre maxYaw a partir del cual se apaga el head-look (el cuerpo ya gira)")]
    [SerializeField] private float yawCutoffMargin = 35f;

    [Header("Suavizado")]
    [Tooltip("Velocidad del fade de peso (1/velocidad ≈ segundos de transición)")]
    [SerializeField] private float weightFadeSpeed = 3.5f;
    [Tooltip("Velocidad de persecución del objetivo (damping exponencial)")]
    [SerializeField] private float aimSpeed = 8f;

    [Header("Reparto cuello/cabeza")]
    [Tooltip("Fracción del giro que absorbe el cuello (el resto va a la cabeza)")]
    [SerializeField, Range(0f, 1f)] private float neckShare = 0.35f;

    [Header("Objetivo")]
    [Tooltip("Altura (m) sobre el root del objetivo a la que se mira si no tiene hueso head")]
    [SerializeField] private float targetHeightFallback = 1.2f;

    private Transform _target;
    private Transform _targetHead;   // hueso head del objetivo (si existe), para mirar a la cara
    private Transform _head;
    private Transform _neck;
    private float _weight;                              // peso actual (0..1)
    private Quaternion _smoothedTurn = Quaternion.identity; // giro suavizado acumulado

    private bool _bonesResolved;

    void Awake()
    {
        ResolveBones();
        // Dormido hasta que alguien le asigne un objetivo
        enabled = false;
    }

    /// <summary>
    /// Localiza los huesos de cabeza y cuello: Animator humanoide primero, nombre exacto después.
    /// Nombre EXACTO (ignorando mayúsculas) a propósito: los rigs del proyecto llaman al hueso
    /// "head", pero los prefabs también contienen mallas "Head01_Male", "HeadArmor02", etc. que
    /// un match parcial confundiría con el hueso.
    /// </summary>
    private void ResolveBones()
    {
        if (_bonesResolved) return;
        _bonesResolved = true;

        var animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        }

        if (_head == null || _neck == null)
        {
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (_head == null && string.Equals(n, "head", System.StringComparison.OrdinalIgnoreCase))
                    _head = all[i];
                else if (_neck == null && (string.Equals(n, "neck_01", System.StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(n, "neck", System.StringComparison.OrdinalIgnoreCase)))
                    _neck = all[i];

                if (_head != null && _neck != null) break;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_head == null)
            Debug.LogWarning($"[DialogueHeadLook:{name}] No se encontró hueso 'head' — head-look desactivado para este personaje");
#endif
    }

    /// <summary>
    /// Busca el hueso "head" del objetivo para mirarle a la cara (y no al pecho/root).
    /// Se resuelve una vez por cambio de objetivo, no por frame.
    /// </summary>
    private static Transform FindTargetHead(Transform target)
    {
        if (target == null) return null;

        var animator = target.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            var h = animator.GetBoneTransform(HumanBodyBones.Head);
            if (h != null) return h;
        }

        var all = target.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (string.Equals(all[i].name, "head", System.StringComparison.OrdinalIgnoreCase))
                return all[i];
        }
        return null;
    }

    /// <summary>
    /// Asigna el objetivo de mirada. null → fade out y el componente se duerme solo.
    /// </summary>
    public void SetTarget(Transform target)
    {
        if (target == transform) target = null; // nunca mirarse a uno mismo

        if (_target != target)
        {
            _target = target;
            _targetHead = FindTargetHead(target);
        }

        // Despertar si hay trabajo que hacer (aunque sea solo el fade out pendiente)
        if (_head != null && (_target != null || _weight > 0.001f))
            enabled = true;
    }

    void LateUpdate()
    {
        if (_head == null)
        {
            enabled = false;
            return;
        }

        // Delta de tiempo no escalado (con cap): el head-look sigue vivo aunque el diálogo
        // pause el juego con timeScale=0, y no da saltos tras un hitch de frame.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        // ── Calcular giro deseado hacia el objetivo, con límites ──
        float targetWeight = 0f;
        Quaternion desiredTurn = Quaternion.identity;

        if (_target != null)
        {
            Vector3 lookPoint = _targetHead != null
                ? _targetHead.position
                : _target.position + Vector3.up * targetHeightFallback;

            Vector3 baseFwd = transform.forward;
            baseFwd.y = 0f;

            Vector3 toTarget = lookPoint - _head.position;
            Vector3 toTargetFlat = new Vector3(toTarget.x, 0f, toTarget.z);

            if (baseFwd.sqrMagnitude > 0.0001f && toTargetFlat.sqrMagnitude > 0.01f)
            {
                baseFwd.Normalize();

                float yaw   = Vector3.SignedAngle(baseFwd, toTargetFlat.normalized, Vector3.up);
                float pitch = Mathf.Atan2(toTarget.y, toTargetFlat.magnitude) * Mathf.Rad2Deg;

                // Objetivo demasiado lateral/trasero: no torcer el cuello, que gire el cuerpo
                // (el peso hace fade a 0 en vez de cortar en seco)
                if (Mathf.Abs(yaw) <= maxYaw + yawCutoffMargin)
                {
                    float yawC   = Mathf.Clamp(yaw, -maxYaw, maxYaw);
                    float pitchC = Mathf.Clamp(pitch, -maxPitch, maxPitch);

                    Vector3 clampedDir = Quaternion.AngleAxis(yawC, Vector3.up) * baseFwd;
                    Vector3 rightAxis  = Vector3.Cross(Vector3.up, clampedDir);
                    clampedDir = Quaternion.AngleAxis(-pitchC, rightAxis) * clampedDir;

                    desiredTurn  = Quaternion.FromToRotation(baseFwd, clampedDir);
                    targetWeight = 1f;
                }
            }
        }

        // ── Suavizados ──
        _weight = Mathf.MoveTowards(_weight, targetWeight, weightFadeSpeed * dt);
        _smoothedTurn = Quaternion.Slerp(_smoothedTurn, desiredTurn, 1f - Mathf.Exp(-aimSpeed * dt));

        // Sin objetivo y fade terminado → dormir el componente (coste cero fuera de diálogos)
        if (_target == null && _weight <= 0.001f)
        {
            _weight = 0f;
            _smoothedTurn = Quaternion.identity;
            enabled = false;
            return;
        }

        // ── Aplicar sobre la pose animada (rotación de mundo pre-multiplicada) ──
        // Componer dos fracciones del mismo giro suma ángulos: cuello (neckShare) + cabeza
        // (resto) ≈ giro completo, repartido de forma natural por la columna.
        if (_neck != null && neckShare > 0.001f)
        {
            Quaternion neckTurn = Quaternion.Slerp(Quaternion.identity, _smoothedTurn, _weight * neckShare);
            _neck.rotation = neckTurn * _neck.rotation;

            Quaternion headTurn = Quaternion.Slerp(Quaternion.identity, _smoothedTurn, _weight * (1f - neckShare));
            _head.rotation = headTurn * _head.rotation;
        }
        else
        {
            Quaternion headTurn = Quaternion.Slerp(Quaternion.identity, _smoothedTurn, _weight);
            _head.rotation = headTurn * _head.rotation;
        }
    }
}

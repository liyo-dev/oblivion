/// <summary>
/// Puente estático mínimo para que scripts bajo Assets/Plugins (que Unity compila en el
/// ensamblado "first pass", ANTES que Assembly-CSharp) puedan saber si el jugador está en un
/// interior sin referenciar directamente a <c>EnvironmentController</c>.
///
/// EnvironmentController vive en Assets/Scripts (Assembly-CSharp), que compila DESPUÉS del
/// ensamblado de Plugins, así que un script de Plugins nunca puede ver ese tipo — de ahí el
/// error CS0103 "The name 'EnvironmentController' does not exist in the current context".
/// Assembly-CSharp sí puede referenciar tipos de Plugins (compila más tarde), así que
/// EnvironmentController actualiza <see cref="IsInterior"/> cada vez que cambia de modo, y
/// vThirdPersonCamera (en Plugins) solo lee este flag.
/// </summary>
public static class EnvironmentQuery
{
    public static bool IsInterior;

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsInterior = false;
    }
#endif
}

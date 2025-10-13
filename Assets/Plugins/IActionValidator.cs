/// <summary>
/// Interfaz para validar acciones del jugador sin depender de enums específicos.
/// Usada para comunicación entre diferentes assemblies/namespaces.
/// </summary>
public interface IActionValidator
{
    bool CanJump();
    bool CanSprint();
    bool CanAttack();
    bool CanCastMagic();
    bool CanInteract();
}

/// <summary>
/// Interfaz para el sistema de casting de magia.
/// Permite al vThirdPersonController (en Plugins) comunicarse con MagicCaster
/// sin crear dependencias entre assemblies.
/// </summary>
public interface IMagicCaster
{
    /// <summary>
    /// Intenta lanzar un hechizo por índice de slot
    /// </summary>
    /// <param name="slotIndex">0=Left, 1=Right, 2=Special</param>
    /// <returns>true si el casting fue exitoso</returns>
    bool TryCastSpell(int slotIndex);
}

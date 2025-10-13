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


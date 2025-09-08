public interface IInputService : IGameService
{
    UnityEngine.Vector2 Move { get; }
    bool AttackPhysicalTriggered { get; }
    bool AttackMagicTriggered { get; }
    bool JumpTriggered { get; }
}

public class InputService : IInputService, ITickable
{
    PlayerControls _controls;
    public UnityEngine.Vector2 Move { get; private set; }
    public bool AttackPhysicalTriggered { get; private set; }
    public bool AttackMagicTriggered { get; private set; }
    public bool JumpTriggered { get; private set; }

    public void Initialize()
    {
        _controls = new PlayerControls();
        _controls.Enable();
        ServiceLocator.Get<ITickManager>().Register(this);
    }

    public void Dispose()
    {
        _controls?.Disable();
        ServiceLocator.Get<ITickManager>().Unregister(this);
    }

    public void Tick(float dt)
    {
        Move = _controls.GamePlay.Move.ReadValue<UnityEngine.Vector2>();
        AttackPhysicalTriggered = _controls.GamePlay.AttackPhysical.triggered;
        AttackMagicTriggered = _controls.GamePlay.AttackMagic.triggered;
        JumpTriggered = _controls.GamePlay.Jump.triggered;
    }
}
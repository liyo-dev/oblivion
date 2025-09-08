public interface IGameService
{
    void Initialize();
    void Dispose(); 
}

public interface ITickable
{
    void Tick(float deltaTime);
}

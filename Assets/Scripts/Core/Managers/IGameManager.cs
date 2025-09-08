public enum GameState { Boot, Title, Playing, Paused }

public interface IGameManager : IGameService
{
    GameState State { get; }
    void SetState(GameState state);
}

public class GameManager : IGameManager
{
    private readonly ProjectConfig _cfg;
    public GameState State { get; private set; } = GameState.Boot;

    public GameManager(ProjectConfig cfg) => _cfg = cfg;

    public void Initialize() { }
    public void Dispose() { }

    public void SetState(GameState state)
    {
        State = state;
    }
}
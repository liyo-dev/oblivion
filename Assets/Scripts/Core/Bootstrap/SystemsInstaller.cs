using System.Collections.Generic;

public class SystemsInstaller
{
    private readonly ProjectConfig _cfg;

    public SystemsInstaller(ProjectConfig cfg) => _cfg = cfg;

    public void InstallAll()
    {
        // 1) Núcleo
        //Register(new SceneLoader());
        //Register(new TickManager());
        //Register(new GameManager(_cfg));
        //Register(new TimeService());
        //Register(new InputService());
        //Register(new AudioManager(_cfg));
        //Register(new SaveManager());
        //Register(new UIManager());
        //Register(new DialogueService());
        //Register(new InventoryService());
        //Register(new LocalizationService());
        //Register(new QuestManager());
        //Register(new CombatDirector());

        // 2) Inicializar en orden (si alguno depende de otro, respeta aquí el orden)
        InitializeAll();
    }

    private readonly List<IGameService> _ordered = new();

    private void Register<T>(T service) where T : IGameService
    {
        ServiceLocator.Register<T>(service);
        _ordered.Add(service);
    }

    private void InitializeAll()
    {
        foreach (var s in _ordered)
            s.Initialize();
    }
}
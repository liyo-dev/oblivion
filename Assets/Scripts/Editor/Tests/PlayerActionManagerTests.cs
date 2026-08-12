using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests de EditMode para el refcount de PushMode/PopMode de PlayerActionManager.
/// Añadido en la auditoría de seguimiento del 12 de agosto de 2026 (TDD.md §19.2/§19.4),
/// que recomendaba precisamente este escenario como primer test del proyecto: reproducir
/// el bug C2 (auditoría 2026-08-07, TDD.md §19.1) como red de seguridad permanente contra
/// una regresión futura, no solo confiar en el comentario "FIX C2" del código.
///
/// Vive en una carpeta Editor sin .asmdef propio a propósito: el proyecto no tiene ningún
/// .asmdef para su propio código (TDD.md §19.2/§19.4 lo documenta como pendiente), así que
/// este archivo compila como parte del ensamblado implícito Assembly-CSharp-Editor, que ya
/// tiene referenciados UnityEditor.TestRunner/UnityEngine.TestRunner/nunit.framework — no
/// hace falta crear un ensamblado nuevo ni arriesgarse a romper la compilación del proyecto
/// por una referencia mal configurada.
/// </summary>
public class PlayerActionManagerTests
{
    private GameObject _go;
    private PlayerActionManager _mgr;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // ApplyTopMode() llama SIEMPRE a UpdatePlayerLock(), que en su primer acceso de
        // toda la sesión crea PlayerLockService.Instance (PlayerLockService.cs:28-34) y
        // llama DontDestroyOnLoad — método que solo es válido en Play mode. En Edit mode
        // lanza InvalidOperationException, pero _instance ya quedó asignado ANTES de esa
        // línea, así que el primer test de la sesión que toque Push/PopMode revienta y
        // todos los siguientes van bien (leen el _instance ya cacheado). Para no depender
        // del orden de ejecución de NUnit (no garantizado), "gastamos" aquí esa única
        // excepción esperada una sola vez, antes de que corra ningún test real.
        try { _ = PlayerLockService.Instance; }
        catch (System.InvalidOperationException) { /* esperado en Edit mode, ver arriba */ }
    }

    [SetUp]
    public void SetUp()
    {
        // RequireComponent(typeof(Animator)) añade el Animator automáticamente.
        _go = new GameObject("PlayerActionManagerTests_GO");
        _mgr = _go.AddComponent<PlayerActionManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
    }

    [Test]
    public void Top_EnEstadoInicial_EsDefault()
    {
        Assert.AreEqual(ActionMode.Default, _mgr.Top);
        Assert.AreEqual(1, _mgr.StackDepth);
    }

    [Test]
    public void PushMode_DosSistemasEmpujanElMismoModo_UnSoloPopNoLoQuita()
    {
        // Reproduce el escenario real del bug C2: DialogueManager y CinematicSequencerBase
        // empujando Cinematic casi a la vez. Antes del fix, "if (Top == mode) return;" en
        // PushMode descartaba el segundo Push, pero el segundo Pop sí borraba la única entrada
        // real de la pila -> el jugador recuperaba el control en mitad de la cinemática.
        _mgr.PushMode(ActionMode.Cinematic); // ej. DialogueManager
        _mgr.PushMode(ActionMode.Cinematic); // ej. CinematicSequencerBase, casi a la vez

        Assert.AreEqual(ActionMode.Cinematic, _mgr.Top);
        Assert.AreEqual(3, _mgr.StackDepth); // Default + 2x Cinematic

        _mgr.PopMode(ActionMode.Cinematic); // termina el diálogo, pero la cinemática sigue

        // Con el bug C2 esto habría vuelto a Default; con el fix, el segundo Push sigue vivo.
        Assert.AreEqual(ActionMode.Cinematic, _mgr.Top,
            "El jugador no debería recuperar el control mientras el segundo sistema siga activo (regresión de C2).");
        Assert.IsTrue(_mgr.IsInMode(ActionMode.Cinematic));
    }

    [Test]
    public void PopMode_CuandoVacíaLaPila_VuelveADefault()
    {
        _mgr.PushMode(ActionMode.Cinematic);
        _mgr.PopMode(ActionMode.Cinematic);

        Assert.AreEqual(ActionMode.Default, _mgr.Top);
        Assert.AreEqual(1, _mgr.StackDepth);
    }

    [Test]
    public void PopMode_ConModoQueNoEstaEnLaPila_NoAlteraElTop()
    {
        _mgr.PushMode(ActionMode.Stunned);
        _mgr.PopMode(ActionMode.Flying); // nunca se empujó -> no debe hacer nada

        Assert.AreEqual(ActionMode.Stunned, _mgr.Top);
        Assert.AreEqual(2, _mgr.StackDepth);
    }

    [Test]
    public void ResetToDefault_VacíaLaPilaDeGolpe()
    {
        // Regresión de A10: morir/revivir con modos apilados (Flying/Carrying/Cinematic/Stunned)
        // debe volver limpio a Default, no arrastrar la pila vieja al respawn.
        _mgr.PushMode(ActionMode.Flying);
        _mgr.PushMode(ActionMode.Stunned);

        _mgr.ResetToDefault();

        Assert.AreEqual(ActionMode.Default, _mgr.Top);
        Assert.AreEqual(1, _mgr.StackDepth);
        Assert.IsFalse(_mgr.IsInMode(ActionMode.Flying));
        Assert.IsFalse(_mgr.IsInMode(ActionMode.Stunned));
    }
}

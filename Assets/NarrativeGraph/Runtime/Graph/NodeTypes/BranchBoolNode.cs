using System;

// ROTO — no usar. Lee el valor del blackboard pero nunca lo usa para elegir una salida:
// siempre llama a onReadyToAdvance() sin más, así que "ramifica" exactamente igual sin
// importar el valor de la variable. Confirmado sin uso en ningún grafo del proyecto
// (Agosto 2026). Marcado [Obsolete] para que no aparezca en el menú "Añadir Nodo" del
// editor (NarrativeGraphWindow filtra tipos con este atributo) y así nadie lo arrastre
// pensando que bifurca de verdad. Si en el futuro hace falta una rama real por bool/estado,
// usar NarrativeRunner.ForceJumpToOutput(this, índice) desde Enter() en vez de Advance().
[Obsolete("BranchBoolNode no bifurca de verdad (siempre avanza). No usar — ver comentario de la clase.")]
[Serializable]
public sealed class BranchBoolNode : NarrativeNode
{
    public string variableName;
    public bool invert = false;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        bool val = false;
        try { if (ctx?.Blackboard != null) val = ctx.Blackboard.Get<bool>(variableName, false); } catch { }
        if (invert) val = !val;
        // Original implementation no-ops with val; keep behavior: simply advance
        onReadyToAdvance?.Invoke();
    }
}

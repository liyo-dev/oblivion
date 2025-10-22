using System;

[Serializable]
public sealed class BranchBoolNode : NarrativeNode
{
    public string blackboardKey;
    public bool defaultWhenMissing = false; // si no existe la clave, usa este valor

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Usa la sobrecarga Get(key, fallback) para evitar avisos
        bool v = ctx.Blackboard.Get(blackboardKey, defaultWhenMissing);

        // No llamamos a onReadyToAdvance: saltamos a la salida elegida
        // Convención: salida[0]=false, salida[1]=true
        ctx.Runner.ForceJumpToOutput(this, v ? 1 : 0);
    }
}
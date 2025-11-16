using System;

/// <summary>
/// Marca un nodo como punto seguro para guardar.
/// Los nodos sin este atributo pueden causar problemas si se guarda durante su ejecución.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SavePointAttribute : Attribute
{
    public string Description { get; }
    
    public SavePointAttribute(string description = "")
    {
        Description = description;
    }
}

/// <summary>
/// Marca un nodo como NO SEGURO para guardar.
/// El sistema debería advertir si se intenta guardar durante la ejecución de estos nodos.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class UnsafeForSaveAttribute : Attribute
{
    public string Reason { get; }
    
    public UnsafeForSaveAttribute(string reason = "")
    {
        Reason = reason;
    }
}

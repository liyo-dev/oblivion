using System;

/// <summary>
/// Entrada serializable que representa la pieza activa para una categoría del ModularAutoBuilder.
/// </summary>
[Serializable]
public struct AppearanceEntry
{
    public PartCat category;
    public string partName;
}

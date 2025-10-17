// filepath: c:\Users\luarb\dev\unity\Alex\Assets\Scripts\Player\PlayerAbilities.cs
using UnityEngine;

/// Clase simple que representa las abilities del jugador.
/// Separada en su propio archivo para evitar dependencias de clases anidadas entre archivos.
[System.Serializable]
public class PlayerAbilities
{
    [Tooltip("Permite nadar (Swimming)")] public bool swim;
    [Tooltip("Permite saltar")] public bool jump;
    [Tooltip("Permite trepar / escalar")] public bool climb;
    [Tooltip("Permite usar magia (casts)")] public bool magic = true;
}


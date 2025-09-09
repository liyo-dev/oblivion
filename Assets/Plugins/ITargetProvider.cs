using UnityEngine;

public interface ITargetProvider
{
    // Devuelve true si hay objetivo y lo entrega en 't'
    bool TryGetTarget(out Transform t);
}
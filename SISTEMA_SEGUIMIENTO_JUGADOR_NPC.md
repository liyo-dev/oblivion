# NPCQuestConfig - Sistema de Seguimiento Continuo del Jugador

## 🎯 Problema Resuelto

**Antes**: El NPC se rotaba hacia el jugador al iniciar la interacción, pero luego se quedaba quieto o volvía a su posición original durante el diálogo.

**Ahora**: El NPC **mira constantemente al jugador** durante todo el diálogo, siguiendo sus movimientos en tiempo real.

## 🔧 Implementación

### Sistema de Seguimiento Continuo

Se ha implementado un sistema de corrutinas que mantiene al NPC mirando al jugador durante todo el diálogo:

```csharp
// Campos nuevos para tracking
[System.NonSerialized] private Coroutine _lookAtPlayerCoroutine;
[System.NonSerialized] private Transform _playerTransform;

// Configuración
public float rotationSpeed = 360f; // Velocidad de rotación suave
public bool keepLookingAtPlayerDuringDialogue = true; // Activar/desactivar
```

### Flujo de Funcionamiento

1. **Al iniciar interacción**:
   ```csharp
   RotateToPlayer() // Rotación inicial instantánea
   StartTalkingAnimation() // Animación de hablar
   ```

2. **Durante el diálogo**:
   ```csharp
   StartContinuousLookAtPlayer() // Inicia corrutina de seguimiento
   // El NPC rota suavemente cada frame hacia el jugador
   ```

3. **Al terminar el diálogo**:
   ```csharp
   StopContinuousLookAtPlayer() // Detiene el seguimiento
   StopTalkingAnimation() // Detiene animación de hablar
   // El NPC se queda mirando donde terminó
   ```

### Corrutina de Seguimiento

```csharp
private IEnumerator ContinuousLookAtPlayerCoroutine(Transform npcTransform, Transform playerTransform)
{
    while (true)
    {
        // Calcular dirección hacia el jugador
        Vector3 directionToPlayer = playerTransform.position - npcTransform.position;
        directionToPlayer.y = 0; // Solo rotación horizontal
        
        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            
            // Rotar suavemente hacia el jugador
            npcTransform.rotation = Quaternion.RotateTowards(
                npcTransform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }
        
        yield return null;
    }
}
```

## ⚙️ Configuración en Inspector

### Campos de Behavior

- **Rotate To Player On Interact**: ✓ (activado)
  - El NPC se rota hacia el jugador al interactuar

- **Rotation Speed**: 360 (grados por segundo)
  - Velocidad de rotación durante el seguimiento
  - 360 = rotación rápida y suave
  - Valores más bajos = rotación más lenta

- **Keep Looking At Player During Dialogue**: ✓ (activado)
  - Mantiene al NPC mirando al jugador durante todo el diálogo
  - Si se desactiva, solo rota al inicio

## 🎮 Comportamiento

### Escenario Típico:

1. **Jugador se acerca al NPC**
2. **Jugador interactúa (E)**
   - NPC rota instantáneamente hacia el jugador
   - NPC inicia animación de hablar
3. **Diálogo comienza**
   - NPC empieza a seguir al jugador con la mirada
   - Si el jugador se mueve, el NPC lo sigue con rotación suave
4. **Jugador puede moverse durante el diálogo**
   - NPC sigue mirándolo todo el tiempo
5. **Diálogo termina**
   - NPC deja de seguir al jugador
   - NPC se queda en la última rotación
   - Animación de hablar se detiene

### Casos Especiales:

- **Jugador se aleja mucho**: NPC sigue rotando hacia él
- **Jugador corre alrededor**: NPC lo sigue suavemente
- **Diálogo largo**: NPC mantiene la mirada todo el tiempo
- **Múltiples diálogos**: Sistema se reinicia correctamente

## 📋 Integración con Otros Sistemas

### DialogueManager

El sistema se integra automáticamente con el callback del DialogueManager:

```csharp
dm.StartDialogue(dialogue, context.Transform, () => 
{
    // Callback automático cuando termina el diálogo
    StopContinuousLookAtPlayer(context);
    StopTalkingAnimation(context);
});
```

### Tipos de Diálogos

El seguimiento funciona con todos los tipos de diálogos:
- ✅ `dlgBefore` (offer dialogue)
- ✅ `dlgInProgress` (quest in progress)
- ✅ `dlgTurnIn` (quest turn-in)
- ✅ `dlgCompleted` (quest completed)

## 🔍 Debug

### Logs Importantes:

```
[NPCQuestConfig] Iniciado seguimiento continuo del jugador para [NPC_Name]
[NPCQuestConfig] Detenido seguimiento continuo del jugador para [NPC_Name]
```

### Verificación Visual:

- El NPC debe rotar suavemente hacia el jugador
- La rotación debe ser continua mientras el diálogo esté abierto
- Al cerrar el diálogo, el NPC debe dejar de rotar

## ⚡ Rendimiento

### Optimizaciones:

- **Corrutina eficiente**: Solo ejecuta cuando hay diálogo activo
- **Rotación suave**: Usa `RotateTowards` para interpolación eficiente
- **Solo plano horizontal**: `directionToPlayer.y = 0` evita cálculos innecesarios
- **Threshold de distancia**: `sqrMagnitude > 0.001f` evita cálculos cuando está cerca

### Costo:

- **Por frame durante diálogo**: 1 cálculo de dirección + 1 RotateTowards
- **Cuando no hay diálogo**: 0 (corrutina detenida)

## 🐛 Troubleshooting

### "El NPC no mira al jugador durante el diálogo"
- ✅ Verifica que `keepLookingAtPlayerDuringDialogue` esté activado
- ✅ Verifica que `rotateToPlayerOnInteract` esté activado
- ✅ Revisa los logs para ver si se inicia el seguimiento

### "El NPC rota demasiado rápido/lento"
- ✅ Ajusta `rotationSpeed` en el Inspector
- Valores recomendados: 180-720 grados/segundo

### "El NPC se queda mirando al jugador después del diálogo"
- ✅ Esto es intencional por diseño
- Si quieres que vuelva a su rotación original, tendrías que guardarla

### "El NPC no rota nada"
- ✅ Verifica que el NPC tenga un componente MonoBehaviour válido
- ✅ Verifica que `context.Transform` no sea null
- ✅ Revisa los logs de error

## 🎯 Resultado Final

✅ **NPC y jugador se miran constantemente durante el diálogo**  
✅ **Rotación suave y natural**  
✅ **Funciona con todos los tipos de diálogos**  
✅ **Sistema automático, no requiere configuración extra**  
✅ **Rendimiento optimizado**

---

**Fecha**: 2025-12-25  
**Estado**: ✅ COMPLETAMENTE FUNCIONAL  
**Impacto**: Interacciones NPC mucho más inmersivas y naturales


# 🎮 Minijuego "Pilla Pilla" (Tag) - Guía de Integración

## Descripción
Minijuego de persecución donde el jugador debe huir de un personaje durante 30 segundos.
Si el perseguidor atrapa al jugador, las posiciones se reinician y se continúa la cuenta.
Si el jugador sobrevive los 30 segundos, gana.

## Archivos Creados

| Archivo | Ubicación | Descripción |
|---------|-----------|-------------|
| `ChaserAI.cs` | `Assets/Scripts/Minigames/` | IA de persecución usando NavMeshAgent |
| `TagMinigameController.cs` | `Assets/Scripts/Minigames/` | Controlador principal del minijuego |
| `TagMinigameUI.cs` | `Assets/Scripts/Minigames/` | Componente de UI para mostrar temporizador |
| `StartTagMinigameNode.cs` | `Assets/NarrativeGraph/Runtime/Graph/NodeTypes/` | Nodo para el grafo narrativo |

---

## 📋 Configuración Paso a Paso

### 1. Crear el Prefab del Minijuego

1. **Crear GameObject vacío** llamado `TagMinigame`
2. **Añadir componente** `TagMinigameController`
3. Configurar:
   - `Minigame Id`: ID único (ej: `"TAG_MINIGAME_01"`)
   - `Duration`: 30 segundos
   - `Countdown Before Start`: 3 segundos

### 2. Configurar el Perseguidor

1. **Crear/Usar modelo de personaje** con Animator
2. **Añadir componentes**:
   - `NavMeshAgent`
   - `ChaserAI`
3. **Configurar ChaserAI**:
   - `Chase Speed`: 5 (ajustar según dificultad)
   - `Catch Distance`: 1.2m
   - `Run Anim Param`: nombre del parámetro de animación de correr
4. **Asignar** el perseguidor al campo `Chaser` del `TagMinigameController`

### 3. Crear Spawn Points

1. **Crear Empty** `PlayerSpawnPoint` → Posición inicial del jugador
2. **Crear Empty** `ChaserSpawnPoint` → Posición inicial del perseguidor
3. **Asignar** ambos al `TagMinigameController`

### 4. Configurar la UI

1. **Crear Canvas** (World Space o Screen Space)
2. **Añadir TextMeshPro**:
   - `TimerText`: Para mostrar "00:30"
   - `CountdownText`: Para mostrar "3, 2, 1..."
   - `MessageText`: Para mostrar "¡HUYE!" / "¡Te atraparon!"
3. **Añadir componente** `TagMinigameUI` (opcional, para lógica adicional)
4. **Asignar** los textos al `TagMinigameController`

### 5. NavMesh

Asegúrate de que la zona del minijuego tenga **NavMesh bakeado** para que el perseguidor pueda navegar.

---

## 🔗 Integración con el Grafo Narrativo

### Opción A: Usando el Nodo StartTagMinigameNode

1. En el editor del grafo narrativo, añadir un nodo `StartTagMinigameNode`
2. Configurar:
   - `Minigame Id`: Debe coincidir con el ID del `TagMinigameController`
   - `Activate On Start`: true (activa el GO si está inactivo)
   - `Deactivate On End`: true (opcional, oculta al terminar)
3. **Conectar** después del nodo de cinemática
4. El nodo **esperará automáticamente** a que el jugador gane para avanzar

```
[PlayTimelineNode] → [StartTagMinigameNode] → [DialogueNode "¡Lo lograste!"]
```

### Opción B: Usando Eventos Custom

Si prefieres más control, puedes usar `WaitCustomEventNode`:

1. Activar el minijuego manualmente (con `ActivateGameObjectNode`)
2. Usar `WaitCustomEventNode` con clave `MINIGAME_TAG_MINIGAME_01_WON`

```
[ActivateGameObjectNode] → [WaitCustomEventNode: "MINIGAME_TAG_MINIGAME_01_WON"] → [...]
```

### Opción C: Como GameObject Independiente

Si no quieres usar el grafo narrativo:

1. Mantén el prefab del minijuego en la escena (activo o inactivo)
2. Actívalo/inícialo desde otro script:
```csharp
minigameController.StartMinigame();
```
3. Suscríbete a los eventos:
```csharp
minigameController.OnMinigameWon.AddListener(() => {
    Debug.Log("¡El jugador ganó!");
    // Continuar con la narrativa...
});
```

---

## ⚙️ Parámetros Configurables

### ChaserAI

| Parámetro | Descripción | Valor Sugerido |
|-----------|-------------|----------------|
| `Chase Speed` | Velocidad de persecución | 4-6 |
| `Catch Distance` | Distancia para atrapar | 1.0-1.5m |
| `Update Path Interval` | Frecuencia de recálculo de ruta | 0.2s |

### TagMinigameController

| Parámetro | Descripción | Valor Sugerido |
|-----------|-------------|----------------|
| `Duration` | Duración total del minijuego | 30s |
| `Countdown Before Start` | Cuenta atrás inicial | 3s |
| `Start Message` | Mensaje al empezar | "¡HUYE!" |
| `Caught Message` | Mensaje al ser atrapado | "¡Te atraparon!" |
| `Win Message` | Mensaje al ganar | "¡Escapaste!" |

---

## 🎯 Eventos UnityEvent

El `TagMinigameController` expone estos eventos que puedes usar en el Inspector:

- `OnMinigameStarted`: Se dispara cuando empieza el juego (después del countdown)
- `OnMinigameWon`: Se dispara cuando el jugador sobrevive los 30 segundos
- `OnMinigameLost`: (No implementado - el minijuego no tiene "perder", solo reinicia)
- `OnPlayerCaught`: Se dispara cada vez que el jugador es atrapado

---

## 💡 Tips

1. **Ajusta la dificultad** modificando la velocidad del perseguidor vs la del jugador
2. **Añade obstáculos** para hacer la persecución más interesante
3. **Considera añadir power-ups** (velocidad temporal, escondites, etc.)
4. **El perseguidor puede usar cualquier modelo** - asigna el animator apropiado

---

## 🐛 Debugging

- Activa `Debug.Log` en los scripts para ver el flujo
- Usa los Gizmos de `ChaserAI` para ver el rango de captura
- Verifica que el NavMesh esté correctamente configurado
- Asegúrate de que el jugador tenga el tag "Player"

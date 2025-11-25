# Revisión del núcleo

## Persistencia y registro
- `PlayerService` ahora se registra explícitamente en `ServiceLocator` al inicializarse para cumplir con el requisito de que los servicios de la escena **Start** persistan y sean accesibles sin búsquedas globales.

## Oportunidades de optimización
- `EnvironmentController` usa `Resources.FindObjectsOfTypeAll<Light>` y un barrido sobre todas las cámaras al resolver interiores/exteriores. Conviene limitar estas búsquedas a eventos de cambio (por ejemplo, al entrar/salir de un interior) o cachear las luces direccionales relevantes para reducir el coste en escenas grandes.
- `WorldBootstrap` recurre a `Resources.FindObjectsOfTypeAll<GameObject>()` como *fallback* para localizar al jugador. Registrar el `Player` de forma proactiva (por ejemplo, desde `PlayerService.RegisterPlayer` en `Awake`) evita escaneos globales y hace el flujo de arranque más predecible.
- Varias búsquedas globales en servicios de UI (`DialogueManager`, `UiInputManager`, `SceneTransitionLoader`) pueden reemplazarse por inyección desde `ServiceLocator` ahora que los managers clave se registran en `Start`.

## Entradas
- `PlayerInputManager` centraliza correctamente los mapas de acción de Gameplay/UI. Los consumidores deberían seguir reutilizando `GetSharedOrNew` para evitar instancias duplicadas del asset de `PlayerControls`.
- `InputActionMapRouter` protege los cambios de mapa, pero conviene cachear `PlayerInput` desde el `ServiceLocator` para evitar búsquedas repetidas en secuencias de arranque.

## Scripts a auditar por coste en runtime
- Componentes de entorno y teletransporte realizan búsquedas globales (`EnvironmentController`, `TeleportService`, `SpawnAnchor`). Revisar que se ejecuten sólo en arranques o eventos discretos.
- Efectos VFX y utilidades con `Update()` frecuentes pueden concentrarse en un gestor o deshabilitarse cuando no son visibles para minimizar coste de CPU.

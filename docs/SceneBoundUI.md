# SceneBoundUI: uso y configuración

El componente `SceneBoundUI` permite que los menús que están dentro del prefab del jugador se inicialicen en la escena **Start**, persistan entre cargas y solo se activen en las escenas de gameplay.

## Pasos mínimos para que funcione
1. **Mueve los menús a la escena Start**
   - Arrastra los canvases (HUD, misiones/QuestLogListUI, Damage Overlay, Collectibles, PlayerEquipment si aplica) desde el prefab del jugador a la jerarquía de la escena Start. No es necesario que sigan como hijos del jugador porque el componente puede desprenderlos.

2. **Añade el componente `SceneBoundUI` a cada canvas que deba persistir**
   - Selecciona el objeto del canvas y usa *Add Component → SceneBoundUI*.
   - Opcional: define un **Unique Id** si hay duplicados con el mismo nombre para evitar instancias extra.

3. **Configura en qué escenas se activará cada menú**
   - En el campo **Allowed Scenes** agrega los nombres exactos de las escenas de gameplay (por ejemplo: `MainWorld`, `Test`, `Level01`).
   - Deja **Allow When List Empty** en `false` si quieres que se desactive en escenas que no estén listadas (menús globales pueden dejarlo en `true`).

4. **Decide si debe persistir y desprenderse del padre**
   - **Persist Across Scenes** debe permanecer activado para que el menú sobreviva a los cambios de escena desde Start.
   - **Detach From Parent** permite que el canvas se desprenda del jugador al iniciar; mantenlo activado si lo has sacado del prefab.

5. **Prueba la carga de escenas**
   - Inicia el juego en la escena Start, cambia a una escena de gameplay y comprueba que los menús de gameplay se activen. Al volver a Start deberían desactivarse si no están permitidos allí.

## Notas
- Solo se mantendrá viva una instancia por `Unique Id` o por nombre del GameObject; las duplicadas se destruirán automáticamente.
- Si no se especifican escenas y `Allow When List Empty` está activado, el menú permanecerá activo en todas las escenas.

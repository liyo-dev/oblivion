# Configuración de UI de habilidades en Unity

Esta guía resume cómo configurar el _Ability Unlock Popup_ y el panel de habilidades del menú de equipo usando los scripts actuales.

## Popup de desbloqueo (`AbilityUnlockPopupUI`)
1. **Añade el componente** `AbilityUnlockPopupUI` a un GameObject de UI (por ejemplo, dentro de tu Canvas principal).
2. **Asigna referencias de UI** en el inspector:
   - **Popup Root** (`popupRoot`): el contenedor completo del popup. Debe arrancar **desactivado**.
   - **Ability Title/Description**: `TextMeshProUGUI` para el nombre y la descripción.
   - **Ability Icon**: `Image` opcional para el ícono.
   - **Hold To Skip** (`holdToSkip`): referencia al `HoldToSkipUI` que ya usas en cinemáticas; se encenderá al mostrar el popup.
3. **Presentaciones de habilidades**:
   - El campo **Ability Key Presentations** (`abilityKeyPresentations`) es opcional; si lo dejas vacío, el script usa los presets por defecto para **Swim, Jump, Climb, Magic y Fly**.
   - Solo necesitas añadir entradas si quieres títulos, descripciones o íconos personalizados para esos `AbilityKey`.
4. **Prueba** desbloqueando una habilidad (por evento `UnlockService.OnAbilityUnlockedKey` o `OnAbilityUnlocked`) y verifica que el popup muestre el título/descripcion correctos.

## Panel de habilidades del menú de equipo (`PlayerEquipmentMenuController`)
1. En el prefab/escena del menú de equipo, selecciona el `PlayerEquipmentMenuController` y ubica el bloque **Habilidades** en el inspector.
2. **Root de habilidades** (`abilitiesRoot`): asigna el GameObject contenedor del panel (puede permanecer activo siempre).
3. **Entradas fijas** (`abilityEntries`): arrastra los GameObjects que representan cada habilidad a los campos `Swim`, `Jump`, `Climb`, `Magic` y `Fly`.
   - **Estado inicial**: pon **SetActive(false)** en cada entrada para que estén ocultas al inicio.
   - **Visual**: cada entrada puede ser cualquier elemento de UI (texto, ícono, etc.) ya diseñado en la escena.
4. El controlador activará cada entrada cuando la habilidad esté desbloqueada (se evalúa en `UpdateAbilitiesPanel`). No se instancian filas dinámicas, solo se habilitan/ocultan los objetos que ya referencies.
5. Si no asignas ninguna entrada, el panel de habilidades se ocultará automáticamente.

> Tip: Usa un prefab del panel de habilidades con todas las entradas ya colocadas y en `SetActive(false)` para replicarlo en otras escenas sin volver a configurarlo.

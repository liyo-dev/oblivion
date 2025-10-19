# Sistema de recogida de objetos

Este sistema permite que el jugador recoja monedas, fragmentos que restauran recursos y estrellas que cargan el ataque especial.

## Configuracion del jugador

- En el prefab o escena del jugador añade los siguientes componentes (si no estaban ya presentes):
  - `Inventory` (debe estar en el mismo `GameObject` que `PlayerPickupCollector`).
  - `PlayerPickupCollector` (nuevo script). Puedes activar `logWarnings` para depurar ausencias de referencias.
  - `SpecialChargeMeter` en el objeto que prefieras (por ejemplo junto al `ManaPool`). Ajusta `maxCharge` y `chargeRequiredToUse` para definir cuantos fragmentos hacen falta para el ataque especial.
- `PlayerPickupCollector` detecta automaticamente `PlayerHealthSystem`, `ManaPool` y `SpecialChargeMeter` en el arbol del jugador, pero puedes asignarlos a mano en el inspector si lo prefieres.
- `MagicCaster` ahora consume carga especial cuando usa el slot `Special`. Si no hay suficiente carga el hechizo especial no se podra lanzar.

## Configuracion de un pickup en escena

1. Crea un `GameObject` con un `Collider` en modo *Trigger* y añade el script `WorldPickup`.
2. En la lista **Effects** añade tantos elementos como necesites. Cada entrada usa `PickupEffect`:
   - **Currency**: precisa un `ItemData` y una cantidad. Suma al inventario.
   - **ManaRestore**: usa `amount` (o `quantity` si no se asigna) para rellenar `ManaPool`.
   - **HealthRestore**: cura al jugador mediante `PlayerHealthSystem`.
   - **SpecialCharge**: añade carga al `SpecialChargeMeter`.
   - Activa `consumeEvenIfNoChange` cuando quieras que el objeto desaparezca aunque el valor no haya podido aplicarse (por ejemplo, monedas con inventario ausente).
3. Configura los feedbacks opcionales:
   - `vfxPrefab`: se instancia al recoger.
   - `pickupSfx`: clip reproducido en `PlayClipAtPoint`.
   - `disableOnCollect`: objetos (malla, luz, etc.) que se desactivan tras recoger.
   - `destroyOnCollect`, `destroyDelay` y `deactivateRootOnCollect` controlan como se limpia el objeto.
   - Usa el `UnityEvent onCollected` para disparar logica adicional (por ejemplo abrir puertas).

## Flujo durante la partida

1. Al entrar en el trigger, `WorldPickup` busca el `PlayerPickupCollector` en el jugador.
2. Cada `PickupEffect` se aplica a traves del collector:
   - Monedas llaman a `Inventory.Add`.
   - Restauraciones usan `ManaPool.Refill` y `PlayerHealthSystem.Heal`.
   - Estrellas usan `SpecialChargeMeter.AddCharge`.
3. Si al menos un efecto se considera consumido, el pickup ejecuta los feedbacks y se destruye/desactiva segun la configuracion.

## Notas

- Todos los scripts se encuentran en `Assets/Scripts`: `Inventory` y `PlayerPickupCollector` bajo `Inventory`, `SpecialChargeMeter` en `Player`, `WorldPickup` en `World`.
- `PlayerService` cachea automaticamente `PlayerPickupCollector` y `SpecialChargeMeter`, por lo que pueden resolverse desde otros sistemas si es necesario.
- `SpecialChargeMeter` expone eventos de porcentaje y valor actual para enlazarlo a la UI facilmente.

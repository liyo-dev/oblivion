# NPCBehaviourManager

Sistema unificado para configurar NPCs con animaciones, ambientacion, misiones y retos sin scripts redundantes.

## Componentes obligatorios

Coloca los siguientes componentes en el GameObject del NPC (en este orden es recomendable):

1. `Animator` – usa el controlador que contiene tus estados (`Free Locomotion`, `Greeting01_NoWeapon`, `SenseSomethingSearching_NoWeapon`, etc.).
2. `NavMeshAgent` – ajusta radio, altura y velocidad segun tu NavMesh.
3. `NPCSimpleAnimator` – **siempre** presente para gestionar locomocion, saludos y transiciones.
4. `Interactable` – asigna el `DialogueAsset` principal; el manager intercepta la interaccion cuando procede.
5. `NPCBehaviourManager` – nuevo script que centraliza toda la configuracion (namespace `Alex.NPC`).

> Los scripts antiguos (`SimpleNPCWander`, `SimpleNPCCombat`, `SimpleQuestNPC`, `NPCAmbientBrain`) quedan obsoletos. El inspector mostrara un aviso `[Obsolete]` si aun los tienes en el prefab.

## Configuracion rapida

El inspector del manager esta organizado en tres modulos; activa unicamente los que necesites.

### Ambientacion
- **Enable Wander**: activa el vagabundeo automatico.
- **Wander Radius / Idle Times**: controla el tamano del area y las pausas aleatorias.
- **Pick While Moving**: si esta desactivado, el NPC termina el destino actual antes de elegir el siguiente.

### Misiones
- **Enable**: habilita la cadena de quests.
- **Chain**: lista de entradas `QuestChainEntry`. Cada entrada permite:
  - Seleccionar la `QuestData`.
  - Definir dialogos personalizados (`dlgBefore`, `dlgInProgress`, `dlgTurnIn`, `dlgCompleted`).
  - Escoger el modo de completado (`Manual`, `AutoCompleteOnTalk`, `CompleteOnTalkIfStepsReady`).
  - Activar la deteccion automatica de objetos (tag + indice de paso).
- **Item Detection**: configura radio, angulo y frecuencia de escaneo para las entradas que lo necesiten.

### Reto / Combate
- **Enable**: marca este bloque para que el NPC detecte al jugador y dispare el reto.
- **Sight Radius / FOV**: distancia y cono de vision para activar el flujo.
- **Challenge Stop Distance**: a que distancia se planta el NPC antes de hablar.
- **Alert / Challenge States**: nombres de los estados de animacion (opcional).
- **Exclamation Prefab**: icono que aparece sobre la cabeza durante la sorpresa.
- **Lock Player During Challenge**: si se marca, invoca el evento `On Player Lock` cuando arranca el reto y `On Player Unlock` al terminar.
- **Eventos**:
  1. `On Challenge Started`: dispara tu logica de combate (habilitar IA, lanzar BattleManager, etc.).
  2. `On Player Lock` / `On Player Unlock`: conecta tu sistema de input si quieres congelar al jugador.
  3. `On Dialogue Request`: fallback en caso de no usar `Interactable` (texto plano).

## Eventos del Interactable

No es necesario anadir listeners manuales; el manager intercepta la interaccion y decide si la consume (misiones) o deja que el `Interactable` abra el dialogo asignado (ambiental/combate).

## Migracion de NPCs existentes

1. Duplica el prefab por seguridad.
2. Elimina `SimpleNPCWander`, `SimpleQuestNPC`, `SimpleNPCCombat` y `NPCAmbientBrain` (apareceran con el atributo `[Obsolete]`).
3. Asegura que `NPCSimpleAnimator`, `NavMeshAgent` e `Interactable` permanecen configurados.
4. Anade `NPCBehaviourManager` y copia las configuraciones antiguas en los bloques correspondientes.
5. Si era un NPC retador, conecta `On Challenge Started` a la logica que inicia el combate.
6. Pulsa Play y prueba: los logs de debug (`Log Debug`) ayudan a verificar el flujo.

## Debug

Al marcar `Log Debug` se mostraran mensajes en consola con los pasos relevantes (deteccion del jugador, arranque de reto, consumo de misiones, etc.). Util para ajustar radios y dialogos.

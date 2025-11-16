# Sistema de Narrativa - Mejoras Implementadas

## 1. Persistencia Mejorada del Estado

### Problema Original
- Los grafos solo guardaban el GUID del nodo actual
- No se verificaba si eventos/quests ya habían ocurrido
- Al cargar, los nodos se re-ejecutaban innecesariamente

### Solución Implementada
- **WaitCustomEventNode**: Guarda en blackboard si el evento ya fue recibido (`__event_{eventKey}_received`)
- **StartQuestNode**: Guarda si la quest ya fue iniciada (`__quest_{questId}_started`)
- **WaitQuestCompleteNode**: Ya tenía verificación de quest completada
- Al recargar, estos nodos verifican el blackboard primero y avanzan automáticamente si ya ocurrió

### Beneficio
- ✅ No más eventos/quests duplicados
- ✅ El estado del grafo se preserva completamente
- ✅ Al cargar, el grafo continúa exactamente donde se quedó

---

## 2. Sistema de Validación (NarrativeGraphValidator)

### Características
- **Validación automática** al registrar grafos en el Hub
- **Detecta errores comunes**:
  - Falta de StartNode
  - Nodos huérfanos sin conexiones
  - GUIDs duplicados
  - Configuraciones incorrectas en nodos específicos

### Validaciones Específicas
- ✅ **WaitQuestNodes**: Verifica que tengan questId configurado
- ✅ **WaitCustomEventNodes**: Lista eventos que el grafo espera
- ✅ **StartQuestNodes**: Detecta quests que se inician múltiples veces
- ✅ **SavePoints**: Verifica que hay nodos seguros para guardar

### Uso
```csharp
// Se ejecuta automáticamente al iniciar el juego
var validation = NarrativeGraphValidator.ValidateGraph(graph);
validation.LogResults("Mi Grafo");
```

### Salida de Ejemplo
```
[NarrativeGraphValidator] ✅ Grafo 'Historia Principal' es válido
[NarrativeGraphValidator] ⚠️ Grafo 'Misiones' tiene 2 advertencia(s):
  • Hay 1 nodo(s) huérfano(s) sin conexiones: DialogueNode
  • Grafo espera 3 evento(s) custom: LETTER_START, START_Q_NIÑOPEZ, ALGAS_INVENTORY
```

---

## 3. Debugger Visual (NarrativeGraphDebugger)

### Características
- **Panel en pantalla** (F3 para mostrar/ocultar)
- **Estado en tiempo real** de todos los grafos
- **Información del blackboard**
- **Historial de nodos visitados**

### Qué Muestra
- ✅ Grafo activo y nodo actual
- ✅ Tipo de nodo y GUID
- ✅ Información específica:
  - Para WaitQuest: qué quest espera
  - Para WaitEvent: qué evento espera (✅ recibido / ⏳ pendiente)
  - Para StartQuest: si la quest fue iniciada
- ✅ Entradas del blackboard
- ✅ Historial de nodos visitados con timestamp

### Instalación
1. Añadir `NarrativeGraphDebugger` al GameObject con `NarrativeGraphHub`
2. Configurar opciones en el Inspector
3. Presionar F3 en juego para ver el panel

### Configuración
```csharp
[Header("Configuración")]
public bool showDebugPanel = true;      // Mostrar panel
public KeyCode toggleKey = KeyCode.F3;  // Tecla para toggle
public bool trackHistory = true;        // Registrar historial
public int maxHistoryEntries = 50;      // Máx entradas historial
```

---

## 4. Atributos [SavePoint] y [UnsafeForSave]

### Propósito
Marcar qué nodos son seguros para guardar la partida

### Uso
```csharp
[SavePoint("Seguro guardar mientras espera eventos")]
public sealed class WaitCustomEventNode : NarrativeNode { ... }

[UnsafeForSave("No guardar durante diálogo")]
public sealed class DialogueNode : NarrativeNode { ... }
```

### Nodos Marcados Como Seguros
- ✅ WaitCustomEventNode
- ✅ WaitQuestCompleteNode

### Validación
El validador cuenta y reporta cuántos nodos son seguros/inseguros

---

## 5. Mejoras en el NarrativeRunner

### Cambios
- `GoTo()` ahora guarda el GUID del nodo en el blackboard (`__currentNodeGuid`)
- `StartFromStartNode()` verifica si hay un nodo guardado y continúa desde ahí
- Logs más descriptivos con tipo de nodo

### Flujo de Guardado/Carga
```
GUARDAR:
1. GoTo(nodo) → Guarda GUID en blackboard
2. Nodo guarda su estado específico (evento recibido, quest iniciada, etc.)
3. CaptureBlackboards() exporta todo el blackboard
4. Se guarda en JSON

CARGAR:
1. RestoreBlackboards() importa blackboard desde JSON
2. StartFromStartNode() lee __currentNodeGuid
3. Si existe → GoTo(nodo guardado)
4. Nodo verifica su estado en blackboard
5. Si evento ya recibido → avanza automáticamente
```

---

## Cómo Usar las Mejoras

### Para Desarrolladores
1. **Añadir Debugger**: Componente en NarrativeGraphHub
2. **Presionar F3 en juego** para ver estado de grafos
3. **Revisar logs de validación** al iniciar el juego
4. **Marcar nuevos nodos** con [SavePoint] o [UnsafeForSave]

### Para Diseñadores
- El validador automáticamente reportará errores en la consola
- El debugger muestra en qué nodo está cada grafo
- Los grafos ahora guardan correctamente su progreso

### Testing de Save/Load
```
1. Iniciar partida
2. Avanzar en la narrativa (completar quest, recibir eventos)
3. Presionar F3 → verificar estado del grafo
4. Guardar partida
5. Cargar partida
6. Presionar F3 → verificar que el estado es el mismo
7. El grafo debe continuar desde el mismo nodo
```

---

## Arquitectura Final

```
NarrativeGraphHub (Singleton)
├── Validación automática al inicio
├── Grafos registrados con runners
├── CaptureBlackboards() → Guarda estado completo
└── RestoreBlackboards() → Restaura estado completo

NarrativeRunner (Por grafo)
├── Blackboard con:
│   ├── __currentNodeGuid (nodo actual)
│   ├── __event_{eventKey}_received (eventos recibidos)
│   ├── __quest_{questId}_started (quests iniciadas)
│   └── Variables custom del grafo
├── GoTo() guarda GUID al cambiar nodo
└── StartFromStartNode() continúa desde nodo guardado

Nodos
├── [SavePoint] → Seguros para guardar
├── [UnsafeForSave] → No guardar aquí
└── Verifican blackboard antes de ejecutarse

Debugger (Opcional)
└── Panel visual con estado en tiempo real

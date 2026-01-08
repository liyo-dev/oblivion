# Sistema de Magia de Levitación

## Descripción General
El sistema de levitación permite al jugador atraer NPCs mientras mantiene presionado el botón de magia (X o B), y repelerlos cuando lo suelta. Es un nuevo tipo de hechizo (`MagicKind.Levitation`) que funciona de forma diferente a los proyectiles y hechizos especiales.

## Componentes Implementados

### 1. MagicKind.Levitation (Identifiers.cs)
Nuevo tipo de hechizo añadido al enum `MagicKind`:
```csharp
public enum MagicKind
{
    Projectile,
    Special,
    Levitation  // NUEVO
}
```

### 2. Propiedades de Levitación en MagicSpellSO
Los hechizos de tipo Levitación tienen las siguientes propiedades configurables:
- `levitationRange`: Rango máximo de detección de NPCs
- `levitationAngle`: Ángulo del cono de detección (grados)
- `levitationPullForce`: Fuerza de atracción durante el hold
- `levitationPushForce`: Fuerza de repulsión al soltar
- `levitationHeight`: Altura de elevación del NPC
- `levitationLiftSpeed`: Velocidad de elevación
- `levitationTargetLayers`: Layers afectados

### 3. PlayerLevitationController (Player)
Nuevo componente que se añade al jugador para manejar la mecánica de levitación.

**Funcionalidad:**
- Detecta cuando el jugador mantiene presionado X o B con un hechizo de levitación equipado
- Busca NPCs en un cono frontal según la configuración del hechizo
- Inicia la animación de magia y la pausa en el frame de "preparación"
- Mientras mantiene el botón: atrae a los NPCs hacia el jugador
- Al soltar: continúa la animación y aplica repulsión a los NPCs

**Configuración del Inspector:**
- `holdPauseNormalizedTime`: Momento de la animación donde pausar (0-1)
- `detectionHeightOffset`: Offset vertical para el origen del cono

### 4. LevitationTarget (NPCs)
Componente que se añade a los NPCs que pueden ser afectados por levitación.

**Funcionalidad:**
- Reproduce la animación `LevelUp_NoWeapon` al ser levitado
- Primera parte: NPC se eleva mientras es atraído hacia el jugador
- Segunda parte: NPC hace voltereta y es repelido

**Configuración del Inspector:**
- `canBeLevitated`: Habilitar/deshabilitar la susceptibilidad
- `pullForceMultiplier`: Multiplicador de fuerza de atracción
- `pushForceMultiplier`: Multiplicador de fuerza de repulsión
- `levitationAnimState`: Estado de animación a usar
- `holdPauseNormalizedTime`: Momento de pausa en la animación
- `liftSpeed`: Velocidad de elevación
- `disableNavMeshDuringLevitation`: Desactivar navegación durante levitación
- `levitationDrag`: Drag del Rigidbody durante levitación

### 5. GamepadInputReader Additions
Nuevas propiedades estáticas para detectar botones mantenidos/soltados:
- `AttackMagicLeftHeld`: X mantenido
- `AttackMagicLeftReleased`: X soltado este frame
- `AttackMagicRightHeld`: B mantenido
- `AttackMagicRightReleased`: B soltado este frame

### 6. IMagicCaster Interface Addition
Nuevo método en la interfaz para identificar hechizos de levitación:
```csharp
bool IsLevitationSpell(int slotIndex);
```

## Flujo de Ejecución

### Inicio de Levitación (Press & Hold)
1. `PlayerLevitationController.CheckForLevitationStart()` detecta botón presionado
2. Verifica que el slot tiene un hechizo de levitación (`spell.kind == MagicKind.Levitation`)
3. Verifica permisos de cast (maná, cooldown, etc.)
4. Consume maná
5. Busca NPCs con `LevitationTarget` en el cono de detección
6. Reproduce animación de magia del jugador y la pausa
7. Notifica a cada NPC: `target.BeginLevitation()`
8. NPCs inician animación `LevelUp_NoWeapon` y se pausan a mitad

### Durante el Hold
1. `PlayerLevitationController.UpdateLevitation()` cada frame
2. NPCs son atraídos horizontalmente hacia el jugador
3. NPCs se elevan a la altura configurada
4. Animaciones permanecen pausadas

### Fin de Levitación (Release)
1. `PlayerLevitationController.CheckForRelease()` detecta botón soltado
2. `EndLevitation()` se ejecuta
3. Continúa animación del jugador (fase de "lanzar")
4. Notifica a cada NPC: `target.EndLevitation()`
5. NPCs reciben fuerza de repulsión + torque para voltereta
6. NPCs continúan animación hasta completar
7. NPCs restauran estado normal al aterrizar

## Configuración en Unity

### Para el Jugador
1. Añadir `PlayerLevitationController` al GameObject del jugador
2. Asignar referencias o dejar que se auto-detecten
3. Configurar parámetros de animación según el Animator Controller

### Para los NPCs
1. Añadir componente `LevitationTarget` a NPCs susceptibles
2. Asegurarse de que tienen `Rigidbody`
3. Configurar la animación y parámetros de física
4. Ajustar multiplicadores de fuerza si es necesario

### Para crear un hechizo de Levitación
1. Crear un nuevo `MagicSpellSO` (Create → Magic → Spell)
2. Configurar `kind = Levitation`
3. Ajustar propiedades de levitación (rango, fuerzas, altura, etc.)
4. Equipar en slot Left o Right (NO Special)

## Notas Técnicas

- Los hechizos de levitación SOLO funcionan en slots Left y Right
- El sistema usa reflexión para leer inputs desde el assembly principal
- `vThirdPersonController` ignora los inputs de levitación (los maneja `PlayerLevitationController`)
- `MagicCaster.TryCastSpell()` retorna `false` para hechizos de levitación
- **NO se necesitan cambios en el Animator Controller**: el sistema usa `animator.Play()` con `normalizedTime` para "pausar" las animaciones en un punto específico sin afectar `animator.speed`. Esto permite que la locomoción siga funcionando normalmente.

## Configuración de Animación

El sistema de animación funciona de la siguiente manera:

### Para el Player:
1. Usa los estados de magia existentes (`MagicLeft`, `MagicRight`) en la capa superior (`UpperBody`)
2. Al iniciar levitación, reproduce la animación y la mantiene en `holdPauseNormalizedTime` (por defecto 0.3 = 30% del clip)
3. Al soltar, continúa la animación desde ese punto hasta el final
4. Luego baja suavemente el peso de la capa superior

### Para los NPCs:
1. Usa la animación `LevelUp_NoWeapon` existente (configurable)
2. Primera mitad: NPC se eleva (animación pausada en `holdPauseNormalizedTime`)
3. Segunda mitad: NPC hace voltereta en el aire (al soltar el jugador)

**Valores recomendados de `holdPauseNormalizedTime`:**
- Player: `0.3` (30%) - El personaje tiene la mano echada hacia atrás
- NPC: `0.5` (50%) - El NPC está elevado a mitad de la animación


# Supresión de Warnings de Third-Party Assets

Este archivo documenta los warnings suprimidos de assets de terceros que no podemos/debemos modificar.

## Assets Afectados y Razón

### Sweet_Land (ithappy)
- **OffMeshLink obsoleto**: Asset antiguo, no actualizado por el autor
- **Solución**: Funciona correctamente, Unity mantiene compatibilidad

### 100BestEffectPack
- **SystemInfo.supportsImageEffects obsoleto**: API antigua
- **RenderTexture.MarkRestoreExpected obsoleto**: Ya no tiene efecto
- **Solución**: Efectos funcionan correctamente, warnings son cosméticos

### Travis Game Assets - Hit Impact Effects
- **Ya corregido**: Agregado `new` keyword

## Cómo Suprimir Warnings de Third-Party

### Opción 1: csc.rsp (Recomendado)
Crear archivo `Assets/csc.rsp` con:
```
-nowarn:CS0618,CS0108,CS0414
```

### Opción 2: Pragma por archivo
Para assets propios, agregar al inicio del archivo:
```csharp
#pragma warning disable CS0618 // Obsolete member
#pragma warning disable CS0414 // Field assigned but not used
```

### Opción 3: Assembly Definition
Crear Assembly Definition para third-party assets y configurar en Inspector.

## Decisión del Proyecto

**Status**: Warnings de third-party se mantienen como referencia.
**Razón**: 
- No modificar código de terceros
- Facilita actualizaciones futuras
- Los warnings no afectan funcionalidad

Para código propio del proyecto, siempre corregir los warnings.


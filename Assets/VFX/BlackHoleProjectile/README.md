# Black Hole Projectile (URP)

**Contenido:**
- `URP_BlackHoleProjectile.shader` (Unlit transparente con lensing + glow)
- `BlackHoleProjectileController.cs` (anima radio/distorsión/ruido)
- `Billboard.cs` (el quad mira siempre a cámara)
- `Noise_Perlin.png` (ruido gris en repeat)

## Instalación
1. Copia la carpeta `Assets/Oblivion/VFX/BlackHoleProjectile` dentro de tu proyecto Unity.
2. En tu `UniversalRendererData`, activa **Opaque Texture**.
3. Crea un **Material** con shader `URP/BlackHoleProjectile` y asígnale `Noise_Perlin.png` a `_NoiseTex`.
4. Pon el material en un **Quad** (añade `Billboard` si es un VFX 2D frente a cámara).
5. (Opcional) Añade `BlackHoleProjectileController` y referencia el `Rigidbody` del proyectil.

## Valores sugeridos (look demonio)
- `_GlowColor`: (0.55, 0.2, 1.0) púrpura infernal
- `_GlowIntensity`: 3.2
- `_CoreRadius`: 0.20
- `_EdgeWidth`: 0.14
- `_Distortion`: 0.14
- `_Chromatic`: 0.7
- `_NoiseAmount`: 0.4
- `_NoiseSpeed`: (0.2, 0.1)

## Nota SRP Batcher
Este shader no es compatible con **SRP Batcher** (usa tiempo y SceneColor). Es normal en efectos de pantalla. No afecta al resto del proyecto.

## Troubleshooting
- **Error `redefinition of _Time`**: ya está corregido (no se redeclara).
- **No distorsiona**: asegúrate de Opaque Texture activada y de usar el Renderer correcto.
- **Se mezcla con otras transparencias**: sube la Render Queue del material a 3050–3100.

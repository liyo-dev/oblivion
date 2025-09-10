# Oblivion

*[🇺🇸 English Version](README.md)*

Un juego de acción 3D desarrollado en Unity con sistemas de combate mágico y cuerpo a cuerpo en un mundo de fantasía.

## 🎮 Descripción General

Oblivion es un juego de acción en tercera persona que combina mecánicas de combate mágico y físico. Los jugadores navegan por entornos fantásticos usando una variedad de ataques, hechizos y habilidades para superar desafíos y enemigos.

## ✨ Características

### Sistema de Combate
- **Modos de Combate Duales**: Alterna entre ataques mágicos y físicos
- **Sistema de Magia**: 
  - Proyectiles de bolas de fuego con daño configurable y efectos de área
  - Sistema de auto-apuntado para mejorar la jugabilidad
  - Curvas de reducción de daño para mecánicas de área balanceadas
  - Ataques mágicos combinados con efectos mejorados
- **Combate Cuerpo a Cuerpo**: Sistema de ataque físico con detección de hitbox
- **Sistema de Daño**: Sistema de daño multicapa que soporta diferentes tipos de daño

### Mecánicas del Jugador
- **Controlador en Tercera Persona**: Movimiento suave del personaje y controles de cámara
- **Sistema de Input Avanzado**: Soporte completo para gamepad con controles personalizables
- **Opciones de Movimiento**: Caminar, correr, esprintar y movimiento lateral
- **Auto-Targeting**: Apuntado inteligente a enemigos para ataques proyectil

### Efectos Visuales
- **VFX de Fuego Procedural**: Efectos dinámicos de fuego y explosión
- **Efectos Mágicos**: Retroalimentación visual de lanzamiento de hechizos e impacto
- **Universal Render Pipeline**: Renderizado moderno con rendimiento optimizado

## 🎯 Controles

### Controles de Gamepad
- **Stick Izquierdo**: Movimiento
- **Stick Derecho**: Vista de cámara
- **Presión Stick Izquierdo**: Esprintar
- **Botón Sur (A/X)**: Saltar
- **Botón Oeste (X/Cuadrado)**: Ataque Físico
- **Botón Norte (Y/Triángulo)**: Ataque Mágico
- **Gatillo Izquierdo**: Movimiento Lateral

## 🛠️ Especificaciones Técnicas

### Requisitos
- **Versión de Unity**: 6000.2.2f1 (Unity 6)
- **Pipeline de Renderizado**: Universal Render Pipeline (URP)
- **Sistema de Input**: Unity Input System (Nuevo)
- **Plataforma**: Compatible con Windows/Mac/Linux

### Dependencias Clave
- **Invector 3rd Person Controller LITE**: Movimiento de personaje y controlador
- **Unity Input System**: Manejo moderno de inputs
- **URP**: Pipeline de renderizado
- **Visual Effect Graph**: Sistemas de partículas y VFX

## 🚀 Configuración e Instalación

### Prerequisitos
1. Unity 6000.2.2f1 o versión compatible
2. Paquete Universal Render Pipeline
3. Paquete Input System

### Pasos de Instalación
1. Clona el repositorio:
   ```bash
   git clone https://github.com/liyo-dev/oblivion.git
   ```

2. Abre Unity Hub y añade la carpeta del proyecto

3. Abre el proyecto en Unity (automáticamente importará los paquetes requeridos)

4. Abre la escena principal: `Assets/Scenes/Start.unity`

5. Presiona Play para probar el juego

### Estructura del Proyecto
```
Assets/
├── Art/                    # Assets de arte del juego
│   ├── Characters/         # Modelos y animaciones de personajes
│   ├── Enemies/           # Assets de enemigos
│   ├── Weapons/           # Modelos de armas
│   └── World/             # Assets de entorno y mundo
├── Prefab/                # Prefabs de objetos del juego
├── Scenes/                # Escenas del juego
│   ├── Start.unity        # Escena principal del juego
│   └── Main World/        # Escenas del mundo
├── Scripts/               # Scripts de C#
│   ├── Core/              # Sistemas centrales del juego
│   ├── Health/            # Sistema de daño y salud
│   ├── Player/            # Mecánicas del jugador
│   ├── VFX/               # Efectos visuales
│   └── Weapons/           # Sistemas de armas
├── Settings/              # Configuraciones del juego y renderizado
└── VFX/                   # Assets de efectos visuales
```

## 🎨 Assets y Créditos

### Assets de Terceros
- **Invector 3rd Person Controller LITE**: Sistema de controlador de personaje
- **Hovl Studio Procedural Fire**: VFX de fuego y explosiones
- **Fantasy Kingdom Pack**: Assets de mundo y entorno
- **Kevin Iglesias**: Assets de animación
- **Pixel Play**: Assets adicionales del juego

## 🔧 Desarrollo

### Arquitectura del Código
- **Diseño Modular**: Sistemas separados para combate, movimiento y efectos
- **Basado en Interfaces**: Usa interfaces como `IDamageable` para extensibilidad
- **Dirigido por Eventos**: El lanzamiento de magia y combate usan eventos de Unity
- **Basado en Componentes**: Sigue la arquitectura de componentes de Unity

### Scripts Principales
- `FireballProjectile.cs`: Maneja el comportamiento de proyectiles mágicos y daño de área
- `MagicProjectileSpawner.cs`: Gestiona el lanzamiento de hechizos y creación de proyectiles
- `PlayerControls.cs`: Integración del sistema de input (auto-generado)
- `PlayerTargeting.cs`: Sistema de auto-apuntado y targeting
- `Damageable.cs`: Gestión de salud y daño

### Detalles del Sistema de Magia
El sistema de magia incluye:
- Física de proyectiles configurable (velocidad, gravedad, tiempo de vida)
- Daño de área con curvas de reducción basadas en distancia
- Efectos visuales de impacto con auto-limpieza
- Optimización de detección de colisiones
- Prevención de daño multi-objetivo

## 🤝 Contribuyendo

1. Haz fork del repositorio
2. Crea una rama de característica (`git checkout -b feature/caracteristica-increible`)
3. Confirma tus cambios (`git commit -m 'Añade característica increíble'`)
4. Sube la rama (`git push origin feature/caracteristica-increible`)
5. Abre un Pull Request

## 📝 Licencia

Este proyecto usa varios assets de terceros. Por favor revisa las licencias individuales de cada asset en sus respectivas carpetas.

## 🐛 Problemas Conocidos

- Proyecto actualmente optimizado para input de gamepad
- Algunos assets pueden requerir versiones específicas de paquetes de Unity

## 📞 Soporte

Para preguntas o soporte, por favor abre un issue en el repositorio de GitHub.

---

**Construido con Unity 6 y ❤️**
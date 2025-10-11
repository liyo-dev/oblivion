# Alex Adventure

*[🇪🇸 Versión en Español](README_ES.md)*

A 3D action game built with Unity featuring magic and melee combat systems in a fantasy world.

## 🎮 Overview

Oblivion is a third-person action game that combines magical and physical combat mechanics. Players navigate through fantasy environments using a variety of attacks, spells, and abilities to overcome challenges and enemies.

## ✨ Features

### Combat System
- **Dual Combat Modes**: Switch between magical and physical attacks
- **Magic System**: 
  - Fireball projectiles with configurable damage and AOE effects
  - Auto-aim targeting system for enhanced gameplay
  - Damage falloff curves for balanced AOE mechanics
  - Combo magic attacks with enhanced effects
- **Melee Combat**: Physical attack system with hitbox detection
- **Damage System**: Multi-layered damage system supporting different damage types

### Player Mechanics
- **Third-Person Controller**: Smooth character movement and camera controls
- **Advanced Input System**: Full gamepad support with customizable controls
- **Movement Options**: Walking, running, sprinting, and strafing
- **Auto-Targeting**: Intelligent enemy targeting for projectile attacks

### Visual Effects
- **Procedural Fire VFX**: Dynamic fire and explosion effects
- **Magic Effects**: Spell casting and impact visual feedback
- **Universal Render Pipeline**: Modern rendering with optimized performance

## 🎯 Controls

### Gamepad Controls
- **Left Stick**: Movement
- **Right Stick**: Camera look
- **Left Stick Press**: Sprint
- **South Button (A/X)**: Jump
- **West Button (X/Square)**: Physical Attack
- **North Button (Y/Triangle)**: Magic Attack
- **Left Shoulder**: Strafe

## 🛠️ Technical Specifications

### Requirements
- **Unity Version**: 6000.2.2f1 (Unity 6)
- **Render Pipeline**: Universal Render Pipeline (URP)
- **Input System**: Unity Input System (New)
- **Platform**: Windows/Mac/Linux compatible

### Key Dependencies
- **Invector 3rd Person Controller LITE**: Character movement and controller
- **Unity Input System**: Modern input handling
- **URP**: Rendering pipeline
- **Visual Effect Graph**: Particle and VFX systems

## 🚀 Setup and Installation

### Prerequisites
1. Unity 6000.2.2f1 or compatible version
2. Universal Render Pipeline package
3. Input System package

### Installation Steps
1. Clone the repository:
   ```bash
   git clone https://github.com/liyo-dev/oblivion.git
   ```

2. Open Unity Hub and add the project folder

3. Open the project in Unity (it will automatically import required packages)

4. Open the main scene: `Assets/Scenes/Start.unity`

5. Press Play to test the game

### Project Structure
```
Assets/
├── Art/                    # Game art assets
│   ├── Characters/         # Character models and animations
│   ├── Enemies/           # Enemy assets
│   ├── Weapons/           # Weapon models
│   └── World/             # Environment and world assets
├── Prefab/                # Game object prefabs
├── Scenes/                # Game scenes
│   ├── Start.unity        # Main game scene
│   └── Main World/        # World scenes
├── Scripts/               # C# scripts
│   ├── Core/              # Core game systems
│   ├── Health/            # Damage and health system
│   ├── Player/            # Player mechanics
│   ├── VFX/               # Visual effects
│   └── Weapons/           # Weapon systems
├── Settings/              # Game and render settings
└── VFX/                   # Visual effects assets
```

## 🎨 Assets and Credits

### Third-Party Assets
- **Invector 3rd Person Controller LITE**: Character controller system
- **Hovl Studio Procedural Fire**: Fire and explosion VFX
- **Fantasy Kingdom Pack**: World and environment assets
- **Kevin Iglesias**: Animation assets
- **Pixel Play**: Additional game assets

## 🔧 Development

### Code Architecture
- **Modular Design**: Separated systems for combat, movement, and effects
- **Interface-Based**: Uses interfaces like `IDamageable` for extensibility
- **Event-Driven**: Magic casting and combat use Unity events
- **Component-Based**: Follows Unity's component architecture

### Key Scripts
- `FireballProjectile.cs`: Handles magic projectile behavior and AOE damage
- `MagicProjectileSpawner.cs`: Manages spell casting and projectile creation
- `PlayerControls.cs`: Input system integration (auto-generated)
- `PlayerTargeting.cs`: Auto-aim and targeting system
- `Damageable.cs`: Health and damage management

### Magic System Details
The magic system features:
- Configurable projectile physics (speed, gravity, lifetime)
- AOE damage with distance-based falloff curves
- Visual impact effects with auto-cleanup
- Collision detection optimization
- Multi-target damage prevention

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project uses various third-party assets. Please check individual asset licenses in their respective folders.

## 🐛 Known Issues

- Project currently optimized for gamepad input
- Some assets may require specific Unity package versions

## 📞 Support

For questions or support, please open an issue on the GitHub repository.

---

**Built with Unity 6 and ❤️**

# Contributing to Oblivion

Thank you for your interest in contributing to Oblivion! This document provides guidelines and information for contributors.

## 🚀 Getting Started

### Prerequisites
- Unity 6000.2.2f1 or later
- Git for version control
- Basic knowledge of C# and Unity development

### Development Setup
1. Fork the repository
2. Clone your fork locally
3. Open the project in Unity
4. Create a new branch for your feature: `git checkout -b feature/your-feature-name`

## 📋 Contribution Guidelines

### Code Style
- Follow Unity's C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public methods
- Keep methods small and focused on a single responsibility

### Script Organization
- Place scripts in appropriate folders under `Assets/Scripts/`
- Use namespaces when appropriate
- Follow the existing folder structure:
  - `Core/`: Essential game systems
  - `Player/`: Player-specific functionality
  - `Health/`: Damage and health systems
  - `Weapons/`: Weapon and combat systems
  - `VFX/`: Visual effects scripts

### Component Design
- Use interfaces for extensibility (follow `IDamageable` pattern)
- Prefer composition over inheritance
- Use Unity events for loose coupling between systems
- Make components configurable via Inspector when appropriate

## 🔧 Development Areas

### Current Systems
- **Combat System**: Magic and melee combat mechanics
- **Input System**: Gamepad and keyboard controls
- **Targeting System**: Auto-aim and enemy targeting
- **VFX System**: Particle effects and visual feedback
- **Damage System**: Health, damage calculation, and status effects

### Areas for Contribution
- **Enemy AI**: Behavior systems for different enemy types
- **Level Design**: New environments and challenges
- **Audio System**: Sound effects and music integration
- **UI/UX**: Menus, HUD, and user interface improvements
- **Optimization**: Performance improvements and mobile support
- **Documentation**: Code documentation and tutorials

## 🎮 Game Design Principles

### Combat Balance
- Magic attacks should have clear advantages and disadvantages
- Melee combat should feel responsive and impactful
- Enemy encounters should be challenging but fair

### Visual Design
- Maintain fantasy theme consistency
- Ensure VFX don't obscure gameplay
- Keep visual style cohesive across assets

### Performance
- Target 60 FPS on mid-range hardware
- Optimize for mobile platforms when possible
- Use object pooling for frequently spawned objects

## 🐛 Bug Reports

When reporting bugs, please include:
- Unity version and platform
- Steps to reproduce the issue
- Expected vs actual behavior
- Screenshots or video if applicable
- Console error messages

### Bug Report Template
```
**Platform**: (Windows/Mac/Linux/Mobile)
**Unity Version**: 
**Issue Description**: 
**Steps to Reproduce**:
1. 
2. 
3. 

**Expected Behavior**: 
**Actual Behavior**: 
**Additional Context**: 
```

## ✨ Feature Requests

When requesting features:
- Explain the use case and benefits
- Consider how it fits with existing systems
- Provide implementation suggestions if possible
- Discuss potential impact on performance

## 📝 Pull Request Process

1. **Update Documentation**: Update README or code comments as needed
2. **Test Your Changes**: Ensure your changes don't break existing functionality
3. **Follow Naming Conventions**: Use descriptive branch and commit names
4. **Small, Focused Changes**: Keep PRs focused on a single feature or fix
5. **Code Review**: Be responsive to feedback and suggestions

### PR Template
```
**What does this PR do?**
- 

**How to test**
1. 
2. 

**Screenshots** (if applicable)

**Checklist**
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Documentation updated
- [ ] Tested in Unity editor
- [ ] No console errors
```

## 🎯 Roadmap Areas

### Short Term
- Enemy AI improvements
- Audio system integration
- UI/UX enhancements
- Performance optimizations

### Long Term
- Multiplayer support
- Level editor tools
- Mobile platform support
- Steam integration

## 💬 Communication

- **Issues**: Use GitHub issues for bug reports and feature requests
- **Discussions**: Use GitHub discussions for general questions
- **Code Review**: Provide constructive feedback on pull requests

## 📚 Resources

### Unity Documentation
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/)
- [Universal Render Pipeline](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)
- [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)

### C# Conventions
- [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- [Unity C# Coding Standards](https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity)

## 🙏 Recognition

Contributors will be recognized in:
- GitHub contributors list
- In-game credits (for significant contributions)
- Release notes

Thank you for helping make Oblivion better! 🎮✨
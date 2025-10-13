# 📚 Documentación del Proyecto Alex Adventure

**Última actualización:** 2025-01-12

## 📋 Índice General

### 🎮 Información General
- [README - Descripción del Proyecto](README.md)
- [README_ES - Versión en Español](README_ES.md)
- [CONTRIBUTING - Guía de Contribución](CONTRIBUTING.md)

### 🏗️ Arquitectura y Sistemas Core
- [SISTEMA_JUEGO - Documentación Técnica Completa](SISTEMA_JUEGO.md)
  - Arquitectura General
  - Sistema de Localización
  - GameBootService y GameBootProfile
  - Sistema de Salud y Maná
  - Sistema de Spawn
  - Sistema de Interacciones
  - Sistema de UI y Feedback
  - Sistema de Save/Load

### 🌐 Sistema de Localización
- [LOCALIZACION - Guía Completa de Localización](docs/LOCALIZACION.md)
  - Estructura de archivos JSON
  - IDs de personajes, diálogos, quests e interacciones
  - Cómo agregar nuevas traducciones
  - Mapeo completo de todos los assets

### 🎯 Sistema de Misiones (Quests)
- [QUESTS - Sistema de Misiones Completo](docs/QUESTS.md)
  - SimpleQuestNPC - Cadenas de misiones
  - QuestManager y QuestData
  - Modos de completado automático
  - API completa y ejemplos de uso
  - Ejemplo: Misiones de Eldran

### 📝 Guías Específicas
- [Cómo crear una cadena de misiones](docs/QUESTS.md#crear-cadena-misiones)
- [Cómo agregar diálogos localizados](docs/LOCALIZACION.md#agregar-dialogos)
- [Cómo configurar un NPC con misiones](docs/QUESTS.md#configurar-npc)

---

## 🚀 Inicio Rápido

### Para Desarrolladores Nuevos
1. Lee el [README](README_ES.md) para entender el proyecto
2. Revisa [CONTRIBUTING](CONTRIBUTING.md) para las guías de código
3. Consulta [SISTEMA_JUEGO](SISTEMA_JUEGO.md) para arquitectura general

### Para Diseñadores de Contenido
1. [Guía de Localización](docs/LOCALIZACION.md) - Agregar textos en español/inglés
2. [Guía de Misiones](docs/QUESTS.md) - Crear y configurar quests
3. Usa los ejemplos de las misiones de Eldran como referencia

### Para QA y Testing
1. Revisa [SISTEMA_JUEGO](SISTEMA_JUEGO.md) para entender los sistemas
2. Consulta las guías específicas para probar cada feature

---

## 📂 Estructura de Documentación

```
/Alex/
├── README.md                          # Descripción general (EN)
├── README_ES.md                       # Descripción general (ES)
├── CONTRIBUTING.md                    # Guía de contribución
├── INDEX.md                           # Este archivo - Índice maestro
├── SISTEMA_JUEGO.md                   # Documentación técnica completa
└── docs/                              # Documentación organizada
    ├── LOCALIZACION.md                # Todo sobre localización
    └── QUESTS.md                      # Todo sobre sistema de misiones
```

---

## 🔄 Historial de Cambios

### 2025-01-12
- ✅ Consolidación de documentación
- ✅ Eliminación de archivos vacíos
- ✅ Creación de índice maestro
- ✅ Reorganización de información duplicada
- ✅ Sistema de localización completo documentado
- ✅ Sistema de misiones completo documentado

---

## 📞 Contacto y Soporte

Para preguntas sobre la documentación o el proyecto, consulta:
- Issues en el repositorio
- Documentación técnica en SISTEMA_JUEGO.md
- Guías específicas en /docs/

---

**Nota:** Esta documentación está en constante evolución. Siempre consulta la fecha de última actualización.


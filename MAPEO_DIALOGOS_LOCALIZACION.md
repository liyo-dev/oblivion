# Mapeo de Diálogos a IDs de Localización

Este documento muestra cómo actualizar cada DialogueAsset (.asset) para usar las nuevas traducciones.

## 📋 DG_Letter_Intro.asset
```yaml
lines:
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_LETTER_INTRO_01"
    text: "Alex ven a verme cuando despiertes."
    portrait: {fileID: 21300000, guid: 170c3148924dd1548874918d63b2c1bb, type: 3}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_LETTER_INTRO_02"
    text: "Te espero en la entrada del reino."
    portrait: {fileID: 0}
```

## 📋 DG_Boy_Pirate.asset
```yaml
lines:
  - speakerNameId: "CHAR_PIRATA"
    speakerName: "Pirata"
    textId: "DLG_PIRATE_01"
    text: "¿Ves este ojo?"
    portrait: {fileID: 21300000, guid: 170c3148924dd1548874918d63b2c1bb, type: 3}
    
  - speakerNameId: "CHAR_PIRATA"
    speakerName: "Pirata"
    textId: "DLG_PIRATE_02"
    text: "Me lo quitó un caridas..."
    portrait: {fileID: 0}
```

## 📋 DG_Eldran_Mision1.asset (TURNIN)
```yaml
lines:
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION1_01"
    text: "Ya estás aquí."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION1_02"
    text: "Te hice venir porque ayer escuché algo en el bosque."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION1_03"
    text: "Era tarde pero me quede preocupado..."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION1_04"
    text: "¿Te importa echar un ojo por si ves algo raro?"
    portrait: {fileID: 0}
```

## 📋 DG_Eldran_Mision1_Before.asset
```yaml
lines:
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION1_BEFORE_01"
    text: "¿Has leido la carta que te dejé en la mesita?"
    portrait: {fileID: 0}
```

## 📋 DG_Eldran_Mision1_Completed.asset
```yaml
lines:
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION1_COMPLETED_01"
    text: "Era algo que nunca había escuchado..."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION1_COMPLETED_02"
    text: "Lo mismo fue mi imaginación..."
    portrait: {fileID: 0}
```

## 📋 DG_Eldran_Mission2_Offer.asset ⭐ NUEVO
```yaml
lines:
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_OFFER_01"
    text: "Gracias por venir."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_OFFER_02"
    text: "He estado recogiendo frutas en el bosque..."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_OFFER_03"
    text: "Y ahora la caja pesa demasiado para que yo la traiga solo."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_OFFER_04"
    text: "¿Podrías ir al bosque a buscarla?"
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_OFFER_05"
    text: "Y luego traérmela aquí, por favor."
    portrait: {fileID: 0}
```

## 📋 DG_Eldran_Mission2_InProgress.asset ⭐ NUEVO
```yaml
lines:
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_INPROGRESS_01"
    text: "¿Encontraste la caja de frutas?"
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_INPROGRESS_02"
    text: "Debería estar en el bosque, búscala bien."
    portrait: {fileID: 0}
```

## 📋 DG_Eldran_Mission2_TurnIn.asset ⭐ NUEVO
```yaml
lines:
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_TURNIN_01"
    text: "¡Excelente! Conseguiste traer la caja."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_TURNIN_02"
    text: "Sabía que podía contar contigo."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_TURNIN_03"
    text: "Toma, esto es por tu ayuda."
    portrait: {fileID: 0}
```

## 📋 DG_Eldran_Mission2_Completed.asset ⭐ NUEVO
```yaml
lines:
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_COMPLETED_01"
    text: "Gracias de nuevo por traer esa pesada caja."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_ELDRAN"
    speakerName: "Eldran"
    textId: "DLG_ELDRAN_MISSION2_COMPLETED_02"
    text: "Las frutas están deliciosas."
    portrait: {fileID: 0}
```

## 📋 DG_Girl_1.asset
```yaml
lines:
  - speakerNameId: "CHAR_VERONICA"
    speakerName: "Verónica"
    textId: "DLG_VERONICA_01"
    text: "Hay una leyenda que dice..."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_VERONICA"
    speakerName: "Verónica"
    textId: "DLG_VERONICA_02"
    text: "que si pronuncias mi nombre tres veces"
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_VERONICA"
    speakerName: "Verónica"
    textId: "DLG_VERONICA_03"
    text: "delante de un espejo...."
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_VERONICA"
    speakerName: "Verónica"
    textId: "DLG_VERONICA_04"
    text: "¡Aparece un fantasma!"
    portrait: {fileID: 0}
    
  - speakerNameId: "CHAR_VERONICA"
    speakerName: "Verónica"
    textId: "DLG_VERONICA_05"
    text: "Mejor no lo pruebes..."
    portrait: {fileID: 0}
```

---

## 🔧 Cómo actualizar en Unity

### Opción 1: Manualmente en el Inspector
1. Abre cada DialogueAsset en Unity
2. Para cada línea, agrega los campos nuevos:
   - `Speaker Name Id` → El ID del personaje (ej: "CHAR_ELDRAN")
   - `Text Id` → El ID del texto (ej: "DLG_LETTER_INTRO_01")
3. Deja los campos `Speaker Name` y `Text` como fallback

### Opción 2: Editar el archivo .asset directamente
1. Cierra Unity
2. Edita los archivos .asset con un editor de texto
3. Agrega las líneas `speakerNameId:` y `textId:` según el mapeo de arriba
4. Abre Unity y deja que recompile

---

## ✅ Traducciones agregadas

### Español (dialogues_es.json)
- ✅ 8 personajes: Alex, Eldran, Pirata, Verónica, Anciano, Mercader, Guardia, Narrador
- ✅ **32 líneas de diálogo** completas (incluyendo Misión 2 de Eldran)
- ✅ Diálogos de Misión 1 de Eldran (4 líneas + before + completed)
- ✅ **Diálogos de Misión 2 de Eldran** (12 líneas nuevas):
  - DLG_ELDRAN_MISSION2_OFFER_01 a 05 (Oferta de quest)
  - DLG_ELDRAN_MISSION2_INPROGRESS_01 a 02 (Sin la caja)
  - DLG_ELDRAN_MISSION2_TURNIN_01 a 03 (Entrega)
  - DLG_ELDRAN_MISSION2_COMPLETED_01 a 02 (Completada)

### Inglés (dialogues_en.json)
- ✅ 8 personajes traducidos
- ✅ **32 líneas traducidas** al inglés
- ✅ Todos los diálogos de Misión 2 traducidos

### Español (quests_es.json)
- ✅ Estados de quest traducidos
- ✅ **QUEST_ELDRAN_MISSION1**: Nombre, descripción y pasos
- ✅ **QUEST_ELDRAN_MISSION2**: Nombre, descripción y pasos (2 pasos)

### Inglés (quests_en.json)
- ✅ Estados de quest traducidos
- ✅ **QUEST_ELDRAN_MISSION1**: Traducido completo
- ✅ **QUEST_ELDRAN_MISSION2**: Traducido completo

---

## 📝 IDs de Localización por Categoría

### Personajes (Characters)
```
CHAR_ALEX          → Alex
CHAR_ELDRAN        → Eldran
CHAR_PIRATA        → Pirata / Pirate
CHAR_VERONICA      → Verónica / Veronica
CHAR_ELDER         → Anciano / Elder
CHAR_MERCHANT      → Mercader / Merchant
CHAR_GUARD         → Guardia / Guard
CHAR_NARRATOR      → Narrador / Narrator
```

### Diálogos - Carta Intro
```
DLG_LETTER_INTRO_01
DLG_LETTER_INTRO_02
```

### Diálogos - Pirata
```
DLG_PIRATE_01
DLG_PIRATE_02
```

### Diálogos - Eldran Misión 1
```
DLG_ELDRAN_MISSION1_BEFORE_01
DLG_ELDRAN_MISSION1_01
DLG_ELDRAN_MISSION1_02
DLG_ELDRAN_MISSION1_03
DLG_ELDRAN_MISSION1_04
DLG_ELDRAN_MISSION1_COMPLETED_01
DLG_ELDRAN_MISSION1_COMPLETED_02
```

### Diálogos - Eldran Misión 2 ⭐ NUEVO
```
DLG_ELDRAN_MISSION2_OFFER_01       (Gracias por venir)
DLG_ELDRAN_MISSION2_OFFER_02       (He estado recogiendo frutas...)
DLG_ELDRAN_MISSION2_OFFER_03       (La caja pesa demasiado)
DLG_ELDRAN_MISSION2_OFFER_04       (¿Podrías ir al bosque?)
DLG_ELDRAN_MISSION2_OFFER_05       (Y luego traérmela)
DLG_ELDRAN_MISSION2_INPROGRESS_01  (¿Encontraste la caja?)
DLG_ELDRAN_MISSION2_INPROGRESS_02  (Debería estar en el bosque)
DLG_ELDRAN_MISSION2_TURNIN_01      (¡Excelente! Conseguiste traer la caja)
DLG_ELDRAN_MISSION2_TURNIN_02      (Sabía que podía contar contigo)
DLG_ELDRAN_MISSION2_TURNIN_03      (Toma, esto es por tu ayuda)
DLG_ELDRAN_MISSION2_COMPLETED_01   (Gracias de nuevo)
DLG_ELDRAN_MISSION2_COMPLETED_02   (Las frutas están deliciosas)
```

### Diálogos - Verónica
```
DLG_VERONICA_01
DLG_VERONICA_02
DLG_VERONICA_03
DLG_VERONICA_04
DLG_VERONICA_05
```

### Quests - Eldran Misión 1
```
QUEST_ELDRAN_MISSION1_NAME   → "Habla con Eldran" / "Talk to Eldran"
QUEST_ELDRAN_MISSION1_DESC   → Descripción completa
QUEST_ELDRAN_MISSION1_STEP1  → "Hablar con Eldran" / "Talk to Eldran"
```

### Quests - Eldran Misión 2 ⭐ NUEVO
```
QUEST_ELDRAN_MISSION2_NAME   → "Trae la caja de frutas" / "Bring the Fruit Crate"
QUEST_ELDRAN_MISSION2_DESC   → Descripción completa
QUEST_ELDRAN_MISSION2_STEP1  → "Hablar con Eldran" / "Talk to Eldran"
QUEST_ELDRAN_MISSION2_STEP2  → "Recoger la caja de frutas" / "Pick up the fruit crate"
```

---

## 📦 Archivos de Localización Actualizados

Todos los siguientes archivos JSON ya contienen las traducciones:

### ✅ Assets/Resources/Localization/dialogues_es.json
- 32 líneas de diálogo en español
- Incluye todos los diálogos de Misión 2

### ✅ Assets/Resources/Localization/dialogues_en.json
- 32 líneas de diálogo en inglés
- Incluye todos los diálogos de Misión 2

### ✅ Assets/Resources/Localization/quests_es.json
- Nombres y descripciones de ambas misiones
- Todos los pasos traducidos

### ✅ Assets/Resources/Localization/quests_en.json
- Nombres y descripciones de ambas misiones
- Todos los pasos traducidos

---

## 🎯 Resumen de Assets a Crear

### DialogueAssets existentes (actualizar IDs):
- [x] DG_Letter_Intro.asset
- [x] DG_Boy_Pirate.asset
- [x] DG_Eldran_Mision1.asset
- [x] DG_Eldran_Mision1_Before.asset
- [x] DG_Eldran_Mision1_Completed.asset
- [x] DG_Girl_1.asset

### DialogueAssets NUEVOS a crear para Misión 2:
- [ ] **DG_Eldran_Mission2_Offer.asset** (5 líneas)
- [ ] **DG_Eldran_Mission2_InProgress.asset** (2 líneas)
- [ ] **DG_Eldran_Mission2_TurnIn.asset** (3 líneas)
- [ ] **DG_Eldran_Mission2_Completed.asset** (2 líneas)

### QuestData a crear:
- [ ] **Q_ELDRAN_MISSION1.asset** (1 paso)
- [ ] **Q_ELDRAN_MISSION2.asset** (2 pasos)

---

## 📝 Notas importantes

1. **Los archivos .asset NO han sido modificados automáticamente** porque Unity puede tener problemas si se editan externamente mientras está abierto.

2. **Las traducciones están listas** en los archivos JSON, solo falta:
   - Actualizar los DialogueAssets existentes con los IDs
   - Crear los 4 DialogueAssets nuevos de Misión 2
   - Crear los 2 QuestData assets

3. **Recuerda activar** "Resolve With Localization Manager" en el DialogueManager para que las traducciones funcionen.

4. **Los textos fallback** (campos `speakerName` y `text`) se mantienen por compatibilidad.

5. **Sistema de modos de completado**:
   - Misión 1: `AutoCompleteOnTalk` (se completa al hablar)
   - Misión 2: `CompleteOnTalkIfStepsReady` (necesita recoger la caja primero)

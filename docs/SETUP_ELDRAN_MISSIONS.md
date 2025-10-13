# Configuración de Misiones de Eldran en Unity

## 📋 Resumen del Sistema

**Misión 1**: "Ve a hablar con Eldran" (se completa automáticamente al hablar)
**Misión 2**: "Trae la caja de frutas" (se completa al traer la caja y hablar)

---

## 🎮 PASO 1: Crear los QuestData Assets

### 1.1 Crear Misión 1
1. En Unity, clic derecho en la carpeta `Assets/Resources/Quests/` (o donde guardes las quests)
2. Create > Game > Quest
3. Nombrar: `Q_ELDRAN_MISSION1`
4. Configurar:
   ```
   Quest ID: ELDRAN_MISSION1
   
   Display Name Id: QUEST_ELDRAN_MISSION1_NAME
   Display Name: Habla con Eldran (fallback si no usa localización)
   
   Description Id: QUEST_ELDRAN_MISSION1_DESC
   Description: Ve a hablar con Eldran en la entrada del reino (fallback)
   
   Steps: (1 paso)
   [0] Description Id: QUEST_ELDRAN_MISSION1_STEP1
       Description: "Hablar con Eldran" (fallback)
       Condition ID: (vacío)
   ```

### 1.2 Crear Misión 2
1. Create > Game > Quest
2. Nombrar: `Q_ELDRAN_MISSION2`
3. Configurar:
   ```
   Quest ID: ELDRAN_MISSION2
   
   Display Name Id: QUEST_ELDRAN_MISSION2_NAME
   Display Name: Trae la caja de frutas (fallback)
   
   Description Id: QUEST_ELDRAN_MISSION2_DESC
   Description: Encuentra la caja de frutas en el bosque y llévasela a Eldran (fallback)
   
   Steps: (2 pasos)
   [0] Description Id: QUEST_ELDRAN_MISSION2_STEP1
       Description: "Hablar con Eldran" (fallback)
       Condition ID: (vacío)
       
   [1] Description Id: QUEST_ELDRAN_MISSION2_STEP2
       Description: "Recoger la caja de frutas" (fallback)
       Condition ID: GET_FRUIT_CRATE
   ```

**IMPORTANTE**: Usa los campos con "Id" para que el sistema de localización funcione automáticamente. Los campos sin "Id" son fallbacks si no hay sistema de localización o falla.

---

## 💬 PASO 2: Crear los DialogueAssets

### 2.1 Diálogos de Misión 1

#### DLG_ELDRAN_MISSION1_TURNIN
1. Create > Game > Dialogue
2. Nombrar: `DLG_ELDRAN_MISSION1_TURNIN`
3. Configurar líneas:
   ```
   Line 0:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION1_01
   - Portrait: (Retrato de Eldran si lo tienes)
   
   Line 1:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION1_02
   
   Line 2:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION1_03
   
   Line 3:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION1_04
   ```

**Textos (español):**
- "Ya estás aquí."
- "Te hice venir porque ayer escuché algo en el bosque."
- "Era tarde pero me quede preocupado..."
- "¿Te importa echar un ojo por si ves algo raro?"

**Textos (inglés):**
- "You are here already."
- "I called you because yesterday I heard something in the forest."
- "It was late but I was worried..."
- "Would you mind taking a look in case you see anything strange?"

#### DLG_ELDRAN_MISSION1_COMPLETED
1. Create > Game > Dialogue
2. Nombrar: `DLG_ELDRAN_MISSION1_COMPLETED`
3. Configurar líneas:
   ```
   Line 0:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION1_COMPLETED_01
   
   Line 1:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION1_COMPLETED_02
   ```

**Textos:**
- ES: "Era algo que nunca había escuchado..." / "Lo mismo fue mi imaginación..."
- EN: "It was something I had never heard before..." / "Maybe it was just my imagination..."

### 2.2 Diálogos de Misión 2

#### DLG_ELDRAN_MISSION2_OFFER (transición de Misión 1 a Misión 2)
1. Create > Game > Dialogue
2. Nombrar: `DLG_ELDRAN_MISSION2_OFFER`
3. Configurar líneas:
   ```
   Line 0:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_OFFER_01
   
   Line 1:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_OFFER_02
   
   Line 2:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_OFFER_03
   
   Line 3:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_OFFER_04
   
   Line 4:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_OFFER_05
   ```

**Textos (español):**
- "Gracias por venir."
- "He estado recogiendo frutas en el bosque..."
- "Y ahora la caja pesa demasiado para que yo la traiga solo."
- "¿Podrías ir al bosque a buscarla?"
- "Y luego traérmela aquí, por favor."

**Textos (inglés):**
- "Thanks for coming."
- "I've been gathering fruits in the forest..."
- "And now the crate is too heavy for me to carry alone."
- "Could you go to the forest and find it?"
- "And then bring it back to me, please."

#### DLG_ELDRAN_MISSION2_INPROGRESS
1. Create > Game > Dialogue
2. Nombrar: `DLG_ELDRAN_MISSION2_INPROGRESS`
3. Configurar líneas:
   ```
   Line 0:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_INPROGRESS_01
   
   Line 1:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_INPROGRESS_02
   ```

**Textos:**
- ES: "¿Encontraste la caja de frutas?" / "Debería estar en el bosque, búscala bien."
- EN: "Did you find the fruit crate?" / "It should be in the forest, look carefully."

#### DLG_ELDRAN_MISSION2_TURNIN
1. Create > Game > Dialogue
2. Nombrar: `DLG_ELDRAN_MISSION2_TURNIN`
3. Configurar líneas:
   ```
   Line 0:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_TURNIN_01
   
   Line 1:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_TURNIN_02
   
   Line 2:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_TURNIN_03
   ```

**Textos:**
- ES: "¡Excelente! Conseguiste traer la caja." / "Sabía que podía contar contigo." / "Toma, esto es por tu ayuda."
- EN: "Excellent! You managed to bring the crate." / "I knew I could count on you." / "Here, this is for your help."

#### DLG_ELDRAN_MISSION2_COMPLETED
1. Create > Game > Dialogue
2. Nombrar: `DLG_ELDRAN_MISSION2_COMPLETED`
3. Configurar líneas:
   ```
   Line 0:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_COMPLETED_01
   
   Line 1:
   - Speaker Name Id: CHAR_ELDRAN
   - Text Id: DLG_ELDRAN_MISSION2_COMPLETED_02
   ```

**Textos:**
- ES: "Gracias de nuevo por traer esa pesada caja." / "Las frutas están deliciosas."
- EN: "Thanks again for bringing that heavy crate." / "The fruits are delicious."

**IMPORTANTE:** Usa los campos **"Id"** (Speaker Name Id, Text Id) para que el sistema de localización funcione. Los campos sin "Id" son fallbacks.

---

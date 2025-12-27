# 🗣️ INSTRUCCIONES: Añadir Frase Antes del Combate

## 📋 **PROBLEMA ACTUAL**

Erika **NO** está diciendo ninguna frase antes del combate. Los logs muestran:

```
[NPCInteractiveNarrativeExecutor:Erika] Iniciando cadena narrativa con 1 acciones
[NPCInteractiveNarrativeExecutor:Erika] ▶️ INICIO Acción 0/1: StartCombat
```

**Solo hay 1 acción**: `StartCombat` 
**Falta**: Acción de `Dialogue` ANTES del combate

---

## ✅ **SOLUCIÓN: Agregar Diálogo a la Cadena Narrativa**

### **🎯 PASOS EN UNITY:**

#### **1. Localizar el ScriptableObject de Erika**

```
Unity → Project → Buscar: "NPC_InteractiveNarrative_Config_Erika"
```

(O el nombre del config que uses para Erika)

---

#### **2. Abrir el Inspector**

```
Seleccionar el ScriptableObject → Inspector
```

Verás:
```
┌─────────────────────────────────────────┐
│ Narrativas Condicionales                │
│   • Conditional Narratives (Array)      │
│     Size: 1                              │
│     Element 0 ▼                          │
│       - Description: "Combate con Erika"│
│       - Priority: 1                      │
│       - Narrative Chain (Array)         │
│         Size: 1  ← ❌ PROBLEMA: SOLO 1  │
│         Element 0 ▼                      │
│           - Action Type: StartCombat    │
│           - Combat Config: ...          │
└─────────────────────────────────────────┘
```

---

#### **3. Expandir "Narrative Chain"**

```
Narrative Chain (Array)
  Size: 1  ← ❌ Cambiar a: 2
```

**Cambiar el tamaño de 1 a 2:**
```
Narrative Chain (Array)
  Size: 2  ← ✅ AHORA HAY 2 ACCIONES
```

---

#### **4. Configurar la Primera Acción (Diálogo)**

```
Element 0 ▼  ← ESTA ES LA NUEVA ACCIÓN DE DIÁLOGO
  ┌───────────────────────────────────────┐
  │ Tipo de Acción                        │
  │   Action Type: Dialogue  ← ✅         │
  ├───────────────────────────────────────┤
  │ Dialogue                              │
  │   Dialogue: [Arrastrar DialogueAsset] │
  │   ej: "Erika_PreBattle_Dialogue"      │
  └───────────────────────────────────────┘
```

**📝 NOTAS:**
- Si aún **NO tienes** el DialogueAsset creado, debes crearlo:
  ```
  Project → Click derecho → Create → Dialogue Asset
  Nombre: "Erika_PreBattle_Dialogue"
  ```
  
- En el DialogueAsset, configura:
  ```
  Speaker: Erika
  Text: "¡Prepárate! ¡Te mostraré mi poder!"
  (O la frase que quieras)
  ```

---

#### **5. Configurar la Segunda Acción (Combate)**

```
Element 1 ▼  ← ESTA ES LA ACCIÓN DE COMBATE (la que ya existía)
  ┌───────────────────────────────────────┐
  │ Tipo de Acción                        │
  │   Action Type: StartCombat  ← ✅      │
  ├───────────────────────────────────────┤
  │ Combat                                │
  │   Combat Config: NPC_Combat_Config... │
  │   Combat Target: (None) - usa Player  │
  └───────────────────────────────────────┘
```

---

#### **6. GUARDAR**

```
File → Save  (o Ctrl+S)
```

---

## 🎬 **RESULTADO ESPERADO**

Ahora, cuando interactúes con Erika, los logs mostrarán:

```
[NPCInteractiveNarrativeExecutor:Erika] Iniciando cadena narrativa con 2 acciones

[NPCInteractiveNarrativeExecutor:Erika] ▶️ INICIO Acción 0/2: Dialogue
[Dialogue System] Mostrando diálogo: "¡Prepárate! ¡Te mostraré mi poder!"
[NPCInteractiveNarrativeExecutor:Erika] ✅ COMPLETADA Acción 0: Dialogue

[NPCInteractiveNarrativeExecutor:Erika] ▶️ INICIO Acción 1/2: StartCombat
[NPCInteractiveNarrativeExecutor:Erika] ⚔️ Iniciando combate con config: NPC_Combat_Config_Erika
```

---

## 🔍 **VALIDACIÓN**

### **Antes de guardar, verifica:**

✅ `Narrative Chain Size: 2`
✅ `Element 0` → `Action Type: Dialogue`
✅ `Element 0` → `Dialogue: [DialogueAsset asignado]`
✅ `Element 1` → `Action Type: StartCombat`
✅ `Element 1` → `Combat Config: [Config asignado]`

---

## 🚨 **ERRORES COMUNES**

### ❌ "Diálogo no se muestra"
- **Causa:** DialogueAsset no está asignado
- **Solución:** Verifica que `Element 0 → Dialogue` tenga un DialogueAsset

### ❌ "Combate empieza sin diálogo"
- **Causa:** Las acciones están en orden inverso
- **Solución:** `Element 0` debe ser `Dialogue`, `Element 1` debe ser `StartCombat`

### ❌ "Diálogo se muestra pero no aparece texto"
- **Causa:** DialogueAsset está vacío
- **Solución:** Abre el DialogueAsset y añade texto

---

## 📋 **ESTRUCTURA CORRECTA**

```
NPC_InteractiveNarrative_Config_Erika
└── Conditional Narratives
    └── Element 0
        └── Narrative Chain
            ├── [0] Dialogue ← "Frase antes del combate"
            └── [1] StartCombat ← "Inicia el combate"
```

---

## 🎨 **EJEMPLO COMPLETO**

### **Erika_PreBattle_Dialogue (DialogueAsset)**
```
Speaker: Erika
Text: "¿Así que quieres desafiarme? ¡Muy bien! ¡Veamos de qué estás hecho!"
Duration: 3.0s
```

### **Narrative Chain (Config)**
```
[0] Dialogue → Erika_PreBattle_Dialogue
[1] StartCombat → NPC_Combat_Config_Erika
```

---

**¡LISTO!** Ahora Erika dirá su frase antes de iniciar el combate. 🎉


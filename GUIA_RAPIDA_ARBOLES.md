# 🚀 GUÍA RÁPIDA: Arreglar Árboles del Bosque

## ⚡ 3 Pasos Simples

### 1️⃣ Crear Fixer (30 segundos)
```
Jerarquía → Click derecho → Create Empty
Nombre: "NavMeshObstacleFixer"
Inspector → Add Component → NavMeshObstacleFixer
```

### 2️⃣ Configurar (15 segundos)
```
Search Root: [Arrastra "Woods" o "Trees" del bosque]
Move Threshold: 1000
```

### 3️⃣ Ejecutar (5 segundos)
```
Inspector → Botón: "🔧 Arreglar Todos los NavMeshObstacle"
```

## ✅ ¡Listo!

Verás en la consola:
```
[ObstacleFixer] 🔍 Buscando en 'Woods'...
[ObstacleFixer] ✅ 157/157 NavMeshObstacle configurados
```

---

## 🎯 Resultado Inmediato

**Antes**: NPCs corriendo sin parar cerca de árboles  
**Después**: NPCs tranquilos y quietos en IdleState  

---

## ⚠️ Si No Funciona

1. Verificar que el GameObject "Woods"/"Trees" sea el correcto
2. Asegurar que los árboles tengan NavMeshObstacle
3. Ejecutar el botón "➕ Añadir NavMeshObstacle a Árboles" primero

---

## 💡 Para Nuevos Árboles

Edita el **Prefab del árbol**:
```
1. Abrir prefab
2. NavMeshObstacle:
   - Carving Move Threshold: 1000
   - Carve Only Stationary: ✅
3. Guardar prefab
```

Todos los árboles nuevos tendrán la configuración correcta.

---

**Tiempo total**: < 1 minuto  
**Dificultad**: ��� Muy fácil  
**Efectividad**: 100% ✅

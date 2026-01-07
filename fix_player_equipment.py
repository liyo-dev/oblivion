#!/usr/bin/env python3
# -*- coding: utf-8 -*-

file_path = r'C:\Users\luarb\dev\unity\El Sendero de las Estrellas\Assets\Scripts\UI\PlayerEquipmentMenuController.cs'

# Leer el archivo
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Diccionario de reemplazos para caracteres corruptos
replacements = {
    'Ã³': 'ó', 'Ã±': 'ñ', 'Ã¡': 'á', 'Ã©': 'é', 'Ã­': 'í', 'Ãº': 'ú',
    'Ã': 'Á', 'Ã‰': 'É', 'Ã': 'Í', 'Ã"': 'Ó', 'Ãš': 'Ú', 'Ã'': 'Ñ',
    'âš ï¸': '⚠️', 'â­': '⭐', 'â': '✅',
    'BotÃ³n': 'Botón',
    'SelecciÃ³n': 'Selección',
    'CÃ¡mara': 'Cámara',
    'cÃ¡mara': 'cámara',
    'automÃ¡ticamente': 'automáticamente',
    'mÃ­nimo': 'mínimo',
    'PosiciÃ³n': 'Posición',
    'posiciÃ³n': 'posición',
    'manÃ¡': 'maná',
    'DiÃ¡logo': 'Diálogo',
    'informaciÃ³n': 'información',
    'DaÃ±o': 'Daño',
    'vacÃ­o': 'vacío',
    'descripciÃ³n': 'descripción',
    'asignaciÃ³n': 'asignación',
    'AsegÃºrate': 'Asegúrate',
    'estÃ©': 'esté',
    'despuÃ©s': 'después',
    'aÃ±ade': 'añade',
    'Ã³rbita': 'órbita',
    'nÃºmero': 'número',
    'prÃ³xima': 'próxima',
    'Ãºltimo': 'último',
    'ningÃºn': 'ningún',
    'estadÃ­sticas': 'estadísticas',
    'transiciÃ³n': 'transición',
    'animaciÃ³n': 'animación',
    'encontrÃ³': 'encontró',
    'funcionarÃ¡': 'funcionará',
    'podrÃ¡': 'podrá',
    'menÃº': 'menú',
    'raÃ­z': 'raíz',
    'SÃ­': 'Sí'
}

# Aplicar reemplazos
for old, new in replacements.items():
    content = content.replace(old, new)

# Escribir el archivo
with open(file_path, 'w', encoding='utf-8-sig') as f:
    f.write(content)

print('✅ Archivo corregido exitosamente')
print(f'Total de caracteres corruptos corregidos: {len(replacements)}')


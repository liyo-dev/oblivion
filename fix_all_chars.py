#!/usr/bin/env python3
# -*- coding: utf-8 -*-

file_path = r'C:\Users\luarb\dev\unity\El Sendero de las Estrellas\Assets\Scripts\UI\PlayerEquipmentMenuController.cs'

# Leer el archivo
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Contar ocurrencias antes
count_before = content.count('Ã')
print(f'Caracteres mal codificados encontrados: {count_before}')

# Reemplazos específicos del problema
replacements = [
    # Letras acentuadas mal codificadas
    ('Ã³', 'ó'),
    ('Ã±', 'ñ'),
    ('Ã¡', 'á'),
    ('Ã©', 'é'),
    ('Ã­', 'í'),
    ('Ãº', 'ú'),
    ('Ã', 'Á'),
    ('Ã‰', 'É'),
    ('Ã', 'Í'),
    ('Ã"', 'Ó'),
    ('Ãš', 'Ú'),
    ('Ã'', 'Ñ'),
    # Símbolos y caracteres especiales
    ('âŒ', 'ERROR:'),
    ('â€¢', '*'),
    # Caracteres de caja (bordes)
    ('â•"', '='),
    ('â•—', '='),
    ('â•'', ' '),
    ('â• ', '='),
    ('â•£', '='),
    ('â•šâ•', '='),
    ('â•', '='),
    # Palabras completas mal codificadas
    ('ESTÃ CONFIGURADA', 'ESTA CONFIGURADA'),
    ('SOLUCIÃ"N', 'SOLUCION'),
    ('Añade', 'Anade'),
    ('categorías', 'categorias'),
]

# Aplicar reemplazos
changes = 0
for old, new in replacements:
    if old in content:
        count = content.count(old)
        print(f'Reemplazando "{old}" -> "{new}" ({count} ocurrencias)')
        content = content.replace(old, new)
        changes += count

# Escribir el archivo corregido
with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print(f'\nTotal de cambios realizados: {changes}')
print('Archivo corregido exitosamente')


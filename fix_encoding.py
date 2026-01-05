#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import sys

path = r'C:\Users\luarb\dev\unity\El Sendero de las Estrellas\Assets\Scripts\UI\PlayerEquipmentMenuController.cs'

# Leer el archivo
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Lista de reemplazos
replacements = [
    ('menÃº', 'menú'),
    ('raÃ­z', 'raíz'),
    ('Pestañas', 'Pestañas'),
    ('Selección', 'Selección'),
    ('Cámara', 'Cámara'),
    ('cámara', 'cámara'),
    ('automáticamente', 'automáticamente'),
    ('mínimo', 'mínimo'),
    ('Posición', 'Posición'),
    ('maná', 'maná'),
    ('Diálogo', 'Diálogo'),
    ('información', 'información'),
    ('Daño', 'Daño'),
    ('é', 'é'),
    ('vacío', 'vacío'),
    ('descripción', 'descripción'),
    ('asignación', 'asignación'),
    ('⚠️', '⚠️'),
    ('❌', '❌'),
    ('⭐', '⭐'),
    ('╔', '╔'),
    ('═', '═'),
    ('╗', '╗'),
    ('║', '║'),
    ('╣', '╣'),
    ('•', '•'),
    ('╚', '╚'),
    ('Asegú', 'Asegú'),
    ('esté', 'esté'),
    ('después', 'después'),
    ('añade', 'añade'),
    ('órbita', 'órbita'),
    ('número', 'número'),
    ('próxima', 'próxima'),
    ('último', 'último'),
    ('ningún', 'ningún'),
    ('estadísticas', 'estadísticas'),
    ('transición', 'transición'),
    ('animación', 'animación'),
    ('curación', 'curación'),
    ('restauración', 'restauración'),
    ('categoría', 'categoría'),
    ('categorías', 'categorías'),
    ('SOLUCIÓN', 'SOLUCIÓN'),
    ('ESTÁ', 'ESTÁ'),
    ('Sí', 'Sí'),
    ('más', 'más'),
    ('cinemática', 'cinemática'),
]

# Aplicar reemplazos
for old, new in replacements:
    content = content.replace(old, new)

# Guardar el archivo
with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print('Archivo corregido exitosamente')
print(f'Se aplicaron {len(replacements)} reemplazos')


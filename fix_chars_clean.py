#!/usr/bin/env python3
# -*- coding: utf-8 -*-

file_path = r'C:\Users\luarb\dev\unity\El Sendero de las Estrellas\Assets\Scripts\UI\PlayerEquipmentMenuController.cs'

# Leer el archivo
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Reemplazos de caracteres mal codificados
char_replacements = [
    ('Ã³', 'ó'),
    ('Ã±', 'ñ'),
    ('Ã¡', 'á'),
    ('Ã©', 'é'),
    ('Ã­', 'í'),
    ('Ãº', 'ú'),
    ('encontrÃ³', 'encontró'),
    ('SÃ­', 'Sí'),
    ('automÃ¡ticamente', 'automáticamente'),
    ('mÃ­nimo', 'mínimo'),
    ('funcionarÃ¡', 'funcionará'),
    ('AsegÃºrate', 'Asegúrate'),
    ('estÃ©', 'esté'),
    ('despuÃ©s', 'después'),
    ('podrÃ¡', 'podrá'),
    ('botÃ³n', 'botón'),
    ('BotÃ³n', 'Botón'),
    ('estÃ¡', 'está'),
    ('navegaciÃ³n', 'navegación'),
    ('pestaÃ±a', 'pestaña'),
    ('MÃ©todos', 'Métodos'),
    ('menÃºs', 'menús'),
    ('todavÃ­a', 'todavía'),
    ('abriÃ³', 'abrió'),
]

# Aplicar reemplazos
for old, new in char_replacements:
    content = content.replace(old, new)

# Escribir el archivo corregido
with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print('Archivo corregido exitosamente')


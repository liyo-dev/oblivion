namespace Core.InputGlyphs
{
    // OBSOLETO — ya no se usa. Este archivo generaba, en tiempo de ejecución, las 11 imágenes de
    // botón dibujándolas píxel a píxel (junto con InputGlyphCanvas.cs e InputGlyphPixelFont.cs, en
    // esta misma carpeta). Eso era una chapuza: generaba texturas nuevas cada vez que arrancaba el
    // juego en vez de usar archivos de imagen reales, así que no había ningún PNG que un artista (o
    // Raúl) pudiera abrir y sustituir.
    //
    // Sustituido por (agosto 2026):
    //  - Assets/Scripts/Editor/InputGlyphs/InputGlyphAssetGeneratorWindow.cs — herramienta de Editor
    //    (Tools/Input Glyphs/Generar Assets de Botones) que genera esas mismas imágenes UNA VEZ,
    //    como archivos PNG reales en Assets/Resources/InputGlyphs/<Familia>/<nombre>.png. Xbox se
    //    copia del arte real que ya existía en Assets/Art/UI/Buttons; PlayStation/Switch/Teclado se
    //    generan como placeholder (mismo dibujado por código de antes, movido a
    //    Assets/Scripts/Editor/InputGlyphs/InputGlyphPlaceholderCanvas.cs e
    //    InputGlyphPlaceholderFont.cs) hasta que se sustituyan por arte final.
    //  - Core.InputGlyphs.InputGlyphService, que ahora carga esos PNG con Resources.Load en vez de
    //    dibujarlos por código.
    //
    // Candidato a borrar sin más (junto con InputGlyphCanvas.cs e InputGlyphPixelFont.cs) — no lo he
    // tocado más que vaciarlo porque no puedo borrar archivos desde aquí, solo escribirlos. Raúl,
    // puedes eliminar los tres a mano en el Editor cuando quieras: no los usa nada más.
}

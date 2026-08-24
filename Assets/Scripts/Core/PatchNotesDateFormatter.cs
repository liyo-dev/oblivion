using System;

namespace Core
{
    /// <summary>
    /// Formatea fechas al mismo estilo usado en las Notas del Parche in-game y en los devlogs de
    /// itch.io: "24 ago 2026" (día, mes abreviado en español en minúsculas sin punto, año).
    ///
    /// Lo usa <c>PatchNotesFlyoutPanel</c> en runtime (solo como respaldo si todavía no existe
    /// Resources/PatchNotes/BuildDate.txt — p. ej. probando en el Editor sin haber hecho nunca un
    /// build de Player) y <c>PatchNotesBuildGuard</c> (Editor) para escribir la fecha real de cada
    /// build. Vive fuera de Editor/ a propósito, para que ambos lados usen exactamente el mismo
    /// formato sin duplicar la tabla de meses.
    /// </summary>
    public static class PatchNotesDateFormatter
    {
        static readonly string[] MesesEs =
        {
            "ene", "feb", "mar", "abr", "may", "jun",
            "jul", "ago", "sep", "oct", "nov", "dic",
        };

        public static string FormatSpanishDate(DateTime date)
        {
            return $"{date.Day} {MesesEs[date.Month - 1]} {date.Year}";
        }
    }
}

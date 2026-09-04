#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElSendero.EditorTools
{
    /// <summary>
    /// Auditoría de assets sin usar (1 sep 2026). Genera un informe de solo lectura — NUNCA borra
    /// nada por su cuenta. Calcula el cierre transitivo real de dependencias desde todas las
    /// escenas del proyecto (más Resources/, StreamingAssets/ y la cadena del Render Pipeline
    /// activo) y reporta qué queda fuera de ese conjunto como candidato a "basura":
    /// scripts, materiales, prefabs, sprites, audio, paquetes de plugins enteros, etc.
    ///
    /// LIMITACIÓN CONOCIDA: un script que solo se usa desde código (AddComponent&lt;T&gt;() / new T(),
    /// nunca serializado en una escena o prefab) puede aparecer aquí como "sin usar" aunque sí se
    /// use de verdad. Revisa cada candidato antes de borrar — esto es un informe, no un borrado
    /// automático.
    /// </summary>
    public static class UnusedAssetsAuditor
    {
        [MenuItem("El Sendero/Auditoría/Buscar Assets Sin Usar (Informe)")]
        public static void RunAudit()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Auditoría de assets", "Recopilando escenas raíz...", 0.05f);

                var reachable = new HashSet<string>();

                // 1) Todas las escenas del proyecto (no solo las de Build Settings, por seguridad)
                var allScenePaths = AssetDatabase.GetAllAssetPaths()
                    .Where(p => p.StartsWith("Assets/") && p.EndsWith(".unity"))
                    .ToArray();

                EditorUtility.DisplayProgressBar("Auditoría de assets", $"Calculando dependencias de {allScenePaths.Length} escenas...", 0.15f);
                foreach (var dep in AssetDatabase.GetDependencies(allScenePaths, true))
                    reachable.Add(dep);

                // 2) Cadena del Render Pipeline (QualitySettings por nivel + GraphicsSettings)
                EditorUtility.DisplayProgressBar("Auditoría de assets", "Resolviendo cadena de Render Pipeline...", 0.3f);
                var rpRoots = new List<UnityEngine.Object>();
                var defaultRP = GraphicsSettings.defaultRenderPipeline;
                if (defaultRP != null) rpRoots.Add(defaultRP);
                int qualityCount = QualitySettings.names.Length;
                for (int i = 0; i < qualityCount; i++)
                {
                    var rp = QualitySettings.GetRenderPipelineAssetAt(i);
                    if (rp != null) rpRoots.Add(rp);
                }
                foreach (var rp in rpRoots.Distinct())
                {
                    var path = AssetDatabase.GetAssetPath(rp);
                    if (string.IsNullOrEmpty(path)) continue;
                    reachable.Add(path);
                    foreach (var dep in AssetDatabase.GetDependencies(path, true))
                        reachable.Add(dep);
                }

                // 3) Enumerar todos los assets del proyecto
                EditorUtility.DisplayProgressBar("Auditoría de assets", "Enumerando todos los assets...", 0.5f);
                var allAssetPaths = AssetDatabase.GetAllAssetPaths()
                    .Where(p => p.StartsWith("Assets/"))
                    .ToArray();

                bool IsExempt(string path)
                {
                    // Carpetas especiales de Unity: incluidas siempre en build o no navegables
                    // por dependencias de escena (se cargan por convención/string, no por referencia).
                    if (path.Contains("/Resources/") || path.Contains("/Resources.")) return true;
                    if (path.Contains("/StreamingAssets/")) return true;
                    if (path.Contains("/Editor/") || path.Contains("/Editor Default Resources/")) return true;
                    if (path.Contains("/Gizmos/")) return true;
                    return false;
                }

                // 4) Calcular el conjunto sin usar
                EditorUtility.DisplayProgressBar("Auditoría de assets", "Comparando contra los assets alcanzables...", 0.7f);
                var unused = new List<string>();
                foreach (var path in allAssetPaths)
                {
                    if (AssetDatabase.IsValidFolder(path)) continue;
                    if (IsExempt(path)) continue;
                    if (reachable.Contains(path)) continue;
                    unused.Add(path);
                }

                EditorUtility.DisplayProgressBar("Auditoría de assets", "Generando informe...", 0.9f);

                // Agrupar por carpeta (hasta 3 niveles) para que el informe sea legible
                var groups = unused
                    .GroupBy(p => TopFolders(p, 3))
                    .OrderByDescending(g => g.Count())
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine("=== Informe de Assets Sin Usar — El Sendero de las Estrellas ===");
                sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Escenas analizadas: {allScenePaths.Length}");
                sb.AppendLine($"Total de assets en el proyecto: {allAssetPaths.Length}");
                sb.AppendLine($"Candidatos a 'sin usar': {unused.Count}");
                sb.AppendLine();
                sb.AppendLine("LIMITACION: un script usado solo desde codigo (AddComponent<T>()/new T(), nunca");
                sb.AppendLine("serializado en una escena o prefab) puede aparecer aqui aunque si se use de verdad.");
                sb.AppendLine("Revisa cada candidato antes de borrar -- esto es un informe, no un borrado automatico.");
                sb.AppendLine("Tampoco se han escaneado carpetas Editor/, Resources/, StreamingAssets/ ni Gizmos/,");
                sb.AppendLine("que se consideran siempre en uso por convencion de Unity. Los 'Preloaded Assets'");
                sb.AppendLine("de Player Settings tampoco se comprueban -- si algo de ahi sale como sin usar,");
                sb.AppendLine("puede ser un falso positivo, revisalo en Project Settings > Player antes de borrar.");
                sb.AppendLine();
                sb.AppendLine("--- Resumen por carpeta ---");
                foreach (var g in groups)
                {
                    long groupBytes = g.Sum(p => SafeFileSize(p));
                    sb.AppendLine($"{g.Key} -- {g.Count()} archivos, {FormatBytes(groupBytes)}");
                }
                sb.AppendLine();
                sb.AppendLine("--- Listado completo ---");
                long totalBytes = 0;
                foreach (var path in unused.OrderBy(p => p))
                {
                    long size = SafeFileSize(path);
                    totalBytes += size;
                    sb.AppendLine($"{FormatBytes(size)}\t{path}");
                }
                sb.AppendLine();
                sb.AppendLine($"TOTAL recuperable estimado: {FormatBytes(totalBytes)}");

                var reportDir = Path.Combine(Application.dataPath, "..", "Reports");
                Directory.CreateDirectory(reportDir);
                var reportPath = Path.Combine(reportDir, $"UnusedAssetsReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(reportPath, sb.ToString());

                EditorUtility.ClearProgressBar();
                Debug.Log($"[UnusedAssetsAuditor] Informe generado en: {reportPath}\n{unused.Count} candidatos, {FormatBytes(totalBytes)} recuperables.");
                EditorUtility.RevealInFinder(reportPath);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[UnusedAssetsAuditor] Fallo generando el informe: {e}");
            }
        }

        private static string TopFolders(string path, int depth)
        {
            var parts = path.Split('/');
            int take = Math.Min(depth, Math.Max(parts.Length - 1, 1));
            return string.Join("/", parts.Take(take));
        }

        private static long SafeFileSize(string assetPath)
        {
            try
            {
                var full = Path.Combine(Application.dataPath, "..", assetPath);
                return new FileInfo(full).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size:0.##} {units[unit]}";
        }
    }
}
#endif

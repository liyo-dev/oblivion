using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Auditoría/conversión de materiales del pack "Fantasy_Kingdom_Pack" (el kit modular elegido
/// para la prueba de Will en el Sendero) al shader unificado del proyecto, Quibli/Stylized Lit
/// (mismo shader al que ya se convirtieron los ~962 materiales de otras auditorías anteriores).
///
/// Hallazgo al auditar el pack (1 sep 2026): de los 44 materiales, 32 YA estaban en Quibli/
/// Stylized Lit — en concreto FK01–FK04, Ground01–05 y la mayoría de Terrain*, que son justo los
/// que usan los prefabs de Interior/Room y Interior/Floor (el kit del laberinto). Es decir, la
/// geometría estructural del laberinto de Will ya está en el shader correcto sin tocar nada.
///
/// Lo que quedaba en el shader Standard clásico de Unity eran 9 materiales, todos del mismo tipo:
/// decorados ambientales alfa-blend (Bee, Butterfly01–03, Dandelion, Fire01, Petals01, Smoke01–02).
/// Esta herramienta los convierte también, replicando el modo de transparencia original (opaco/
/// cutout/fade/transparent) con la misma lógica que usa el propio URP (BaseShaderGUI.
/// SetupMaterialBlendMode) para que no cambie su comportamiento visual, solo el shader.
///
/// Deliberadamente NO toca Grass02 (shader de partículas) ni Terrain01_D/Terrain03 (shader custom
/// de viento) — no son el shader Standard y una conversión automática a ciegas podría romper su
/// comportamiento (scroll de UVs, animación por vértices, etc.) sin ningún beneficio real, ya que
/// ninguno de los dos es estructural para el laberinto. Si alguna vez se necesitan en Quibli,
/// mejor a mano y probando el resultado en el Editor.
///
/// Idempotente: si se vuelve a ejecutar, los materiales ya convertidos se listan como
/// "ya en Quibli" y no se tocan de nuevo.
/// </summary>
public static class FantasyKingdomShaderConverter
{
    private const string MaterialsFolder = "Assets/Art/World/Fantasy_Kingdom_Pack/Materials";
    private const string QuibliShaderGuid = "2a230514c860643f69b6a4d1871d3825";

    [MenuItem("El Sendero/Materiales/Convertir Fantasy Kingdom Pack a Quibli StylizedLit")]
    public static void ConvertAll()
    {
        string shaderPath = AssetDatabase.GUIDToAssetPath(QuibliShaderGuid);
        Shader quibliShader = string.IsNullOrEmpty(shaderPath) ? null : AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (quibliShader == null)
        {
            Debug.LogError("[FantasyKingdomShaderConverter] No se encontró el shader Quibli/Stylized Lit (guid " + QuibliShaderGuid + "). Abortando sin tocar nada.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder });
        int converted = 0, yaEnQuibli = 0, omitidos = 0;
        var log = new StringBuilder();
        log.AppendLine("=== Fantasy_Kingdom_Pack -> Quibli/Stylized Lit ===");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            if (mat.shader == quibliShader)
            {
                yaEnQuibli++;
                continue;
            }

            if (mat.shader == null || mat.shader.name != "Standard")
            {
                omitidos++;
                log.AppendLine($"  OMITIDO (no es el shader Standard, revisar a mano si hace falta): {mat.name} — shader actual: {(mat.shader != null ? mat.shader.name : "null")}");
                continue;
            }

            ConvertOne(mat, quibliShader, log);
            converted++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine($"--- Resumen: {converted} convertidos, {yaEnQuibli} ya estaban en Quibli, {omitidos} omitidos ---");
        Debug.Log(log.ToString());
    }

    private static void ConvertOne(Material mat, Shader quibliShader, StringBuilder log)
    {
        // --- Leer todo lo que hace falta del material Standard ANTES de cambiar el shader ---
        Texture baseMap = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        Vector2 baseMapScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
        Vector2 baseMapOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
        Color baseColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

        Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
        Vector2 bumpScale = mat.HasProperty("_BumpMap") ? mat.GetTextureScale("_BumpMap") : Vector2.one;
        Vector2 bumpOffset = mat.HasProperty("_BumpMap") ? mat.GetTextureOffset("_BumpMap") : Vector2.zero;

        bool hasEmission = mat.IsKeywordEnabled("_EMISSION");
        Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
        Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
        if (!hasEmission && emissionColor.maxColorComponent > 0.001f) hasEmission = true;

        // Modo de transparencia original del Standard: 0=Opaque, 1=Cutout, 2=Fade, 3=Transparent
        int modoOriginal = mat.HasProperty("_Mode") ? (int)mat.GetFloat("_Mode") : 0;
        float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

        // --- Cambiar el shader ---
        mat.shader = quibliShader;

        // --- Textura base / color ---
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetTextureScale("_BaseMap", baseMapScale);
            mat.SetTextureOffset("_BaseMap", baseMapOffset);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);

        // --- Normal map ---
        if (mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", bumpMap);
            mat.SetTextureScale("_BumpMap", bumpScale);
            mat.SetTextureOffset("_BumpMap", bumpOffset);
        }
        if (bumpMap != null) mat.EnableKeyword("_NORMALMAP");
        else mat.DisableKeyword("_NORMALMAP");

        // --- Emisión (Fire01 la necesita para brillar) ---
        if (hasEmission && mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", emissionColor);
            if (emissionMap != null && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emissionMap);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
        }

        // --- Modo de superficie: opaco / recorte alfa / alpha blend (igual que el original) ---
        ApplySurfaceMode(mat, modoOriginal, cutoff);

        // Los toggles artísticos propios de Quibli (Rim, Specular plano, Gradiente de altura,
        // Outline, Vertex colors) se dejan apagados a propósito: esto es solo la migración de
        // shader, no un rediseño. Si algún prop concreto (p.ej. Fire01 con Rim para las brasas)
        // se beneficia del look estilizado completo, se activa a mano en el Inspector después.
        ApagarToggle(mat, "_SpecularEnabled", "DR_SPECULAR_ON");
        ApagarToggle(mat, "_RimEnabled", "DR_RIM_ON");
        ApagarToggle(mat, "_GradientEnabled", "DR_GRADIENT_ON");
        ApagarToggle(mat, "_OutlineEnabled", "DR_OUTLINE_ON");
        ApagarToggle(mat, "_VertexColorsEnabled", "DR_VERTEX_COLORS_ON");

        EditorUtility.SetDirty(mat);
        log.AppendLine($"  Convertido: {mat.name} (modo: {NombreModo(modoOriginal)}{(hasEmission ? ", con emisión" : "")}{(bumpMap != null ? ", con normal map" : "")})");
    }

    private static void ApplySurfaceMode(Material mat, int modoOriginal, float cutoff)
    {
        switch (modoOriginal)
        {
            case 1: // Cutout
                SetFloatSiExiste(mat, "_Surface", 0f);
                SetFloatSiExiste(mat, "_AlphaClip", 1f);
                SetFloatSiExiste(mat, "_Cutoff", cutoff);
                SetFloatSiExiste(mat, "_SrcBlend", (float)BlendMode.One);
                SetFloatSiExiste(mat, "_DstBlend", (float)BlendMode.Zero);
                SetFloatSiExiste(mat, "_ZWrite", 1f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                break;

            case 2: // Fade
            case 3: // Transparent
                SetFloatSiExiste(mat, "_Surface", 1f);
                SetFloatSiExiste(mat, "_AlphaClip", 0f);
                SetFloatSiExiste(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
                SetFloatSiExiste(mat, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                SetFloatSiExiste(mat, "_ZWrite", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
                break;

            default: // Opaque
                SetFloatSiExiste(mat, "_Surface", 0f);
                SetFloatSiExiste(mat, "_AlphaClip", 0f);
                SetFloatSiExiste(mat, "_SrcBlend", (float)BlendMode.One);
                SetFloatSiExiste(mat, "_DstBlend", (float)BlendMode.Zero);
                SetFloatSiExiste(mat, "_ZWrite", 1f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = -1; // cola por defecto del shader
                break;
        }
    }

    private static void SetFloatSiExiste(Material mat, string prop, float value)
    {
        if (mat.HasProperty(prop)) mat.SetFloat(prop, value);
    }

    private static void ApagarToggle(Material mat, string prop, string keyword)
    {
        if (mat.HasProperty(prop)) mat.SetFloat(prop, 0f);
        mat.DisableKeyword(keyword);
    }

    private static string NombreModo(int modo)
    {
        switch (modo)
        {
            case 1: return "Cutout";
            case 2: return "Fade";
            case 3: return "Transparent";
            default: return "Opaque";
        }
    }
}

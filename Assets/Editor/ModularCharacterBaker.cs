#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class ModularCharacterBaker
{
    [MenuItem("El Sendero/Personajes/Bake Selected As Clean Prefab")]
    public static void BakeSelected()
    {
        var src = Selection.activeGameObject;
        if (!src)
        {
            EditorUtility.DisplayDialog("Baker", "Selecciona el GO raíz del personaje en la escena.", "Ok");
            return;
        }

        // Crear una copia en la escena
        GameObject clone = Object.Instantiate(src);
        clone.name = src.name + "_BAKED";
        
        // Posicionar al lado del original 
        clone.transform.position = src.transform.position + Vector3.right * 2f;
        clone.transform.rotation = src.transform.rotation;

        // Eliminar todos los GameObjects inactivos (SetActive = false)
        RemoveInactiveRecursive(clone.transform);
        
        // Eliminar holders vacíos
        RemoveEmptyHolders(clone.transform);

        // Seleccionar el clon para que el usuario lo vea
        Selection.activeGameObject = clone;
        
        EditorUtility.DisplayDialog("Baked!", 
            $"Personaje horneado creado: {clone.name}\n\n" +
            "Solo contiene las partes activas (sin GameObjects inactivos).\n" +
            "Ahora puedes convertirlo a prefab manualmente si lo deseas.", 
            "Ok");
    }

    static void RemoveInactiveRecursive(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i);
            RemoveInactiveRecursive(child);
            
            // Si el GameObject está inactivo, eliminarlo
            if (!child.gameObject.activeSelf)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    static void RemoveEmptyHolders(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            RemoveEmptyHolders(t.GetChild(i));

        bool hasRenderer = t.GetComponent<Renderer>() || t.GetComponent<SkinnedMeshRenderer>();
        bool hasChildren = t.childCount > 0;
        bool isRoot = t.parent == null;
        
        if (!isRoot && !hasRenderer && !hasChildren)
            Object.DestroyImmediate(t.gameObject);
    }
}
#endif

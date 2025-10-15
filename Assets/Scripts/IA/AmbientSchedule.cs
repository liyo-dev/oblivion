using UnityEngine;

[CreateAssetMenu(menuName = "Ambient/Schedule")]
public class AmbientSchedule : ScriptableObject
{
    [System.Serializable]
    public class Block
    {
        [Range(0,23)] public int fromHour = 8;
        [Range(0,23)] public int toHour = 12;
        public string tag; // ej: "Market", "Bench", "Plaza", "Wander"
    }

    public Block[] blocks;

    public string GetCurrentTag(int hour24)
    {
        foreach (var b in blocks)
        {
            // rango inclusivo-exclusivo
            if (hour24 >= b.fromHour && hour24 < b.toHour)
                return string.IsNullOrEmpty(b.tag) ? "Wander" : b.tag;
        }
        return "Wander";
    }
}

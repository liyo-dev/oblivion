using System;
using UnityEngine;

namespace GameParticles.Editor
{
[CreateAssetMenu(fileName = "RenderSettingsConfig", menuName = "Particle Master/Render Settings Config")]
public class RenderSettingsConfig : ScriptableObject
{
    [Serializable]
    public class RenderModeSettings
    {
        public string RenderType;
        public UnityEngine.Rendering.BlendMode SrcBlend;
        public UnityEngine.Rendering.BlendMode DstBlend;
        public int ZWrite;
        public int ZTest;
        public int RenderQueue;
    }

    [Serializable]
    public class ApplicationModeConfig
    {
        public RenderModeSettings Transparent;
        public RenderModeSettings Additive;
        public RenderModeSettings SoftAdditive;
        public RenderModeSettings Blend;
        public RenderModeSettings Opaque;
    }

    public ApplicationModeConfig Game3D;
    public ApplicationModeConfig UI;
    public ApplicationModeConfig Sprite2D;
}
}
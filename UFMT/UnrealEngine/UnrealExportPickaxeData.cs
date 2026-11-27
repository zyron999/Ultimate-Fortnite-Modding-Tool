
using System.Collections.Generic;

namespace UFMT.UnrealEngine
{
    internal class UnrealExportPickaxeData
    {
        public string FbxPath { get; set; }
        public List<string> SoundWavPaths { get; set; }
        public string[] DiffuseTexturePaths { get; set; }
        public string[] MaskTexturePaths { get; set; }
        public string[] NormalTexturePaths { get; set; }
        public string[] SpecularTexturePaths { get; set; }
        public string[] IconTexturePaths { get; set; }
        public string[] PhysicsJsonPaths { get; set; }
        public string[] MaterialNames { get; set; }
        public string Codename { get; set; }
        public string PID { get; set; } = string.Empty;
        public string UePickaxesPackagePath { get; set; }
    }
}

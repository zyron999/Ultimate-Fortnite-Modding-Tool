using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UFMT.UnrealEngine
{
    internal class UnrealExportEmoteData
    {
        public string MaleAnimationFbxPath { get; set; }
        public string MaleAnimationJsonPath { get; set; }
        public double MaleAnimationLength { get; set; }
        public string FemaleAnimationFbxPath { get; set; }
        public string FemaleAnimationJsonPath { get; set; }
        public double FemaleAnimationLength { get; set; }
        public string LoopSoundFilePath { get; set; }
        public string IntroSoundFilePath { get; set; }
        public int SoundWavCompressionQuality { get; set; }
        public string[] IconTexturePaths { get; set; }
        public string Codename { get; set; }
        public string EID { get; set; } = string.Empty;
        public string UeEmotesPackagePath { get; set; }
        public string UnrealEngineVersion { get; set; }
        public float LoopSoundStartTime { get; set; }
    }
}

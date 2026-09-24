using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using UFMT.Core;

namespace UFMT.FnAssetsLogic
{
    internal class SkinLobbyAnimationJsonEditor
    {
        internal static void RemoveControlExpressions(string jsonPath)
        {
            if (jsonPath == null || !File.Exists(jsonPath)) return;
            Console.WriteLine($"Removing ctrl expressions from {Path.GetFileName(jsonPath)}");
            string jsonContent = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(jsonContent);

            string updatedJson = doc.RootElement.GetRawText().Replace("ctrl_expressions_", "").Replace("CTRL_expressions_", "");
            File.WriteAllText(jsonPath, updatedJson);
            Log.Success($"Successfully edited {Path.GetFileName(jsonPath)}");
        }
    }
}

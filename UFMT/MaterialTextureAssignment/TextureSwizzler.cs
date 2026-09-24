#pragma warning disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UFMT;
using UFMT.Core;
using UFMT.MaterialTextureAssignment;

namespace UFMT.MaterialTextureAssignment
{
    internal static class TextureSwizzler
    {
        internal static void SwizzleSpecularTextures(string texturesPath, string[] textures)
        {
            string swizzledTexturesPath = Path.Combine(texturesPath, "Swizzled");
            if (!Directory.Exists(swizzledTexturesPath))
            {
                var swizzledFolder = Directory.CreateDirectory(swizzledTexturesPath);
                swizzledFolder.Attributes |= FileAttributes.Hidden;
            }

            string[] texturePaths = textures.Select(tex => Path.Combine(texturesPath, $"{tex}.png")).Where
            (texPath => File.Exists(texPath) && !File.Exists(Path.Combine(swizzledTexturesPath, Path.GetFileName(texPath)))).ToArray();
            Parallel.ForEach(texturePaths, texPath =>
            {
                Console.WriteLine($"Swizzling {Path.GetFileName(texPath)}...");
                using Bitmap bmp = new Bitmap(texPath);
                var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, bmp.PixelFormat);

                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0;
                    int bytesPerPixel = System.Drawing.Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
                    int totalBytes = bmpData.Stride * bmp.Height;

                    for (int i = 0; i < totalBytes; i += bytesPerPixel)
                    {
                        byte blue = ptr[i];
                        ptr[i] = ptr[i + 1];
                        ptr[i + 1] = blue;
                    }
                }

                bmp.UnlockBits(bmpData);
                bmp.Save(Path.Combine(texturesPath, "Swizzled", Path.GetFileName(texPath)));
                Log.Success($"Successfuly Swizzled {Path.GetFileName(texPath)}!");
            });
        }
    }
}

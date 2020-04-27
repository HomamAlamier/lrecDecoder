using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace lrecDecoder
{
    static class BC
    {
        public static int bytesToInt(byte[] array, int start, int count)
        {
            int i = 0;
            for (int j = start + count; j > start; j--)
            {
                i = i << 8;
                i += array[j - 1] & 0xFF;
            }
            return i;
        }
        public static String[] getStrings(byte[] paramArrayOfbyte, int paramInt1, int paramInt2)
        {
            String[] arrayOfString = new String[paramInt2];
            bool bool1 = false;
            byte b = 0;
            while (true)
            {
                int i = paramInt1;
                for (int j = paramInt1; j < paramArrayOfbyte.Length && b < paramInt2; j++)
                {
                    if (paramArrayOfbyte[j] == 0 && (!bool1 || paramArrayOfbyte[j + 1] == 0))
                    {
                        arrayOfString[b++] = (i == j) ? "" : Encoding.UTF8.GetString(paramArrayOfbyte, i, j - i);
                        if (bool1)
                            j++;
                        i = j + 1;
                    }
                }
                if (bool1 == true && b == 0)
                {
                    bool1 = false;
                    continue;
                }
                break;
            }
            return arrayOfString;
        }
        public static int dwordToInt(byte[] paramArrayOfbyte, int paramInt)
        {
            return ((paramArrayOfbyte[paramInt + 3] & 0xFF) << 32) + ((paramArrayOfbyte[paramInt + 2] & 0xFF) << 16) + ((paramArrayOfbyte[paramInt + 1] & 0xFF) << 8) + (paramArrayOfbyte[paramInt] & 0xFF);
        }
        public static Color[] bytesToPallete(byte[] data)
        {
            byte[] pa = new byte[data.Length - 1];
            Array.Copy(data, 1, pa, 0, data.Length - 1);
            Color[] cols = new Color[pa.Length / 4];
            for (int i = 0; i < pa.Length; i += 4)
            {
                if (pa.Length - i < 3) break;
                byte[] corrected = new byte[]
                {
                     pa[i + 2],
                     pa[i + 1],
                     pa[i + 0],
                };
                var col = Color.FromArgb(255, Color.FromArgb(corrected[0], corrected[1], corrected[2]));
                cols[i / 4] = col;
            }
            return cols;
        }
        public static Bitmap BuildImage(Byte[] sourceData, Int32 width, Int32 height,
                        Int32 stride, PixelFormat pixelFormat, Color[] palette, Color? defaultColor)
        {
            Bitmap newImage = new Bitmap(width, height, pixelFormat);
            BitmapData targetData = newImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, newImage.PixelFormat);
            Int32 newDataWidth = ((Image.GetPixelFormatSize(pixelFormat) * width) + 7) / 8;
            Int32 targetStride = targetData.Stride;
            Int64 scan0 = targetData.Scan0.ToInt64();

            for (Int32 y = 0; y < height; ++y)
                if (y * stride < sourceData.Length) Marshal.Copy(sourceData, y * stride, new IntPtr(scan0 + y * targetStride),
                                                       sourceData.Length > y * stride + newDataWidth ? newDataWidth : sourceData.Length - y * stride);

            newImage.UnlockBits(targetData);
            // For indexed images, set the palette.
            if ((pixelFormat & PixelFormat.Indexed) != 0 && (palette != null || defaultColor.HasValue))
            {
                if (palette == null)
                    palette = new Color[0];
                ColorPalette pal = newImage.Palette;
                Int32 palLen = pal.Entries.Length;
                Int32 paletteLength = palette.Length;
                for (Int32 i = 0; i < palLen; ++i)
                {
                    if (i < paletteLength)
                        pal.Entries[i] = palette[i];
                    else if (defaultColor.HasValue)
                        pal.Entries[i] = defaultColor.Value;
                    else
                        break;
                }
                // Palette property getter creates a copy, so the newly filled in palette is
                // not actually referenced in the image until you set it again explicitly.
                newImage.Palette = pal;
            }
            return newImage;
        }
        public static byte[] BitmapToByte(Bitmap map)
        {
            byte[] output = null;
            using (MemoryStream ms = new MemoryStream())
            {
                map.Save(ms, ImageFormat.Png);
                output = ms.ToArray();
            }
            return output;
        }
    }

}

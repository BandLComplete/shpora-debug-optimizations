using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;

namespace JPEG.Images
{
    public class Matrix
    {
        public readonly byte[,] Y;
        public readonly byte[,] Cb;
        public readonly byte[,] Cr;
        public readonly int Height;
        public readonly int Width;

        public Matrix(int height, int width)
        {
            Height = height;
            Width = width;
            Y = new byte[height, width];
            Cb = new byte[height/2, width/2];
            Cr = new byte[height/2, width/2];
        }
        
        public static Matrix FromFile(string fileName, out long length)
        {
	        Bitmap bitmap;
	        using (var fileStream = File.OpenRead(fileName))
	        {
		        length = fileStream.Length;
		        bitmap = new Bitmap(fileStream);
	        }
            
            var height = bitmap.Height - bitmap.Height % 16;
            var width = bitmap.Width - bitmap.Width % 16;
            var matrix = new Matrix(height, width);

            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly,
                bitmap.PixelFormat);
            var pointer = bitmapData.Scan0;
            var border = Math.Abs(bitmapData.Stride) - width * 3;
            unsafe
            {
	            var src = (byte *)pointer.ToPointer();
	            var heightFlag = true;
	            var widthFlag = true;
	            for(var j = 0; j < height; j++)
	            {
		            for (var i = 0; i < width; i++)
		            {
			            var b = *src++;
			            var g = *src++;
			            var r = *src++;
			            matrix.Y[j,i] = ToByte(16.0 + (65.738 * r + 129.057 * g + 24.064 * b) / 256.0);
			            if (widthFlag && heightFlag)
			            {
				            matrix.Cb[j/2,i/2] = ToByte(128.0 + (-37.945 * r - 74.494 * g + 112.439 * b) / 256.0);
				            matrix.Cr[j/2,i/2] = ToByte(128.0 + (112.439 * r - 94.154 * g - 18.285 * b) / 256.0);
			            }

			            widthFlag = !widthFlag;
		            }

		            src += border;
		            heightFlag = !heightFlag;
	            }
            }

            bitmap.UnlockBits(bitmapData);
            return matrix;
        }
        
        public void SaveToFile(string fileName)
        {
            var bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite,
                bitmap.PixelFormat);
            var pointer = bitmapData.Scan0;
            unsafe
            {
                var src = (byte *)pointer.ToPointer();
                for(var j = 0; j < Height; j++)
                for (var i = 0; i < Width; i++)
                {
	                *src++ = ToByte((298.082 * Y[j, i] + 516.412 * Cb[j/2, i/2]) / 256.0 - 276.836);
	                *src++ = ToByte((298.082 * Y[j, i] - 100.291 * Cb[j/2, i/2] - 208.120 * Cr[j/2, i/2]) / 256.0 + 135.576);
	                *src++ = ToByte((298.082 * Y[j, i] + 408.583 * Cr[j/2, i/2]) / 256.0 - 222.921);
                }
            }
            bitmap.Save(fileName, ImageFormat.Bmp);
            bitmap.UnlockBits(bitmapData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte(double d)
        {
            if (d > byte.MaxValue)
                return byte.MaxValue;
            return d < byte.MinValue ? byte.MinValue : (byte)d;
        }
    }
}
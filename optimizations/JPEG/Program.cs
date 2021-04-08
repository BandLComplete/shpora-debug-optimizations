using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JPEG.Images;

namespace JPEG
{
	public class Program
	{
		private const int CompressionQuality = 70;
		public const int DCTSize = 8;

		public static void Main(string[] args)
		{
			try
			{
				Console.WriteLine(IntPtr.Size == 8 ? "64-bit version" : "32-bit version");
				var sw = Stopwatch.StartNew();
				//var fileName = @"sample.bmp";
				//var fileName = @"MARBLES.BMP";
				//var fileName = @"earth.bmp";
				var fileName = @"kot.jpg";
				var compressedFileName = fileName + ".compressed." + CompressionQuality;
				var uncompressedFileName = fileName + ".uncompressed." + CompressionQuality + ".bmp";
				
				var imageMatrix = Matrix.FromFile(fileName, out var length);
				sw.Stop();
				Console.WriteLine($"{imageMatrix.Width}x{imageMatrix.Height} - {length / (1024.0 * 1024):F2} MB");
				sw.Start();
				var compressionResult = Compress(imageMatrix);
				compressionResult.Save(compressedFileName);

				sw.Stop();
				Console.WriteLine("Compression: " + sw.Elapsed);
				sw.Restart();
				var compressedImage = CompressedImage.Load(compressedFileName);
				var uncompressedImage = Uncompress(compressedImage);
				uncompressedImage.SaveToFile(uncompressedFileName);
				Console.WriteLine("Decompression: " + sw.Elapsed);
				Console.WriteLine($"Peak commit size: {MemoryMeter.PeakPrivateBytes() / (1024.0*1024):F2} MB");
				Console.WriteLine($"Peak working set: {MemoryMeter.PeakWorkingSet() / (1024.0*1024):F2} MB");
			}
			catch(Exception e)
			{
				Console.WriteLine(e);
			}
		}

		private static readonly Func<Matrix, (byte[,], int)>[] Selectors =
		{
			m => (m.Y,0), m => (m.Cb,m.Y.Length), m => (m.Cr, m.Y.Length + m.Cb.Length)
		};
		
		private static CompressedImage Compress(Matrix matrix)
		{
			var allQuantizedBytes = new byte[matrix.Y.Length + matrix.Cb.Length + matrix.Cr.Length];
			
			var subMatrix = new sbyte[DCTSize, DCTSize];
			foreach (var selector in Selectors)
			{
				var (channel, index) = selector(matrix);
				var height = channel.GetLength(0);
				var width = channel.GetLength(1);
				for(var y = 0; y < height; y += DCTSize)
				for (var x = 0; x < width; x += DCTSize)
				{
					ReadChannel(channel, y, DCTSize, x, DCTSize, subMatrix);
					FourierTransform.FFT2(subMatrix, Direction.Forward);
					var quantizedFreqs = Quantize(subMatrix);
					var quantizedBytes = ZigZagScan(quantizedFreqs);
					foreach (var b in quantizedBytes)
						allQuantizedBytes[index++] = b;
				}
			}
			
			/*Parallel.ForEach(Selectors, selector =>
			{
				var subMatrix = new sbyte[DCTSize, DCTSize];
				var (channel, index) = selector(matrix);
				var height = channel.GetLength(0);
				var width = channel.GetLength(1);
				for (var y = 0; y < height; y += DCTSize)
				for (var x = 0; x < width; x += DCTSize)
				{
					ReadChannel(channel, y, DCTSize, x, DCTSize, subMatrix);
					FourierTransform.FFT2(subMatrix, Direction.Forward);
					var quantizedFreqs = Quantize(subMatrix);
					var quantizedBytes = ZigZagScan(quantizedFreqs);
					foreach (var b in quantizedBytes)
						allQuantizedBytes[index++] = b;
				}
			});*/

			var compressedBytes = HuffmanCodec.Encode(allQuantizedBytes, out var decodeTable, out var bitsCount);

			return new CompressedImage {Quality = CompressionQuality, CompressedBytes = compressedBytes, BitsCount = bitsCount, DecodeTable = decodeTable, Height = matrix.Height, Width = matrix.Width};
		}
		
		private static Matrix Uncompress(CompressedImage image)
		{
			var result = new Matrix(image.Height, image.Width);
			using (var allQuantizedBytes =
				new MemoryStream(HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount)))
			{
				var quantizedBytes = new byte[DCTSize * DCTSize];
				var subMatrix = new byte[DCTSize,DCTSize];
				var halfHeight = image.Height / 2;
				var halfWidth = image.Width / 2;
				Func<Matrix, (byte[,], int, int)>[] selectors =
				{
					m => (m.Y, image.Height, image.Width), m => (m.Cb, halfHeight, halfWidth),
					m => (m.Cr, halfHeight, halfWidth),
				};

				foreach (var selector in selectors)
				{
					var (channel, height, width) = selector(result);
					for (var y = 0; y < height; y += DCTSize)
					for (var x = 0; x < width; x += DCTSize)
					{
						allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
						var quantizedFreqs = ZigZagUnScan(quantizedBytes);
						var channelFreqs = DeQuantize(quantizedFreqs);
						FourierTransform.FFT2(channelFreqs, Direction.Backward);
						SbytesToBytes(channelFreqs, subMatrix);

						SetPixels(subMatrix, channel, y, x);
					}
				}
			}

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void SbytesToBytes(sbyte[,] array, byte[,] result)
		{
			var height = array.GetLength(0);
			var width = array.GetLength(1);

			for(var y = 0; y < height; y++)
			for(var x = 0; x < width; x++)
				result[y, x] = (byte)(array[y, x] + 128);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void ReadChannel(byte[,] matrix, int yOffset, int yLength, int xOffset, int xLength, sbyte[,] channel)
		{
			for(var j = 0; j < yLength; j++)
			for (var i = 0; i < xLength; i++)
			{
				channel[j, i] = (sbyte)(matrix[yOffset + j, xOffset + i] - 128);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void SetPixels(byte[,] from, byte[,] to, int yOffset, int xOffset)
		{
			var height = from.GetLength(0);
			var width = from.GetLength(1);

			for(var y = 0; y < height; y++)
			for (var x = 0; x < width; x++)
			{
				to[yOffset + y, xOffset + x] = from[y, x];
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static byte[] ZigZagScan(byte[,] channelFreqs)
		{
			return new[]
			{
				channelFreqs[0, 0], channelFreqs[0, 1], channelFreqs[1, 0], channelFreqs[2, 0], channelFreqs[1, 1], channelFreqs[0, 2], channelFreqs[0, 3], channelFreqs[1, 2],
				channelFreqs[2, 1], channelFreqs[3, 0], channelFreqs[4, 0], channelFreqs[3, 1], channelFreqs[2, 2], channelFreqs[1, 3],  channelFreqs[0, 4], channelFreqs[0, 5],
				channelFreqs[1, 4], channelFreqs[2, 3], channelFreqs[3, 2], channelFreqs[4, 1], channelFreqs[5, 0], channelFreqs[6, 0], channelFreqs[5, 1], channelFreqs[4, 2],
				channelFreqs[3, 3], channelFreqs[2, 4], channelFreqs[1, 5],  channelFreqs[0, 6], channelFreqs[0, 7], channelFreqs[1, 6], channelFreqs[2, 5], channelFreqs[3, 4],
				channelFreqs[4, 3], channelFreqs[5, 2], channelFreqs[6, 1], channelFreqs[7, 0], channelFreqs[7, 1], channelFreqs[6, 2], channelFreqs[5, 3], channelFreqs[4, 4],
				channelFreqs[3, 5], channelFreqs[2, 6], channelFreqs[1, 7], channelFreqs[2, 7], channelFreqs[3, 6], channelFreqs[4, 5], channelFreqs[5, 4], channelFreqs[6, 3],
				channelFreqs[7, 2], channelFreqs[7, 3], channelFreqs[6, 4], channelFreqs[5, 5], channelFreqs[4, 6], channelFreqs[3, 7], channelFreqs[4, 7], channelFreqs[5, 6],
				channelFreqs[6, 5], channelFreqs[7, 4], channelFreqs[7, 5], channelFreqs[6, 6], channelFreqs[5, 7], channelFreqs[6, 7], channelFreqs[7, 6], channelFreqs[7, 7]
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static byte[,] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes)
		{
			return new[,]
			{
				{ quantizedBytes[0], quantizedBytes[1], quantizedBytes[5], quantizedBytes[6], quantizedBytes[14], quantizedBytes[15], quantizedBytes[27], quantizedBytes[28] },
				{ quantizedBytes[2], quantizedBytes[4], quantizedBytes[7], quantizedBytes[13], quantizedBytes[16], quantizedBytes[26], quantizedBytes[29], quantizedBytes[42] },
				{ quantizedBytes[3], quantizedBytes[8], quantizedBytes[12], quantizedBytes[17], quantizedBytes[25], quantizedBytes[30], quantizedBytes[41], quantizedBytes[43] },
				{ quantizedBytes[9], quantizedBytes[11], quantizedBytes[18], quantizedBytes[24], quantizedBytes[31], quantizedBytes[40], quantizedBytes[44], quantizedBytes[53] },
				{ quantizedBytes[10], quantizedBytes[19], quantizedBytes[23], quantizedBytes[32], quantizedBytes[39], quantizedBytes[45], quantizedBytes[52], quantizedBytes[54] },
				{ quantizedBytes[20], quantizedBytes[22], quantizedBytes[33], quantizedBytes[38], quantizedBytes[46], quantizedBytes[51], quantizedBytes[55], quantizedBytes[60] },
				{ quantizedBytes[21], quantizedBytes[34], quantizedBytes[37], quantizedBytes[47], quantizedBytes[50], quantizedBytes[56], quantizedBytes[59], quantizedBytes[61] },
				{ quantizedBytes[35], quantizedBytes[36], quantizedBytes[48], quantizedBytes[49], quantizedBytes[57], quantizedBytes[58], quantizedBytes[62], quantizedBytes[63] }
			};
		}

		private static byte[,] Quantize(sbyte[,] channelFreqs)
		{
			var result = new byte[channelFreqs.GetLength(0), channelFreqs.GetLength(1)];

			for(var y = 0; y < channelFreqs.GetLength(0); y++)
			for (var x = 0; x < channelFreqs.GetLength(1); x++)
			{
				result[y, x] = (byte) (channelFreqs[y, x] / QuantizationMatrix[y, x]);
			}

			return result;
		}

		private static sbyte[,] DeQuantize(byte[,] quantizedBytes)
		{
			var result = new sbyte[quantizedBytes.GetLength(0), quantizedBytes.GetLength(1)];

			for(var y = 0; y < quantizedBytes.GetLength(0); y++)
			for (var x = 0; x < quantizedBytes.GetLength(1); x++)
			{
				result[y, x] =
					(sbyte) (quantizedBytes[y, x] * QuantizationMatrix[y, x]); //NOTE cast to sbyte not to loose negative numbers
			}

			return result;
		}

		private static readonly int [,] QuantizationMatrix = GetQuantizationMatrix(CompressionQuality);
		
		private static int[,] GetQuantizationMatrix(int quality)
		{
			if(quality < 1 || quality > 99)
				throw new ArgumentException("quality must be in [1,99] interval");

			var multiplier = quality < 50 ? 5000 / quality : 200 - 2 * quality;

			var result = new[,]
			{
				{16, 11, 10, 16, 24, 40, 51, 61},
				{12, 12, 14, 19, 26, 58, 60, 55},
				{14, 13, 16, 24, 40, 57, 69, 56},
				{14, 17, 22, 29, 51, 87, 80, 62},
				{18, 22, 37, 56, 68, 109, 103, 77},
				{24, 35, 55, 64, 81, 104, 113, 92},
				{49, 64, 78, 87, 103, 121, 120, 101},
				{72, 92, 95, 98, 112, 100, 103, 99}
			};

			for(var y = 0; y < result.GetLength(0); y++)
			for (var x = 0; x < result.GetLength(1); x++)
			{
				result[y, x] = (multiplier * result[y, x] + 50) / 100;
			}

			return result;
		}
	}
}

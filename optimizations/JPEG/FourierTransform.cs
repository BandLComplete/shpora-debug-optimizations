using System;

namespace JPEG
{
    public static class FourierTransform
    {
        private static readonly Complex[,][] ComplexRotation = new Complex[14, 2][];
        private static readonly int[] ReversedBits = {0, 4, 2, 6, 1, 5, 3, 7};
        
        private static readonly Complex[] Data1 = new Complex[Program.DCTSize];
        private static readonly Complex[] Data2 = new Complex[Program.DCTSize];

        public static void FFT2(sbyte[,] data, Direction direction)
        {
	        for (var index1 = 0; index1 < Program.DCTSize; ++index1)
	        {
		        for (var index2 = 0; index2 < Program.DCTSize; ++index2)
			        Data1[index2] = (Complex)data[index1, index2];
		        FFT(Data1, direction);
		        for (var index2 = 0; index2 < Program.DCTSize; ++index2)
			        data[index1, index2] = (sbyte)Data1[index2];
	        }
	        
	        for (var index1 = 0; index1 < Program.DCTSize; ++index1)
	        {
		        for (var index2 = 0; index2 < Program.DCTSize; ++index2)
			        Data2[index2] = (Complex)data[index2, index1];
		        FFT(Data2, direction);
		        for (var index2 = 0; index2 < Program.DCTSize; ++index2)
			        data[index2, index1] = (sbyte)Data2[index2];
	        }
        }
        
        public static void FFT(Complex[] data, Direction direction)
        {
            var length = data.Length;
            const int num1 = 3; //Log2(length);
            ReorderData(data);
            var num2 = 1;
            for (var numberOfBits = 1; numberOfBits <= num1; ++numberOfBits)
            {
                var complexRotation = GetComplexRotation(numberOfBits, direction);
                var num3 = num2;
                num2 <<= 1;
                for (var index1 = 0; index1 < num3; ++index1)
                {
                    var complex1 = complexRotation[index1];
                    for (var index2 = index1; index2 < length; index2 += num2)
                    {
                        var index3 = index2 + num3;
                        var complex2 = data[index2];
                        var complex3 = data[index3];
                        var num4 = complex3.Re * complex1.Re - complex3.Im * complex1.Im;
                        var num5 = complex3.Re * complex1.Im + complex3.Im * complex1.Re;
                        data[index2].Re += num4;
                        data[index2].Im += num5;
                        data[index3].Re = complex2.Re - num4;
                        data[index3].Im = complex2.Im - num5;
                    }
                }
            }
            if (direction != Direction.Forward) return;
            for (var index = 0; index < length; ++index)
            { 
	            data[index].Re /= length;
	            data[index].Im /= length;
            }
        }

        

        private static Complex[] GetComplexRotation(int numberOfBits, Direction direction)
        {
	        var index1 = direction == Direction.Forward ? 0 : 1;
	        if (ComplexRotation[numberOfBits - 1, index1] == null)
	        {
		        var length = 1 << numberOfBits - 1;
		        var re = 1.0;
		        var im = 0.0;
		        var num1 = Math.PI / length * (double) direction;
		        var num2 = Math.Cos(num1);
		        var num3 = Math.Sin(num1);
		        var complexArray = new Complex[length];
		        for (var index2 = 0; index2 < length; ++index2)
		        {
			        complexArray[index2] = new Complex(re, im);
			        var num4 = re * num3 + im * num2;
			        re = re * num2 - im * num3;
			        im = num4;
		        }
		        ComplexRotation[numberOfBits - 1, index1] = complexArray;
	        }
	        return ComplexRotation[numberOfBits - 1, index1];
        }

        private static void ReorderData(Complex[] data)
        {
	        var length = data.Length;
	        for (var index1 = 0; index1 < length; ++index1)
	        {
		        var index2 = ReversedBits[index1];
		        if (index2 > index1)
		        {
			        var complex = data[index1];
			        data[index1] = data[index2];
			        data[index2] = complex;
		        }
	        }
        }
    }
}
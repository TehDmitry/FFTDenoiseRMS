using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FFTDenoiseRMS
{
    class DFT
    {
        public static Complex[] Transform(Complex[] input)
        {
            int N = input.Length;

            Complex[] output = new Complex[N];

            double arg = -2.0 * Math.PI / (double)N;
            for (int n = 0; n < N; n++)
            {
                output[n] = new Complex();
                for (int k = 0; k < N; k++)
                    output[n] += input[k] * Complex.FromPolarCoordinates(1, arg * (double)n * (double)k);
            }
            return output;
        }

        public static Complex[] getSine(int numberValues, double samplingFrequency)
        {
            Complex[] input = new Complex[numberValues];
            double t = 0;
            double y = 0;
            for (int i = 0; i < numberValues; i++)
            {
                t = (double)i / (double)samplingFrequency;
                y = Math.Sin(2 * Math.PI * t);
                input[i] = new Complex(y, 0);
            }

            return input;
        }

        public static short[] getSineShort(int numberValues, double samplingFrequency)
        {
            short[] input = new short[numberValues];
            double t = 0;
            double y = 0;
            for (int i = 0; i < numberValues; i++)
            {
                t = (double)i / (double)samplingFrequency;
                y = Math.Sin(2 * Math.PI * t);
                input[i] = (short)y;
            }

            return input;
        }


        public static List<KeyValuePair<double, double>> test(Complex[] input)
        {
            for (int i = 0; i < input.Length; i++)
            {

            }


            Complex[] output = DFT.Transform(input);

            List<KeyValuePair<double, double>> result = new List<KeyValuePair<double, double>>();

            for (int i = 0; i < input.Length; i++)
            {
                var tempComplex = new System.Numerics.Complex(output[i].Real, output[i].Imaginary);
                //KeyValuePair<double, double> item = new KeyValuePair<double, double>(i, 20 * Math.Log10(tempComplex.Magnitude));
                KeyValuePair<double, double> item = new KeyValuePair<double, double>(i, tempComplex.Magnitude);
                result.Add(item);
            }
            return result;
        }
    }
}

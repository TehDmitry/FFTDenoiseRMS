using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NAudio.Wave;

namespace FFTDenoiseRMS
{
    class FFT
    {
        // https://dsp.stackexchange.com/questions/32076/fft-to-spectrum-in-decibel
        ///
        // http://www.comm.pub.ro/diploma/Noise/JSIP_Spectral_subtraction.pdf
        // http://caxapa.ru/thumbs/455725/algorithms.pdf
        // Ephraim-Malah algorithm ?
        // https://stackoverflow.com/questions/5879851/how-can-the-noise-be-removed-from-a-recorded-sound-using-fft-in-matlab
        // https://dsp.stackexchange.com/questions/346/rms-calculation-in-frequency-domain-after-windowing
        // rms(signal*windows) = rms(signal)*rms(window)
        // https://blog.prosig.com/2015/01/06/rms-from-time-history-and-fft-spectrum/
        // https://www.softdb.com/difference-between-db-dba/
        // https://groups.google.com/forum/embed/#!topic/accord-net/YABuOlqXQoE
        // https://dsp.stackexchange.com/questions/14808/spl-values-from-fft-of-microphone-signal
        // 
        // https://dsp.stackexchange.com/questions/45909/recognize-unwanted-sounds-noises-and-deleting-them-algorithm?rq=1
        // https://dsp.stackexchange.com/questions/7593/eliminate-signal-a-from-signal-b
        // 
        // https://github.com/ar1st0crat/NWaves/wiki/Transforms#short-time-fourier-transform
        // https://blog.prosig.com/2015/01/06/rms-from-time-history-and-fft-spectrum/
        // 
        // http://www.websound.ru/articles/theory/fft.htm

        readonly int fft_size;

        double[] window;
        double sum_win_s1;
        double sum_win_s2;
        double ENBW;


        public double minFreq = 20;
        public double maxFreq = 22050;

        int sample_rate = 44100;


        float[] realSamples;

        double[] avgMagnitude;

        public double[] AvgSamples { get => avgMagnitude; }
        public double RMS { get => _RMS; }

        double _RMS;

        public FFT(int fft_size)
        {
            this.fft_size = fft_size;

            window = new double[fft_size];
            avgMagnitude = new double[fft_size];

            sum_win_s1 = 0;
            sum_win_s2 = 0;
            for (int i = 0; i < fft_size; i++)
            {
                double multiplier = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (fft_size - 1));

                //multiplier = (float)(NAudio.Dsp.FastFourierTransform.BlackmannHarrisWindow(i, fft_size));
                multiplier = (float)(NAudio.Dsp.FastFourierTransform.HammingWindow(i, fft_size));
                //multiplier = 1; //temp disable window 

                sum_win_s1 += multiplier;
                sum_win_s2 += multiplier * multiplier;

                window[i] = multiplier;
            }

            ENBW = sample_rate * sum_win_s2 / Math.Pow(sum_win_s1, 2);
        }

        public void readSamples(String filename)
        {
            short[] samples;

            // float[] realSamples;

            using (AudioFileReader reader = new AudioFileReader(filename))
            {
                ISampleProvider chReader = reader;
                if (reader.WaveFormat.Channels == 2)
                {
                    var mono = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(reader);
                    mono.LeftVolume = 1.0f; // 
                    mono.RightVolume = 0.0f; //

                    //mono.LeftVolume = 0.5f; // discard the left channel
                    //mono.RightVolume = 0.5f; // keep the right channel
                    chReader = mono;
                }

                sample_rate = reader.WaveFormat.SampleRate;

                //realSamples = new float[reader.WaveFormat.SampleRate];
                //chReader.Read(realSamples, 0, realSamples.Length);

                realSamples = new float[reader.TotalTime.Seconds * reader.WaveFormat.SampleRate];
                chReader.Read(realSamples, 0, realSamples.Length);
            }

            float sumOfSamples = 0;
            for (int i = 0; i < realSamples.Length; i++)
            {
                sumOfSamples += realSamples[i] * realSamples[i];
            }

            _RMS = Math.Sqrt(sumOfSamples / realSamples.Length);
            _RMS = Math.Round(20 * Math.Log10(_RMS), 1);

            avgMagnitude = getAvgFFTSamples(realSamples, fft_size);
        }


        public double[] getAvgFFTSamples(float[] audioSamples, int fft_size)
        {
            /*
             Note that the averaging must be done with the power spectrum
            (PS) or the power spectral density (PSD), not with their square roots LS or LSD.
             */


            var meanMags = new double[fft_size];
            int numSignals = 0;

            for (int startPos = 0; startPos < audioSamples.Length - fft_size; startPos += fft_size / 2)
            {
                float[] buffer = new float[fft_size];

                Array.Copy(audioSamples, startPos, buffer, 0, fft_size);

                //double[] fftResult = FFT_AForge(buffer, fft_size);
                double[] fftResult = FFT_MathNET(buffer, fft_size);

                for (int i = 0; i < fft_size; i++)
                {
                    meanMags[i] += fftResult[i];
                }
                numSignals++;
            }

            for (int i = 0; i < fft_size; i++)
            {
                meanMags[i] /= numSignals;
            }

            return meanMags;
        }

        public double[] FFT_MathNET(float[] buffer, int fft_size)
        {
            double[] result = new double[fft_size];
            float average = buffer.Average(); // for DC and trend removal
            var complexBuffer = new MathNet.Numerics.Complex32[fft_size];
            for (int i = 0; i < fft_size; i++)
            {
                complexBuffer[i] = new MathNet.Numerics.Complex32((float)((buffer[i] - average) * window[i]), 0);
            }

            MathNet.Numerics.IntegralTransforms.Fourier.Forward(complexBuffer, MathNet.Numerics.IntegralTransforms.FourierOptions.NoScaling);
            for (int i = 1; i < fft_size; i++)
            {
                result[i] = Math.Pow(complexBuffer[i].Magnitude, 2);
            }

            return result;
        }

        public double[] FFT_AForge(float[] buffer, int fft_size)
        {
            double[] result = new double[fft_size];

            float average = buffer.Average(); // for DC and trend removal

            var complexBuffer = new AForge.Math.Complex[fft_size];
            for (int i = 0; i < fft_size; i++)
            {
                complexBuffer[i].Re = (buffer[i] - average) * window[i];
                complexBuffer[i].Im = 0;

            }

            AForge.Math.FourierTransform.FFT(complexBuffer, AForge.Math.FourierTransform.Direction.Backward);
            //AForge.Math.FourierTransform.FFT(complexBuffer, AForge.Math.FourierTransform.Direction.Forward);

            for (int i = 1; i < fft_size / 2; i++)
            {
                /*
                 * Whenever doing windowing, it is necessary to compensate for loss of energy due to multiplication by that window. 
                 * This is defined as division by sum of window samples (sum(win)). 
                 * In case of rectangular window (or now window), it is as simple as division by N, where N is the DFT length.                 
                 */

                /*
                 * Scale the magnitude of FFT by window and factor of 2,
                 * because we are using half of FFT spectrum.
                */

                //result[i] = (complexBuffer[i].Magnitude * 2) / (sum_win_s1);// * Math.Sqrt(2);
                //result[i] = (complexBuffer[i].Magnitude * 2) / (sum_win_s1 * sum_win_s1);// * Math.Sqrt(2);
                //result[i] = (complexBuffer[i].Magnitude / sum_win);// * Math.Sqrt(2);

                result[i] = Math.Pow(complexBuffer[i].Magnitude, 2);

            }

            return result;
        }


        public List<KeyValuePair<double, double>> getFFT()
        {
            List<KeyValuePair<double, double>> result = new List<KeyValuePair<double, double>>();
            for (int i = 0; i < avgMagnitude.Length / 2; i++)
            {
                double freq = i * (float)sample_rate / (float)avgMagnitude.Length;
                if (freq >= minFreq && freq <= maxFreq)
                {
                    KeyValuePair<double, double> item = new KeyValuePair<double, double>(freq, getdBVal(avgMagnitude[i])); //
                    result.Add(item);
                }
            }
            return result;
        }

        private double getdBVal(double fftVal)
        {
            //PS RMS (23)
            double value = (2 * fftVal) / (sum_win_s1 * sum_win_s1);
            //LS
            value = Math.Sqrt(value);

            //PSD RMS
            //double value = (2 * avgSamples[i]) / (sample_rate * sum_win_s2);
            //LSD
            //value = Math.Sqrt(value);

            // https://dsp.stackexchange.com/questions/8785/how-to-compute-dbfs
            double valuedB = 20 * Math.Log10(value * Math.Sqrt(2));

            if (Double.IsNaN(valuedB) || value == 0 || valuedB < -96)
            {
                valuedB = -96;
            }

            return valuedB;
        }


        public double getRMS()
        {
            double sum = 0;
            int len = 0;

            for (int i = 0; i < fft_size / 2; i++)
            {
                double freq = i * (float)sample_rate / (float)fft_size;
                if (freq >= minFreq && freq <= maxFreq)
                {
                    sum += avgMagnitude[i];// * value;
                    len++;
                }
            }

            return 20 * Math.Log10(Math.Sqrt(sum / (len * len)));
        }

        public void subtractNoise(double[] fft_noise)
        {
            for (int i = 0; i < fft_size; i++)
            {
                if (getdBVal(avgMagnitude[i]) - getdBVal(fft_noise[i]) > 10)
                {

                    avgMagnitude[i] = avgMagnitude[i] - fft_noise[i];

                    if (avgMagnitude[i] < 0)
                    {
                        avgMagnitude[i] = 0;
                    }
                }
                else
                {
                    avgMagnitude[i] = 0;
                }
            }
        }


        public void denoise(String inFileName, String outFileName, int dbLimit)
        {
            float[] samples;
            float[] samplesOut;

            using (AudioFileReader reader = new AudioFileReader(inFileName))
            {
                ISampleProvider chReader = reader;
                if (reader.WaveFormat.Channels == 2)
                {
                    var mono = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(reader);
                    mono.LeftVolume = 1.0f; // 
                    mono.RightVolume = 0.0f; //

                    //mono.LeftVolume = 0.5f; // discard the left channel
                    //mono.RightVolume = 0.5f; // keep the right channel
                    chReader = mono;
                }

                //samples = new float[reader.WaveFormat.SampleRate * reader.Length];
                samples = new float[reader.TotalTime.Seconds * reader.WaveFormat.SampleRate];
                chReader.Read(samples, 0, samples.Length);
            }

            samplesOut = new float[samples.Length];

            for (int startPos = 0; startPos < samples.Length - fft_size; startPos += fft_size)
            {
                float[] buffer = new float[fft_size];

                Array.Copy(samples, startPos, buffer, 0, fft_size);

                float average = buffer.Average(); // for DC and trend removal
                var complexBufferIn = new MathNet.Numerics.Complex32[fft_size];
                var complexBufferOut = new MathNet.Numerics.Complex32[fft_size];

                for (int i = 0; i < fft_size; i++)
                {
                    //complexBufferIn[i] = new MathNet.Numerics.Complex32((float)((buffer[i] - average) * window[i]), 0);
                    complexBufferIn[i] = new MathNet.Numerics.Complex32((float)((buffer[i] - average)), 0);
                }

                MathNet.Numerics.IntegralTransforms.Fourier.Forward(complexBufferIn, MathNet.Numerics.IntegralTransforms.FourierOptions.NoScaling);

                for (int i = 0; i < fft_size; i++)
                {
                    float magDiff = (float)complexBufferIn[i].Magnitude;

                    if (dbLimit >= 0)
                    {
                        double dbDiff = getdBVal(complexBufferIn[i].Magnitude) - getdBVal(avgMagnitude[i]);
                        if (dbDiff < dbLimit)
                        {
                            magDiff = 0;
                        }
                    }
                    else
                    {
                        magDiff -= (float)Math.Sqrt(avgMagnitude[i]);
                        if (magDiff < 0)
                        {
                            magDiff = 0;
                        }
                    }

                    magDiff = (float)(magDiff / Math.Sqrt(fft_size)); //normalize for default transform
                    //magDiff = (float)(magDiff / sum_win_s1);
                    complexBufferOut[i] = new MathNet.Numerics.Complex32((float)(magDiff * Math.Cos(complexBufferIn[i].Phase)), (float)(magDiff * Math.Sin(complexBufferIn[i].Phase)));
                }

                MathNet.Numerics.IntegralTransforms.Fourier.Inverse(complexBufferOut, MathNet.Numerics.IntegralTransforms.FourierOptions.Default);
                //MathNet.Numerics.IntegralTransforms.Fourier.Inverse(complexBufferOut, MathNet.Numerics.IntegralTransforms.FourierOptions.NoScaling);

                for (int i = 0; i < fft_size; i++)
                {
                    samplesOut[startPos + i] = complexBufferOut[i].Real;
                }
            }

            using (WaveFileWriter writer = new WaveFileWriter(outFileName, new WaveFormat(44100, 1)))
            {
                writer.WriteSamples(samplesOut, 0, samplesOut.Length);
            }

            avgMagnitude = getAvgFFTSamples(realSamples, fft_size);
        }
    }
}

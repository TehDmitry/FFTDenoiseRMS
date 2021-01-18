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


        public double minFreq = 10;
        public double maxFreq = 22050;

        int sample_rate = 44100;


        float[] realSamples;

        double[] avgSamples;

        public double[] AvgSamples { get => avgSamples; }

        public FFT(int fft_size)
        {
            this.fft_size = fft_size;

            window = new double[fft_size];
            avgSamples = new double[fft_size];

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

                var pcm = new NAudio.Wave.SampleProviders.SampleToWaveProvider16(chReader);

                var buffer = new byte[2 * chReader.WaveFormat.SampleRate];
                pcm.Read(buffer, 0, buffer.Length);

                //samples = buffer.Select(b => (short)b).ToArray();
                samples = new short[(int)Math.Ceiling((double)buffer.Length / 2)];
                Buffer.BlockCopy(buffer, 0, samples, 0, buffer.Length);

                sample_rate = reader.WaveFormat.SampleRate;

                realSamples = new float[reader.WaveFormat.SampleRate];
                chReader.Read(realSamples, 0, realSamples.Length);
            }

            avgSamples = getAvgFFTSamples(realSamples, fft_size);
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

                double[] fftResult = FFT_AForge(buffer, fft_size);

                for (int i = 0; i < fft_size; i++)
                {
                    meanMags[i] += fftResult[i];
                }
                numSignals++;
            }

            List<KeyValuePair<double, double>> result = new List<KeyValuePair<double, double>>();
            for (int i = 0; i < fft_size; i++)
            {
                meanMags[i] /= numSignals;
            }

            return meanMags;
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

            //AForge.Math.FourierTransform.FFT(complexBuffer, AForge.Math.FourierTransform.Direction.Backward);
            AForge.Math.FourierTransform.FFT(complexBuffer, AForge.Math.FourierTransform.Direction.Forward);

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

            for (int i = 0; i < avgSamples.Length / 2; i++)
            {
                double freq = i * (float)sample_rate / (float)avgSamples.Length;

                //PS RMS (23)
                double value = (2 * avgSamples[i]) / (sum_win_s1 * sum_win_s1);
                //LS
                value = Math.Sqrt(value);

                //PSD RMS
                //double value = (2 * avgSamples[i]) / (sample_rate * sum_win_s2);
                //LSD
                //value = Math.Sqrt(value);


                if (freq >= minFreq && freq <= maxFreq)
                {
                    double valuedB = 20 * Math.Log10(value); // + 75;
                    if (Double.IsNaN(valuedB)) // || valuedB < -96
                    {
                        valuedB = -96;
                    }

                    KeyValuePair<double, double> item = new KeyValuePair<double, double>(freq, valuedB); //
                    //KeyValuePair<double, double> item = new KeyValuePair<double, double>(freq, 10 * Math.Log10(value)); // + 134
                    result.Add(item);
                }
            }

            return result;
        }


        public double getRMS()
        {

            double decibel;
            double sum = 0;
            int len = 0;

            for (int i = 0; i < fft_size / 2; i++)
            {
                double freq = i * (float)sample_rate / (float)fft_size;
                if (freq >= minFreq && freq <= maxFreq)
                {
                    //sum += Math.Pow(fft[i], 2);

                    //PSD RMS
                    double value = (2 * avgSamples[i]) / (sample_rate * sum_win_s2);
                    //LSD
                    //value = Math.Sqrt(value);

                    sum += value;
                    len++;
                }
            }

            //double rmsa = Math.Sqrt(sum / (fft.Length * fft.Length / 4));

            //decibel = 20 * Math.Log10(Math.Sqrt(sum / Math.Pow(len, 2))) + 201; // + 273; // + 339.5; // + 131.8; + 204

            decibel = 10 * Math.Log10(Math.Sqrt(sum / Math.Pow(len, 2))) + 73;

            return decibel;
        }

        public void subtractNoise(double[] fft_noise)
        {
            for (int i = 0; i < fft_size; i++)
            {
                avgSamples[i] = avgSamples[i] - fft_noise[i];
            }
        }
        public short[] restoreFromFFT(double[] fft)
        {
            short[] samples = new short[fft_size];
            double[] samples_alt = new double[fft_size];


            for (int i = 0; i < fft_size; i++)
            {
                double summ = 0;

                for (int k = 0; k < fft_size; k++)
                {
                    // val += waves[n]*np.exp(1.j * 2*np.pi * n * k / len(waves))
                    //System.Numerics.Complex j = System.Numerics.Complex.ImaginaryOne * -1;
                    // / 32768
                    System.Numerics.Complex result = (fft[k] / 32768) * System.Numerics.Complex.Exp(System.Numerics.Complex.ImaginaryOne * 2 * Math.PI * k / fft_size);
                    // System.Numerics.Complex result = fft[k];
                    //System.Numerics.Complex result = System.Numerics.Complex.Exp(System.Numerics.Complex.ImaginaryOne * 2 * Math.PI * k / fft_size);

                    summ += result.Magnitude;

                }

                samples[i] = (short)summ;

            }



            var complexBuffer = new AForge.Math.Complex[fft_size];
            for (int i = 0; i < fft_size; i++)
            {
                complexBuffer[i].Re = Math.Sqrt(fft[i] * sum_win_s1 / 2.0);
                complexBuffer[i].Im = 0;
            }

            AForge.Math.FourierTransform.FFT(complexBuffer, AForge.Math.FourierTransform.Direction.Forward);

            for (int i = 0; i < fft_size; i++)
            {
                double s_mag = complexBuffer[i].Magnitude * 2.0 / sum_win_s1;  // Scale the magnitude of FFT by window and factor of 2,
                                                                               // because we are using half of FFT spectrum

                //samples[i] = (double)s_mag;

                samples_alt[i] = (double)s_mag;
            }

            return samples;
        }
    }
}

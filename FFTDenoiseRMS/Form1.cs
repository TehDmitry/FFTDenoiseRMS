﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace FFTDenoiseRMS
{
    public partial class Form1 : Form
    {
        //private int FFT_SIZE = 8192;
        private int FFT_SIZE = 2048 * 2; //for show lo-freq
                                         //private int FFT_SIZE = 2048;
                                         //private int FFT_SIZE = 1024;
                                         //private int FFT_SIZE = 8192* 2;
                                         //private int FFT_SIZE = 2048;

        //private int FFT_SIZE = 256;
        //private int FFT_SIZE = 512;
        //private int FFT_SIZE = 4;

        private double max = -1000;

        private Recorder recorder;

        private double RMS_sig = -120;
        private double RMS_noise = -120;
        private double RMS_diff = -120;


        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void addPoint(int serieIndex, KeyValuePair<double, double> fftItem)
        {

            if (fftItem.Value > -1000)
            {

                chart1.Series[serieIndex].Points.AddXY(Math.Round(fftItem.Key), Math.Round(fftItem.Value, 2));
                if (fftItem.Value > max)
                {
                    max = Math.Round(fftItem.Value, 2);
                }
            }
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            if (recorder != null && recorder.recording())
            {
                Text = "REC";
            }
            else
            {
                Text = "IDLE";
            }
        }

        private void drawResults(String sigName, String noiseName)
        {
            for (int i = 0; i < 3; i++)
            {
                chart1.Series[i].Points.Clear();
            }

            FFT fftSignal = new FFT(FFT_SIZE);
            FFT fftNoise = new FFT(FFT_SIZE);
            FFT fftDiff = new FFT(FFT_SIZE);

            fftSignal.readSamples(sigName);
            fftNoise.readSamples(noiseName);

            List<KeyValuePair<double, double>> result_1 = fftSignal.getFFT();
            List<KeyValuePair<double, double>> result_2 = fftNoise.getFFT();
            
            RMS_sig = Math.Round(fftSignal.getRMS(), 1);
            RMS_noise = Math.Round(fftNoise.getRMS(), 1);
            
            fftSignal.subtractNoise(fftNoise.AvgSamples);


            List<KeyValuePair<double, double>> result_diff = fftSignal.getFFT();
            RMS_diff = Math.Round(fftSignal.getRMS(), 1);


            max = -1000;
            for (int i = 0; i < result_1.Count; i++)
            {
                addPoint(0, result_1[i]);
                addPoint(1, result_2[i]);
                addPoint(2, result_diff[i]);
                if (result_1[i].Value > max)
                {
                    max = result_1[i].Value;
                }
            }

            label1.Text = max.ToString();



            

            chart1.Series[0].Name = "Signal " + RMS_sig.ToString() + " dBFS";
            chart1.Series[1].Name = "Noise " + RMS_noise.ToString() + " dBFS";
            chart1.Series[2].Name = "Diff " + RMS_diff.ToString() + " dBFS";

            chart1.ChartAreas[0].AxisX.Maximum = fftSignal.maxFreq;
            chart1.ChartAreas[0].AxisX.Minimum = fftSignal.minFreq;

            //chart1.ChartAreas[0].AxisY.Minimum = -100;
        }

        private void btDraw_Click(object sender, EventArgs e)
        {
            //drawResults("..\\..\\audio_samples\\sig.wav", "..\\..\\audio_samples\\noise.wav");
            drawResults("..\\..\\audio_samples\\Sine_440hz_0dB_10seconds_44.1khz_16bit_mono.wav", "..\\..\\audio_samples\\noise.wav");
        }



        private void button1_Click(object sender, EventArgs e)
        {
            System.IO.Directory.CreateDirectory(".\\Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\");

            recorder = new Recorder(0, "Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\", "noise.wav", 20);
            System.Threading.Thread.Sleep(100);
            recorder.StartRecording();
        }


        private void btRecSig_Click(object sender, EventArgs e)
        {
            System.IO.Directory.CreateDirectory(".\\Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\");

            recorder = new Recorder(0, "Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\", "sig.wav", 20);
            System.Threading.Thread.Sleep(100);
            recorder.StartRecording();
        }


        private void btnShow_Click(object sender, EventArgs e)
        {
            drawResults("Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\sig.wav", "Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\noise.wav");
        }

        private void btnWrite_Click(object sender, EventArgs e)
        {
            drawResults("Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\sig.wav", "Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\noise.wav");
            chart1.SaveImage("Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\result.png", System.Windows.Forms.DataVisualization.Charting.ChartImageFormat.Png);

            StringBuilder sb = new StringBuilder();

            sb.Append("RMS signal: ");
            sb.Append(RMS_sig);
            sb.Append("\n");

            sb.Append("RMS noise: ");
            sb.Append(RMS_noise);
            sb.Append("\n");

            sb.Append("RMS result: ");
            sb.Append(RMS_diff);
            sb.Append("\n");

            using (System.IO.StreamWriter sw = System.IO.File.CreateText("Measurements\\" + tbName.Text + "\\" + tbSpeed.Text + "\\result.txt"))
            {
                sw.Write(sb.ToString());
            }

        }

        private void cbLogScale_CheckStateChanged(object sender, EventArgs e)
        {

            chart1.SuppressExceptions = true;
            chart1.ChartAreas[0].AxisX.IsLogarithmic = cbLogScale.Checked;
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.IO;

namespace FFTDenoiseRMS
{
    public class Recorder
    {
        WaveIn sourceStream;
        WaveFileWriter waveWriter;
        readonly String FilePath;
        readonly String FileName;
        readonly int InputDeviceIndex;
        readonly int dataLimit;

        readonly int sampleRate = 44100;
        private object myLock = new object();

        public Recorder(int inputDeviceIndex, String filePath, String fileName, int seconds)
        {
            this.InputDeviceIndex = inputDeviceIndex;
            this.FileName = fileName;
            this.FilePath = filePath;

            this.dataLimit = sampleRate * seconds;
        }

        public void StartRecording()
        {
            sourceStream = new WaveIn
            {
                DeviceNumber = this.InputDeviceIndex,
                WaveFormat =
                    new WaveFormat(sampleRate, WaveIn.GetCapabilities(this.InputDeviceIndex).Channels)
            };

            sourceStream.DataAvailable += this.SourceStreamDataAvailable;
            sourceStream.RecordingStopped += this.onRecordingStopped;

            if (!Directory.Exists(FilePath))
            {
                Directory.CreateDirectory(FilePath);
            }

            waveWriter = new WaveFileWriter(FilePath + FileName, sourceStream.WaveFormat);
            sourceStream.StartRecording();
        }

        public void SourceStreamDataAvailable(object sender, WaveInEventArgs e)
        {
            if (waveWriter == null) return;
            waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            waveWriter.Flush();

            if (waveWriter.Length >= dataLimit)
            {
                sourceStream.StopRecording();
            }

        }

        public void onRecordingStopped(object sender, StoppedEventArgs e)
        {
            RecordEnd();
        }

        private void RecordEnd()
        {

                if (sourceStream != null)
                {
                    sourceStream.StopRecording();
                    sourceStream.Dispose();
                    sourceStream = null;
                }
                if (this.waveWriter == null)
                {
                    return;
                }
                this.waveWriter.Dispose();
                this.waveWriter = null;
        }

        internal bool recording()
        {
            return (sourceStream != null);
        }
    }
}

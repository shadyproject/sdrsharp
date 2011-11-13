using System;
using SDRSharp.Radio.PortAudio;

namespace SDRSharp.Radio
{
    public delegate void BufferNeededDelegate(Complex[] iqBuffer, double[] audioBuffer);

    public class AudioControl
    {
        private const double InputGain = 0.01;

        private double[] _audioBuffer;
        private Complex[] _iqBuffer;
        private Complex[] _recorderIQBuffer;

        private WavePlayer _player;
        private WaveRecorder _recorder;
        private WaveFile _waveFile;
        private FifoStream<Complex> _audioStream;

        private int _sampleRate;
        private int _inputDevice;
        private int _bufferSizeInMs;
        private int _outputDevice;

        public event BufferNeededDelegate BufferNeeded;

        public AudioControl()
        {
            AudioGain = 10.0;
        }

        public double AudioGain { get; set; }

        public bool SwapIQ { get; set; }

        public int SampleRate
        {
            get
            {
                return _sampleRate;
            }
        }

        private void PlayerFiller(float[] buffer)
        {
            if (BufferNeeded == null)
            {
                return;
            }

            if (_audioBuffer == null || _audioBuffer.Length != buffer.Length / 2)
            {
                _audioBuffer = new double[buffer.Length / 2];
            }

            if (_iqBuffer == null || _iqBuffer.Length != buffer.Length / 2)
            {
                _iqBuffer = new Complex[buffer.Length / 2];
            }

            if (_waveFile != null)
            {
                _waveFile.Read(_iqBuffer);
            }
            else
            {
                _audioStream.Read(_iqBuffer, 0, _iqBuffer.Length);
            }

            BufferNeeded(_iqBuffer, _audioBuffer);
                
            double audioGain = Math.Pow(AudioGain / 10.0, 10);

            for (var i = 0; i < _audioBuffer.Length; i++)
            {
                var audio = (float) (_audioBuffer[i] * audioGain);
                buffer[i * 2] = audio;
                buffer[i * 2 + 1] = audio;
            }
        }

        private void RecorderFiller(float[] buffer)
        {
            if (_audioStream.Length > buffer.Length * 2)
            {
                return;
            }

            #region Fill IQ buffer

            if (_recorderIQBuffer == null || _recorderIQBuffer.Length != buffer.Length / 2)
            {
                _recorderIQBuffer = new Complex[buffer.Length / 2];
            }

            for (var i = 0; i < _recorderIQBuffer.Length; i++)
            {
                _recorderIQBuffer[i].Real = buffer[i * 2] * InputGain;
                _recorderIQBuffer[i].Imag = buffer[i * 2 + 1] * InputGain;
            }

            #endregion

            #region Fill the FiFo

            _audioStream.Write(_recorderIQBuffer, 0, _recorderIQBuffer.Length);

            #endregion
        }

        public void Stop()
        {
            if (_player != null)
            {
                try
                {
                    _player.Dispose();
                }
                finally
                {
                    _player = null;
                }
            }
            if (_recorder != null)
            {
                try
                {
                    _recorder.Dispose();
                }
                finally
                {
                    _recorder = null;
                }
            }
            if (_audioStream != null)
            {
                try
                {
                    _audioStream.Close();
                }
                finally
                {
                    _audioStream = null;
                }
            }
            if (_waveFile != null)
            {
                try
                {
                    _waveFile.Dispose();
                }
                finally
                {
                    _audioStream = null;
                }
            }
            _sampleRate = 0;
        }

        public bool Play()
        {
            try
            {
                if (_player == null)
                {
                    var bufferSize = _bufferSizeInMs * _sampleRate / 1000;

                    if (_waveFile == null)
                    {
                        _audioStream = new FifoStream<Complex>();

                        _recorder = new WaveRecorder(_inputDevice, _sampleRate, bufferSize, RecorderFiller);
                    }
                    _player = new WavePlayer(_outputDevice, _sampleRate, bufferSize, PlayerFiller);
                    return true;
                }
            }
            catch
            {
                Stop();
                throw;
            }
            return false;
        }

        public void OpenDevice(int inputDevice, int outputDevice, int sampleRate, int bufferSizeInMs)
        {
            Stop();

            _bufferSizeInMs = bufferSizeInMs;
            _inputDevice = inputDevice;
            _outputDevice = outputDevice;
            _sampleRate = sampleRate;
        }

        public void OpenFile(string filename, int outputDevice, int bufferSizeInMs)
        {
            Stop();

            try
            {
                _outputDevice = outputDevice;
                _bufferSizeInMs = bufferSizeInMs;
                _waveFile = new WaveFile(filename);
                _sampleRate = _waveFile.SampleRate;
            }
            catch (Exception)
            {
                Stop();
            }
        }
    }
}
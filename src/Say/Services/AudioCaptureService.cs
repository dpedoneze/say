using NAudio.Wave;

namespace Say.Services;

public sealed class AudioCaptureService : IDisposable
{
    public const int SampleRate = 16000;

    private readonly object _lock = new();
    private readonly List<float> _samples = new(SampleRate * 120);
    private readonly float[] _bars = new float[9];

    private WaveInEvent? _device;
    private DateTime _startedAtUtc;
    private int _buffersForNextBar;

    public bool IsCapturing { get; private set; }

    public Exception? LastError { get; private set; }

    public TimeSpan Elapsed =>
        IsCapturing ? DateTime.UtcNow - _startedAtUtc : TimeSpan.Zero;

    public int SampleCount
    {
        get
        {
            lock (_lock)
            {
                return _samples.Count;
            }
        }
    }

    public float[] Bars
    {
        get
        {
            lock (_lock)
            {
                return (float[])_bars.Clone();
            }
        }
    }

    public static IReadOnlyList<(int Number, string Name)> ListDevices()
    {
        var list = new List<(int, string)>();
        try
        {
            var count = WaveIn.DeviceCount;
            for (var i = 0; i < count; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                list.Add((i, caps.ProductName));
            }
        }
        catch
        {
            // No audio subsystem available.
        }

        return list;
    }

    public void Start(int deviceNumber)
    {
        Stop();

        LastError = null;
        lock (_lock)
        {
            _samples.Clear();
            Array.Clear(_bars);
            _buffersForNextBar = 0;
        }

        var device = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(SampleRate, 16, 1),
            BufferMilliseconds = 40,
            NumberOfBuffers = 4,
        };

        device.DataAvailable += OnDataAvailable;
        device.RecordingStopped += OnRecordingStopped;

        try
        {
            device.StartRecording();
        }
        catch
        {
            device.DataAvailable -= OnDataAvailable;
            device.RecordingStopped -= OnRecordingStopped;
            device.Dispose();
            throw;
        }

        _device = device;
        _startedAtUtc = DateTime.UtcNow;
        IsCapturing = true;
    }

    public void Stop()
    {
        var device = _device;
        _device = null;
        IsCapturing = false;

        if (device is null)
        {
            return;
        }

        try
        {
            device.StopRecording();
        }
        catch
        {
            // Already stopped.
        }

        device.DataAvailable -= OnDataAvailable;
        device.RecordingStopped -= OnRecordingStopped;
        device.Dispose();
    }

    public float[] Snapshot()
    {
        lock (_lock)
        {
            return _samples.ToArray();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded < 2)
        {
            return;
        }

        var sampleCount = e.BytesRecorded / 2;
        double sumSquares = 0;

        lock (_lock)
        {
            for (var i = 0; i < sampleCount; i++)
            {
                var raw = (short)(e.Buffer[i * 2] | (e.Buffer[i * 2 + 1] << 8));
                var sample = raw / 32768f;
                _samples.Add(sample);
                sumSquares += sample * sample;
            }

            var rms = Math.Sqrt(sumSquares / sampleCount);
            var level = Math.Clamp((rms - 0.02) * 5.0, 0, 1);

            _buffersForNextBar++;
            if (_buffersForNextBar >= 2)
            {
                _buffersForNextBar = 0;
                Array.Copy(_bars, 1, _bars, 0, _bars.Length - 1);
                _bars[^1] = (float)Math.Max(level, _bars[^2] * 0.55);
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            LastError = e.Exception;
        }
    }

    public void Dispose() => Stop();
}

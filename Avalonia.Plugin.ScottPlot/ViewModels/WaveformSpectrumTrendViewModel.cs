using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ScottPlot.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SP = global::ScottPlot;

namespace Avalonia.Plugin.ScottPlot.ViewModels;

[NavigationItem("KeyWaveformSpectrumTrend")]
[Menu("NAV_WaveformSpectrumTrend", "KeyWaveformSpectrumTrend", "NAV_ScottPlot")]
[ViewMap(typeof(WaveformSpectrumTrendPage))]
public partial class WaveformSpectrumTrendViewModel : ObservableObject
{
    private readonly List<double[]> _waveformData = [];
    private double[] _trendValues = [];
    private double[] _trendTimes = [];
    private const double SampleRate = 1000.0;
    private const int PointCount = 50;
    private const int SamplesPerPoint = 512; // 2^9，适合 FFT

    [ObservableProperty] private int _selectedIndex = -1;

    public SP.Plot WaveformPlot { get; } = new();
    public SP.Plot SpectrumPlot { get; } = new();
    public SP.Plot TrendPlot { get; } = new();

    public event Action? WaveformPlotChanged;
    public event Action? SpectrumPlotChanged;
    public event Action? TrendPlotChanged;

    public WaveformSpectrumTrendViewModel()
    {
        GenerateDemoData();
        UpdateWaveformPlot(0);
        UpdateSpectrumPlot(0);
        UpdateTrendPlot();
    }

    private void GenerateDemoData()
    {
        _waveformData.Clear();
        _trendTimes = new double[PointCount];
        _trendValues = new double[PointCount];

        for (int i = 0; i < PointCount; i++)
        {
            _trendTimes[i] = i;

            double[] signal = new double[SamplesPerPoint];
            double baseFreq = 10 + i * 0.5;
            double amp = 1.0 + 0.5 * Math.Sin(i * 0.2);
            for (int j = 0; j < SamplesPerPoint; j++)
            {
                double t = j / SampleRate;
                signal[j] = amp * Math.Sin(2 * Math.PI * baseFreq * t)
                           + 0.3 * Math.Sin(2 * Math.PI * baseFreq * 2.5 * t)
                           + 0.1 * (Random.Shared.NextDouble() - 0.5);
            }

            _waveformData.Add(signal);

            double sumSq = 0;
            for (int j = 0; j < SamplesPerPoint; j++)
                sumSq += signal[j] * signal[j];
            _trendValues[i] = Math.Sqrt(sumSq / SamplesPerPoint);
        }
    }

    public void OnTrendPointClicked(int index)
    {
        if (index < 0 || index >= _waveformData.Count) return;
        SelectedIndex = index;
        UpdateWaveformPlot(index);
        UpdateSpectrumPlot(index);
    }

    private void UpdateWaveformPlot(int index)
    {
        WaveformPlot.Clear();
        if (index < 0 || index >= _waveformData.Count) return;

        var data = _waveformData[index];
        var signal = WaveformPlot.Add.Signal(data, SampleRate);
        signal.Color = SP.Colors.CornflowerBlue;
        WaveformPlot.Title($"Waveform - Point {index}");
        WaveformPlot.XLabel("Time (s)");
        WaveformPlot.YLabel("Amplitude");
        WaveformPlot.Axes.AutoScale();
        WaveformPlotChanged?.Invoke();
    }

    private void UpdateSpectrumPlot(int index)
    {
        SpectrumPlot.Clear();
        if (index < 0 || index >= _waveformData.Count) return;

        var data = _waveformData[index];
        int n = data.Length;

        double[] magnitudes = ComputeMagnitudeSpectrum(data);
        double freqResolution = SampleRate / n;
        double[] freqs = new double[n / 2];
        for (int i = 0; i < freqs.Length; i++)
            freqs[i] = i * freqResolution;

        var scatter = SpectrumPlot.Add.Scatter(freqs, magnitudes);
        scatter.Color = SP.Colors.OrangeRed;
        scatter.LineWidth = 1.5f;
        SpectrumPlot.Title($"Spectrum - Point {index}");
        SpectrumPlot.XLabel("Frequency (Hz)");
        SpectrumPlot.YLabel("Magnitude");
        SpectrumPlot.Axes.AutoScale();
        SpectrumPlotChanged?.Invoke();
    }

    private void UpdateTrendPlot()
    {
        TrendPlot.Clear();
        var scatter = TrendPlot.Add.Scatter(_trendTimes, _trendValues);
        scatter.Color = SP.Colors.Green;
        scatter.LineWidth = 1.5f;
        scatter.MarkerSize = 6;
        scatter.MarkerShape = SP.MarkerShape.FilledCircle;
        TrendPlot.Title("Trend (RMS)");
        TrendPlot.XLabel("Time Index");
        TrendPlot.YLabel("RMS Value");
        TrendPlot.Axes.AutoScale();
        TrendPlotChanged?.Invoke();
    }

    /// <summary>
    /// Cooley-Tukey FFT，O(n log n)
    /// </summary>
    private static double[] ComputeMagnitudeSpectrum(double[] input)
    {
        int n = input.Length;

        // 应用汉宁窗
        double[] windowed = new double[n];
        for (int i = 0; i < n; i++)
        {
            double w = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
            windowed[i] = input[i] * w;
        }

        // FFT 原地计算
        double[] re = new double[n];
        double[] im = new double[n];
        Array.Copy(windowed, re, n);

        Fft(re, im);

        // 计算幅值谱（前半段）
        double[] magnitudes = new double[n / 2];
        for (int k = 0; k < n / 2; k++)
        {
            magnitudes[k] = 2.0 / n * Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
        }

        return magnitudes;
    }

    /// <summary>
    /// 原地 Cooley-Tukey FFT（n 必须为 2 的幂）
    /// </summary>
    private static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        if (n <= 1) return;

        // 位反转排列
        int bits = (int)Math.Log2(n);
        for (int i = 0; i < n; i++)
        {
            int j = BitReverse(i, bits);
            if (j > i)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        // 蝶形运算
        for (int len = 2; len <= n; len *= 2)
        {
            double angle = -2 * Math.PI / len;
            double wRe = Math.Cos(angle);
            double wIm = Math.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                double curRe = 1.0, curIm = 0.0;
                for (int j = 0; j < len / 2; j++)
                {
                    int evenIdx = i + j;
                    int oddIdx = i + j + len / 2;

                    double tRe = curRe * re[oddIdx] - curIm * im[oddIdx];
                    double tIm = curRe * im[oddIdx] + curIm * re[oddIdx];

                    re[oddIdx] = re[evenIdx] - tRe;
                    im[oddIdx] = im[evenIdx] - tIm;
                    re[evenIdx] += tRe;
                    im[evenIdx] += tIm;

                    double newCurRe = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe;
                    curRe = newCurRe;
                }
            }
        }
    }

    private static int BitReverse(int x, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (x & 1);
            x >>= 1;
        }
        return result;
    }

    [RelayCommand]
    private void WaveformAutoscale()
    {
        WaveformPlot.Axes.AutoScale();
        WaveformPlotChanged?.Invoke();
    }

    [RelayCommand]
    private void SpectrumAutoscale()
    {
        SpectrumPlot.Axes.AutoScale();
        SpectrumPlotChanged?.Invoke();
    }

    [RelayCommand]
    private void TrendAutoscale()
    {
        TrendPlot.Axes.AutoScale();
        TrendPlotChanged?.Invoke();
    }

    [RelayCommand]
    private void RegenerateData()
    {
        GenerateDemoData();
        SelectedIndex = -1;
        UpdateWaveformPlot(0);
        UpdateSpectrumPlot(0);
        UpdateTrendPlot();
    }

    [RelayCommand]
    private void ResetAll()
    {
        SelectedIndex = -1;
        WaveformPlot.Clear();
        WaveformPlot.Title("Waveform");
        WaveformPlotChanged?.Invoke();

        SpectrumPlot.Clear();
        SpectrumPlot.Title("Spectrum");
        SpectrumPlotChanged?.Invoke();

        TrendPlot.Axes.AutoScale();
        TrendPlotChanged?.Invoke();
    }
}

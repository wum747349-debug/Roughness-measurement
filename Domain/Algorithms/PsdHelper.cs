using System;
using System.Numerics;

namespace ConfocalMeter.Domain
{
    /// <summary>
    /// 功率谱密度辅助类 - 用于 PSD 自检与能量保持率计算
    /// </summary>
    public static class PsdHelper
    {
        /// <summary>
        /// 计算功率谱（使用简单的 DFT，适用于短序列）
        /// </summary>
        public static void PowerSpectrum(double[] signal, double dx, out double[] freq, out double[] power)
        {
            freq = Array.Empty<double>();
            power = Array.Empty<double>();
            if (signal == null || signal.Length < 2 || dx <= 0) return;

            int n = signal.Length;
            int nfft = NextPow2(n);
            Complex[] data = new Complex[nfft];
            for (int i = 0; i < n; i++) data[i] = new Complex(signal[i], 0);
            for (int i = n; i < nfft; i++) data[i] = Complex.Zero;

            FFT(data);

            int half = nfft / 2 + 1;
            freq = new double[half];
            power = new double[half];
            double df = 1.0 / (nfft * dx);
            for (int i = 0; i < half; i++)
            {
                freq[i] = i * df;
                double mag = data[i].Magnitude;
                power[i] = mag * mag / n;
            }
        }

        /// <summary>
        /// 从波长范围转换为频率带
        /// </summary>
        public static (double fLow, double fHigh) BandFromWavelength(double lambdaMin, double lambdaMax)
        {
            double fLow = (lambdaMax > 0) ? 1.0 / lambdaMax : 0;
            double fHigh = (lambdaMin > 0) ? 1.0 / lambdaMin : double.MaxValue;
            return (fLow, fHigh);
        }

        /// <summary>
        /// 计算指定频带内的能量比例
        /// </summary>
        public static double BandEnergyRatio(double[] freq, double[] power, double fLow, double fHigh)
        {
            if (freq == null || power == null || freq.Length != power.Length) return 0;
            double total = 0, band = 0;
            for (int i = 0; i < freq.Length; i++)
            {
                total += power[i];
                if (freq[i] >= fLow && freq[i] <= fHigh) band += power[i];
            }
            return (total > 0) ? band : 0;
        }

        private static int NextPow2(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            return p;
        }

        /// <summary>
        /// Cooley-Tukey FFT (in-place, radix-2)
        /// </summary>
        private static void FFT(Complex[] data)
        {
            int n = data.Length;
            if (n <= 1) return;

            // 位反转置换
            int bits = (int)Math.Log(n, 2);
            for (int i = 0; i < n; i++)
            {
                int j = BitReverse(i, bits);
                if (j > i) { var t = data[i]; data[i] = data[j]; data[j] = t; }
            }

            // Cooley-Tukey
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2.0 * Math.PI / len;
                Complex wn = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (int i = 0; i < n; i += len)
                {
                    Complex w = Complex.One;
                    for (int j = 0; j < len / 2; j++)
                    {
                        Complex u = data[i + j];
                        Complex v = data[i + j + len / 2] * w;
                        data[i + j] = u + v;
                        data[i + j + len / 2] = u - v;
                        w *= wn;
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
    }
}

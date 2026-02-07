using System;

namespace ConfocalMeter.Domain
{
    /// <summary>
    /// 高通滤波去趋势 - 基于高斯低通实现
    /// 高通 = 原始信号 - 低通(原始信号)
    /// </summary>
    public class HighpassDetrender
    {
        /// <summary>
        /// 高斯高通滤波
        /// </summary>
        /// <param name="y">输入信号</param>
        /// <param name="dx">采样间隔 (mm)</param>
        /// <param name="lambdaC">截止波长 λc (mm)</param>
        /// <param name="lowpass">输出：低通分量（趋势）</param>
        /// <param name="highpass">输出：高通分量（粗糙度）</param>
        public void HighpassByGaussian(double[] y, double dx, double lambdaC, out double[] lowpass, out double[] highpass)
        {
            int n = y?.Length ?? 0;
            lowpass = new double[n];
            highpass = new double[n];
            if (n == 0 || dx <= 0 || lambdaC <= 0)
            {
                if (n > 0) Array.Copy(y, highpass, n);
                return;
            }

            // σ(px) = λc / (2.355 * dx)，2.355 ≈ 2*sqrt(2*ln2) 是高斯 FWHM 与 σ 的关系
            double sigmaPx = lambdaC / (2.355 * dx);
            int ksize = (int)(2 * Math.Ceiling(3 * sigmaPx) + 1);
            if ((ksize & 1) == 0) ksize++;
            ksize = Math.Max(3, Math.Min(ksize, Math.Min(101, (n | 1))));

            lowpass = GaussianFilter.Filter(y, sigmaPx, ksize, GaussianFilter.BoundaryMode.Reflect);
            for (int i = 0; i < n; i++) highpass[i] = y[i] - lowpass[i];
        }
    }
}

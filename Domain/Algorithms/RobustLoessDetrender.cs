using System;
using ConfocalMeter.Interfaces;

namespace ConfocalMeter.Domain
{
    /// <summary>
    /// 鲁棒 LOESS 去趋势器
    /// 使用局部加权回归 (tricube) + Huber 鲁棒权进行趋势分离
    /// </summary>
    public class RobustLoessDetrender : IDetrender
    {
        public const int DefaultHardCap = 8001; // 窗口点数上限

        private readonly double _trendLength;
        private readonly int _maxIter;
        private readonly double _huberC;

        /// <summary>
        /// 单次调用覆盖窗口长度（λc），用后自动清零
        /// </summary>
        public double TrendLengthOverride { get; set; } = 0;

        public RobustLoessDetrender(double trendLength = 0, int maxIter = 1, double huberC = 1.345)
        {
            _trendLength = trendLength;
            _maxIter = Math.Max(1, maxIter);
            _huberC = huberC > 0 ? huberC : 1.345;
        }

        /// <summary>
        /// 带 Hampel 预处理的去趋势
        /// </summary>
        public void DetendWithHampel(double[] y, double dx, int degree, int hampelWindow, double hampelSigma, out double[] trend, out double[] residual)
        {
            var cleanY = HampelFilter.Apply(y, hampelWindow, hampelSigma);
            Detend(cleanY, dx, degree, out trend, out residual);
        }

        /// <summary>
        /// 基于局部加权回归 (tricube) + Huber 鲁棒权
        /// </summary>
        public void Detend(double[] y, double dx, int degree, out double[] trend, out double[] residual)
        {
            trend = Array.Empty<double>();
            residual = Array.Empty<double>();
            if (y == null || y.Length == 0) return;

            int n = y.Length;
            int deg = Math.Max(1, Math.Min(2, degree));

            double[] x = new double[n];
            for (int i = 0; i < n; i++) x[i] = i * dx;

            // 窗口点数
            int window;
            double Ltrend = TrendLengthOverride > 0 ? TrendLengthOverride : _trendLength;
            if (Ltrend > 0 && dx > 0)
            {
                int pts = Math.Max(5, (int)Math.Round(Ltrend / dx));
                if ((pts & 1) == 0) pts++;
                window = Math.Min(pts, DefaultHardCap);
            }
            else
            {
                window = Math.Min(Math.Max(5, (n / 10) | 1), DefaultHardCap);
            }
            if (window > n) window = n | 1;
            if ((window & 1) == 0) window++;
            TrendLengthOverride = 0;

            int halfW = window / 2;

            // tricube 权重函数
            Func<double, double> tricube = d =>
            {
                if (d >= 1) return 0;
                double t = 1 - d * d * d;
                return t * t * t;
            };

            double[] trendArr = new double[n];
            double[] residArr = new double[n];
            double[] robustW = new double[n];
            for (int i = 0; i < n; i++) robustW[i] = 1.0;

            for (int iter = 0; iter < _maxIter; iter++)
            {
                for (int i = 0; i < n; i++)
                {
                    int left = Math.Max(0, i - halfW);
                    int right = Math.Min(n - 1, i + halfW);
                    double xi = x[i];
                    double maxDist = 0;
                    for (int j = left; j <= right; j++)
                    {
                        double d = Math.Abs(x[j] - xi);
                        if (d > maxDist) maxDist = d;
                    }
                    if (maxDist < 1e-12) maxDist = 1e-12;

                    // 加权最小二乘
                    double sw = 0, swx = 0, swy = 0, swx2 = 0, swxy = 0, swx3 = 0, swx4 = 0, swx2y = 0;
                    for (int j = left; j <= right; j++)
                    {
                        double d = Math.Abs(x[j] - xi) / maxDist;
                        double w = tricube(d) * robustW[j];
                        double xj = x[j] - xi;
                        double yj = y[j];
                        sw += w;
                        swx += w * xj;
                        swy += w * yj;
                        swx2 += w * xj * xj;
                        swxy += w * xj * yj;
                        if (deg >= 2)
                        {
                            swx3 += w * xj * xj * xj;
                            swx4 += w * xj * xj * xj * xj;
                            swx2y += w * xj * xj * yj;
                        }
                    }

                    double yi;
                    if (deg == 1)
                    {
                        double det = sw * swx2 - swx * swx;
                        if (Math.Abs(det) < 1e-12) { yi = (sw > 0) ? swy / sw : y[i]; }
                        else
                        {
                            double a0 = (swx2 * swy - swx * swxy) / det;
                            yi = a0;
                        }
                    }
                    else
                    {
                        // 二阶多项式
                        double[,] A = { { sw, swx, swx2 }, { swx, swx2, swx3 }, { swx2, swx3, swx4 } };
                        double[] B = { swy, swxy, swx2y };
                        double[] coef = SolveLinear3(A, B);
                        yi = (coef != null) ? coef[0] : ((sw > 0) ? swy / sw : y[i]);
                    }
                    trendArr[i] = yi;
                    residArr[i] = y[i] - yi;
                }

                // 更新鲁棒权重
                if (iter < _maxIter - 1)
                {
                    double[] absRes = new double[n];
                    for (int i = 0; i < n; i++) absRes[i] = Math.Abs(residArr[i]);
                    double medRes = Median(absRes);
                    double scale = medRes > 0 ? _huberC * medRes : 1e-6;
                    for (int i = 0; i < n; i++)
                    {
                        double r = Math.Abs(residArr[i]) / scale;
                        robustW[i] = (r <= 1) ? 1.0 : (1.0 / r);
                    }
                }
            }

            trend = trendArr;
            residual = residArr;
        }

        private static double Median(double[] arr)
        {
            if (arr == null || arr.Length == 0) return 0;
            double[] tmp = new double[arr.Length];
            Array.Copy(arr, tmp, arr.Length);
            Array.Sort(tmp);
            int n = tmp.Length;
            return (n & 1) == 1 ? tmp[n / 2] : 0.5 * (tmp[n / 2 - 1] + tmp[n / 2]);
        }

        private static double[] SolveLinear3(double[,] A, double[] B)
        {
            // 简单的 3x3 克拉默法则
            double det = A[0, 0] * (A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1])
                       - A[0, 1] * (A[1, 0] * A[2, 2] - A[1, 2] * A[2, 0])
                       + A[0, 2] * (A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0]);
            if (Math.Abs(det) < 1e-15) return null;

            double[] x = new double[3];
            x[0] = (B[0] * (A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1])
                  - A[0, 1] * (B[1] * A[2, 2] - A[1, 2] * B[2])
                  + A[0, 2] * (B[1] * A[2, 1] - A[1, 1] * B[2])) / det;
            x[1] = (A[0, 0] * (B[1] * A[2, 2] - A[1, 2] * B[2])
                  - B[0] * (A[1, 0] * A[2, 2] - A[1, 2] * A[2, 0])
                  + A[0, 2] * (A[1, 0] * B[2] - B[1] * A[2, 0])) / det;
            x[2] = (A[0, 0] * (A[1, 1] * B[2] - B[1] * A[2, 1])
                  - A[0, 1] * (A[1, 0] * B[2] - B[1] * A[2, 0])
                  + B[0] * (A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0])) / det;
            return x;
        }
    }
}

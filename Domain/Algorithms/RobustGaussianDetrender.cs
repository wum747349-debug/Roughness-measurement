using System;

namespace ConfocalMeter.Domain
{
    /// <summary>
    /// 稳健高斯回归去趋势器 - ISO 16610-31
    /// 以高斯权作平滑基础，配合 Huber 鲁棒损失
    /// </summary>
    public class RobustGaussianDetrender
    {
        /// <summary>
        /// 稳健高通滤波
        /// </summary>
        public void RobustHighpass(double[] x, double dx, double lambdaC, double huberC, out double[] lowpass, out double[] rough)
        {
            int n = x?.Length ?? 0;
            lowpass = new double[n];
            rough = new double[n];
            if (n < 3 || dx <= 0 || lambdaC <= 0)
            {
                if (n > 0) Array.Copy(x, rough, n);
                return;
            }

            double sigma = lambdaC / (2.355 * dx);
            int ksize = (int)(2 * Math.Ceiling(3 * sigma) + 1);
            if ((ksize & 1) == 0) ksize++;
            ksize = Math.Max(3, Math.Min(ksize, Math.Min(101, (n | 1))));
            int pad = ksize / 2;

            double[] kernel = BuildKernel(sigma, ksize);

            // 初始低通
            double[] lp = new double[n];
            for (int i = 0; i < n; i++)
            {
                double acc = 0.0, ws = 0.0;
                for (int k = -pad; k <= pad; k++)
                {
                    int j = i + k;
                    int jj = (j < 0 || j >= n) ? ReflectIndex(j, n) : j;
                    double kw = kernel[k + pad];
                    acc += x[jj] * kw;
                    ws += kw;
                }
                lp[i] = (ws > 0) ? (acc / ws) : x[i];
            }

            double[] res = new double[n];
            for (int i = 0; i < n; i++) res[i] = x[i] - lp[i];

            // 鲁棒迭代
            double[] w = new double[n];
            for (int i = 0; i < n; i++) w[i] = 1.0;

            for (int iter = 0; iter < 3; iter++)
            {
                double med = Median(res);
                double[] absRes = new double[n];
                for (int i = 0; i < n; i++) absRes[i] = Math.Abs(res[i] - med);
                double mad = Median(absRes);
                double delta = huberC * (mad > 0 ? mad : 1e-9);

                for (int i = 0; i < n; i++)
                {
                    double r = Math.Abs(res[i]);
                    w[i] = (r <= delta) ? 1.0 : (delta / r);
                }

                // 加权回归平滑
                for (int i = 0; i < n; i++)
                {
                    double acc = 0.0, ws = 0.0;
                    for (int k = -pad; k <= pad; k++)
                    {
                        int j = i + k;
                        int jj = (j < 0 || j >= n) ? ReflectIndex(j, n) : j;
                        double kw = kernel[k + pad] * w[jj];
                        acc += x[jj] * kw;
                        ws += kw;
                    }
                    lp[i] = (ws > 0) ? (acc / ws) : x[i];
                }
                for (int i = 0; i < n; i++) res[i] = x[i] - lp[i];
            }

            lowpass = lp;
            rough = new double[n];
            for (int i = 0; i < n; i++) rough[i] = x[i] - lp[i];
        }

        private static int ReflectIndex(int j, int n)
        {
            if (n <= 1) return 0;
            while (j < 0 || j >= n)
            {
                if (j < 0) j = -j - 1;
                else j = 2 * n - j - 1;
            }
            return j;
        }

        private static double[] BuildKernel(double sigma, int size)
        {
            double[] k = new double[size];
            int half = size / 2;
            double sum = 0.0, inv2s2 = 1.0 / (2.0 * sigma * sigma);
            for (int i = -half; i <= half; i++)
            {
                double val = Math.Exp(-(i * i) * inv2s2);
                k[i + half] = val;
                sum += val;
            }
            if (sum > 0) for (int i = 0; i < size; i++) k[i] /= sum;
            return k;
        }

        private static double Median(double[] a)
        {
            int n = a.Length;
            double[] t = new double[n];
            Array.Copy(a, t, n);
            Array.Sort(t);
            if ((n & 1) == 1) return t[n / 2];
            return 0.5 * (t[n / 2 - 1] + t[n / 2]);
        }
    }
}

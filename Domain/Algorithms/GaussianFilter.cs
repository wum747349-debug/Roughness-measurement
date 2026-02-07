using System;

namespace ConfocalMeter.Domain
{
    /// <summary>
    /// 高斯滤波器 - ISO 16610-21 线性高斯 L-filter
    /// </summary>
    public static class GaussianFilter
    {
        public enum BoundaryMode { Zero, Reflect, Replicate }

        /// <summary>
        /// 一维高斯滤波
        /// </summary>
        /// <param name="data">输入序列</param>
        /// <param name="sigma">高斯核标准差（像素单位）</param>
        /// <param name="kernelSize">核大小（奇数）</param>
        /// <param name="boundaryMode">边界处理模式</param>
        public static double[] Filter(double[] data, double sigma, int kernelSize, BoundaryMode boundaryMode = BoundaryMode.Reflect)
        {
            if (data == null || data.Length == 0) return data ?? new double[0];
            int n = data.Length;

            if (kernelSize < 3) kernelSize = 3;
            if ((kernelSize & 1) == 0) kernelSize++;
            kernelSize = Math.Min(kernelSize, Math.Min(101, (n | 1)));

            double[] kernel = BuildKernel(sigma, kernelSize);
            int pad = kernelSize / 2;

            double[] result = new double[n];
            for (int i = 0; i < n; i++)
            {
                double acc = 0.0;
                for (int k = -pad; k <= pad; k++)
                {
                    int j = i + k;
                    double v;
                    if (j < 0 || j >= n)
                    {
                        switch (boundaryMode)
                        {
                            case BoundaryMode.Reflect: v = data[ReflectIndex(j, n)]; break;
                            case BoundaryMode.Replicate: v = data[j < 0 ? 0 : n - 1]; break;
                            default: v = 0; break;
                        }
                    }
                    else v = data[j];
                    acc += v * kernel[k + pad];
                }
                result[i] = acc;
            }
            return result;
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
            double sum = 0.0;
            double inv2s2 = (sigma > 0) ? (1.0 / (2.0 * sigma * sigma)) : 0;
            for (int i = -half; i <= half; i++)
            {
                double val = (sigma > 0) ? Math.Exp(-(i * i) * inv2s2) : (i == 0 ? 1.0 : 0.0);
                k[i + half] = val;
                sum += val;
            }
            if (sum > 0) for (int i = 0; i < size; i++) k[i] /= sum;
            return k;
        }

        /// <summary>
        /// 根据采样间隔和 λc 计算安全的 sigma 和核大小（轻度去噪）
        /// </summary>
        public static (double sigma, int kernelSize) SafeNoiseSigma(int n, double sampleSpacing, double requestedSigma, double? lambdaC)
        {
            if (n < 3 || sampleSpacing <= 0) return (0.0, 1);
            double sigma = requestedSigma;

            // "温和清理"的上限：不超过 2 像素
            double maxSigma = 2.0;
            if (lambdaC.HasValue && sampleSpacing > 0)
            {
                double lambdaCpx = lambdaC.Value / sampleSpacing;
                maxSigma = Math.Min(maxSigma, 0.2 * lambdaCpx);
                if (maxSigma < 0.6) maxSigma = 0.6;
            }
            sigma = Math.Max(0.0, Math.Min(sigma, maxSigma));

            if (sigma < 0.6) return (0.0, 1);

            int kernelSize = (int)(2 * Math.Ceiling(3 * sigma) + 1);
            if ((kernelSize & 1) == 0) kernelSize++;
            kernelSize = Math.Max(3, kernelSize);
            kernelSize = Math.Min(kernelSize, Math.Min(101, (n | 1)));

            return (sigma, kernelSize);
        }
    }
}

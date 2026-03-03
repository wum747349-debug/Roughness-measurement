using System;

namespace ConfocalMeter.Domain
{
    /// <summary>
    /// Hampel 滤波器 - 异常值抑制
    /// 使用滑动窗口中位数和 MAD 检测并替换异常点
    /// </summary>
    public static class HampelFilter
    {
        /// <summary>
        /// 应用 Hampel 滤波
        /// </summary>
        /// <param name="data">输入序列</param>
        /// <param name="windowSize">窗口大小，建议 5、7、9、11 等奇数</param>
        /// <param name="nSigma">阈值系数，通常 3.0 左右</param>
        public static double[] Apply(double[] data, int windowSize = 7, double nSigma = 3.0)
        {
            if (data == null) return null;
            int n = data.Length;
            if (n == 0) return new double[0];

            if (windowSize < 3) windowSize = 3;
            if ((windowSize & 1) == 0) windowSize++; // 保障为奇数
            int half = windowSize / 2;

            double[] result = new double[n];
            Array.Copy(data, result, n);

            double[] window = new double[windowSize];

            for (int i = 0; i < n; i++)
            {
                int left = Math.Max(0, i - half);
                int right = Math.Min(n - 1, i + half);
                int len = right - left + 1;

                for (int k = 0; k < len; k++) window[k] = data[left + k];

                double med = Median(window, len);

                // 计算 abs deviation 相对于中位数的 MAD
                double[] absDev = new double[len];
                for (int k = 0; k < len; k++) absDev[k] = Math.Abs(window[k] - med);
                double mad = Median(absDev, len);
                if (mad < 1e-12) mad = 1e-12; // 防止除以零

                // 如果当前点为异常点，替换为局部中位数
                if (Math.Abs(data[i] - med) > nSigma * mad)
                {
                    result[i] = med;
                }
            }

            return result;
        }

        /// <summary>
        /// 取中位数，len 指定实际长度
        /// </summary>
        private static double Median(double[] arr, int len)
        {
            if (len <= 0) return 0;
            double[] tmp = new double[len];
            Array.Copy(arr, tmp, len);
            Array.Sort(tmp);
            if ((len & 1) == 1) return tmp[len / 2];
            return 0.5 * (tmp[len / 2 - 1] + tmp[len / 2]);
        }
    }
}

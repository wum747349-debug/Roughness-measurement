using System;
using ConfocalMeter.Interfaces;

namespace ConfocalMeter.Domain
{
    /// <summary>
    /// 粗糙度参数计算器
    /// 计算 Ra/Rz（五段平均法）及调用 AF 曲线计算
    /// </summary>
    public class RoughnessCalculator : IRoughnessCalculator
    {
        private readonly IAbbottFirestoneCalculator _afCalc;

        public RoughnessCalculator(IAbbottFirestoneCalculator af)
        {
            _afCalc = af;
        }

        /// <summary>
        /// 计算 Rz 五段平均法（ISO 21920-2）
        /// </summary>
        public double ComputeRzFiveSectionMean(double[] rough, double dx, double? le)
        {
            if (rough == null || rough.Length < 5) return 0;
            int n = rough.Length;

            int start = 0;
            int nEval = n;
            if (le.HasValue && dx > 0)
            {
                nEval = Math.Min(n, (int)(le.Value / dx));
                start = (n - nEval) / 2;
            }
            start = Math.Max(0, start);
            nEval = Math.Min(nEval, n - start);
            if (nEval < 5) { start = 0; nEval = n; }

            int sections = 5;
            int segLen = Math.Max(1, nEval / sections);
            double sum = 0.0;
            int used = 0;

            for (int s = 0; s < sections; s++)
            {
                int segStart = start + s * segLen;
                int segEnd = (s == sections - 1) ? (start + nEval) : (segStart + segLen);
                segEnd = Math.Min(segEnd, n);
                if (segEnd - segStart <= 0) continue;

                double segMin = double.PositiveInfinity, segMax = double.NegativeInfinity;
                for (int i = segStart; i < segEnd; i++)
                {
                    double v = rough[i];
                    if (v < segMin) segMin = v;
                    if (v > segMax) segMax = v;
                }
                sum += (segMax - segMin);
                used++;
            }
            return (used > 0) ? (sum / used) : 0.0;
        }

        /// <summary>
        /// 计算 Ra, Rz, RMS, MAD
        /// </summary>
        public void ComputeRaRz(double[] residual, double scale, out double ra, out double rz, out double rms, out double mad)
        {
            int n = residual?.Length ?? 0;
            ra = rz = rms = mad = 0.0;
            if (n == 0) return;

            // Welford 在线算法计算均值和方差
            double sumAbs = 0.0, mean = 0.0, m2 = 0.0;
            for (int i = 0; i < n; i++)
            {
                double v = residual[i];
                sumAbs += Math.Abs(v);
                double delta = v - mean;
                mean += delta / (i + 1);
                m2 += delta * (v - mean);
            }
            ra = sumAbs / n;
            rms = Math.Sqrt(m2 / n);

            // MAD
            double[] copy = new double[n];
            Array.Copy(residual, copy, n);
            double med = MedianOf(copy);
            for (int i = 0; i < n; i++) copy[i] = Math.Abs(residual[i] - med);
            mad = MedianOf(copy);

            // Rz: 五段平均
            rz = ComputeRzFiveSectionMean(residual, 1.0, null);

            ra *= scale;
            rz *= scale;
            rms *= scale;
            mad *= scale;
        }

        public void ComputeAbbottFirestone(double[] rough, double dx,
            out double mr1, out double mr2, out double rpk, out double rvk, out double rk,
            out double[] AbbottTp, out double[] AbbottHeight,
            out double coreA, out double coreB, out double coreStart, out double coreEnd)
        {
            _afCalc.ComputeAbbottFirestoneCurve(rough, dx,
                out mr1, out mr2, out rpk, out rvk, out rk,
                out AbbottTp, out AbbottHeight,
                out coreA, out coreB, out coreStart, out coreEnd);
        }

        public void ComputeAbbottFirestone(double[] rough, double dx,
            out double mr1, out double mr2, out double rpk, out double rvk, out double rk,
            out double[] AbbottTp, out double[] AbbottHeight)
        {
            _afCalc.ComputeAbbottFirestoneCurve(rough, dx,
                out mr1, out mr2, out rpk, out rvk, out rk,
                out AbbottTp, out AbbottHeight);
        }

        private static double MedianOf(double[] arr)
        {
            if (arr == null || arr.Length == 0) return 0;
            Array.Sort(arr);
            int n = arr.Length;
            return (n & 1) == 1 ? arr[n / 2] : 0.5 * (arr[n / 2 - 1] + arr[n / 2]);
        }
    }
}

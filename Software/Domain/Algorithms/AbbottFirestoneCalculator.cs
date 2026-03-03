using System;
using ConfocalMeter.Interfaces;

namespace ConfocalMeter.Domain
{
    /// <summary>
    /// Abbott-Firestone 曲线计算器
    /// 实现 ISO 13565-2/21920-2 的核心线拟合算法
    /// </summary>
    public class AbbottFirestoneCalculator : IAbbottFirestoneCalculator
    {
        public void ComputeAbbottFirestoneCurve(
            double[] rough, double dx,
            out double mr1, out double mr2,
            out double rpk, out double rvk, out double rk,
            out double[] AbbottTp, out double[] AbbottHeight)
        {
            ComputeAbbottFirestoneCurve(rough, dx,
                out mr1, out mr2, out rpk, out rvk, out rk,
                out AbbottTp, out AbbottHeight,
                out _, out _, out _, out _);
        }

        public void ComputeAbbottFirestoneCurve(
            double[] rough, double dx,
            out double mr1, out double mr2,
            out double rpk, out double rvk, out double rk,
            out double[] AbbottTp, out double[] AbbottHeight,
            out double coreA, out double coreB, out double coreStart, out double coreEnd)
        {
            mr1 = mr2 = rpk = rvk = rk = 0.0;
            coreA = coreB = coreStart = coreEnd = 0.0;
            AbbottTp = Array.Empty<double>();
            AbbottHeight = Array.Empty<double>();
            if (rough == null || rough.Length < 10) return;

            // 构造 AF 曲线（tp 0..100%）
            int n = rough.Length;
            double[] sorted = new double[n];
            Array.Copy(rough, sorted, n);
            Array.Sort(sorted);

            int M = 1001;
            double[] tp = new double[M];
            double[] H = new double[M];
            for (int i = 0; i < M; i++)
            {
                tp[i] = i * (100.0 / (M - 1));
                H[i] = HeightAtPercentile(sorted, 100.0 - tp[i]);
            }
            AbbottTp = tp;
            AbbottHeight = H;

            // 核心线性段拟合（滑窗 40%）
            int window = Math.Max(31, (int)(0.4 * M));
            if ((window & 1) == 0) window++;
            int halfW = window / 2;

            double bestSlope = 0, bestIntercept = 0;
            double minResidual = double.MaxValue;
            int bestStart = 0, bestEnd = 0;

            // 在 [5%, 95%] 范围内滑窗
            int iMin = (int)(0.05 * M);
            int iMax = (int)(0.95 * M) - window;
            for (int i = iMin; i <= iMax; i++)
            {
                // 线性拟合 H = a * tp + b
                double sx = 0, sy = 0, sxy = 0, sxx = 0;
                int cnt = 0;
                for (int j = i; j < i + window && j < M; j++)
                {
                    sx += tp[j];
                    sy += H[j];
                    sxy += tp[j] * H[j];
                    sxx += tp[j] * tp[j];
                    cnt++;
                }
                double det = cnt * sxx - sx * sx;
                if (Math.Abs(det) < 1e-12) continue;
                double a = (cnt * sxy - sx * sy) / det;
                double b = (sxx * sy - sx * sxy) / det;

                // 残差
                double res = 0;
                for (int j = i; j < i + window && j < M; j++)
                {
                    double diff = H[j] - (a * tp[j] + b);
                    res += diff * diff;
                }

                if (res < minResidual)
                {
                    minResidual = res;
                    bestSlope = a;
                    bestIntercept = b;
                    bestStart = i;
                    bestEnd = i + window - 1;
                }
            }

            coreA = bestSlope;
            coreB = bestIntercept;
            coreStart = tp[bestStart];
            coreEnd = tp[bestEnd];

            // 求核心线与 AF 曲线的交点
            double hMax = H[0];
            double hMin = H[M - 1];

            // Mr1: 核心线在 H=hMax 附近的 tp 交点
            // Mr2: 核心线在 H=hMin 附近的 tp 交点
            // 简化：找核心线与曲线的近似交点
            mr1 = FindIntersection(tp, H, coreA, coreB, 0, bestStart);
            mr2 = FindIntersection(tp, H, coreA, coreB, bestEnd, M - 1);
            if (mr1 < 0) mr1 = coreStart;
            if (mr2 < 0) mr2 = coreEnd;

            // Rk = 核心线在 Mr1 和 Mr2 处的高度差
            double hAtMr1 = coreA * mr1 + coreB;
            double hAtMr2 = coreA * mr2 + coreB;
            rk = Math.Abs(hAtMr1 - hAtMr2);

            // Rpk, Rvk: 峰谷面积归一化
            // A1 = ∫[0, Mr1] (H(tp) - hcore(tp)) dtp
            // Rpk = A1 / Mr1
            double A1 = 0, A2 = 0;
            for (int i = 0; i < M; i++)
            {
                double tpVal = tp[i];
                double hCore = coreA * tpVal + coreB;
                double dtp = 100.0 / (M - 1);
                if (tpVal <= mr1)
                {
                    double excess = H[i] - hCore;
                    if (excess > 0) A1 += excess * dtp;
                }
                else if (tpVal >= mr2)
                {
                    double excess = hCore - H[i];
                    if (excess > 0) A2 += excess * dtp;
                }
            }
            rpk = (mr1 > 0) ? (A1 / mr1) : 0;
            rvk = (100 - mr2 > 0) ? (A2 / (100 - mr2)) : 0;
        }

        private static double HeightAtPercentile(double[] sorted, double pct)
        {
            if (sorted == null || sorted.Length == 0) return 0;
            int n = sorted.Length;
            double idx = pct / 100.0 * (n - 1);
            int lo = (int)Math.Floor(idx);
            int hi = (int)Math.Ceiling(idx);
            lo = Math.Max(0, Math.Min(n - 1, lo));
            hi = Math.Max(0, Math.Min(n - 1, hi));
            if (lo == hi) return sorted[lo];
            double frac = idx - lo;
            return sorted[lo] * (1 - frac) + sorted[hi] * frac;
        }

        private static double FindIntersection(double[] tp, double[] H, double a, double b, int start, int end)
        {
            // 找核心线 y=a*x+b 与 AF 曲线的交点
            for (int i = start; i < end; i++)
            {
                double h1 = H[i] - (a * tp[i] + b);
                double h2 = H[i + 1] - (a * tp[i + 1] + b);
                if (h1 * h2 <= 0 && Math.Abs(h1 - h2) > 1e-12)
                {
                    // 线性插值
                    return tp[i] + (tp[i + 1] - tp[i]) * Math.Abs(h1) / Math.Abs(h1 - h2);
                }
            }
            return -1;
        }
    }
}

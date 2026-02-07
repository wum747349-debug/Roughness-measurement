using System;
using ConfocalMeter.Domain;
using ConfocalMeter.Interfaces;
using ConfocalMeter.Models;

namespace ConfocalMeter.Application
{
    /// <summary>
    /// 粗糙度算法应用服务 - 完整版
    /// 包含 Hampel 预处理、LOESS 去趋势、三种分离器、PSD 自检与 λc 自适应放宽
    /// </summary>
    public class DetrendAppService
    {
        private readonly IDetrender _detrender;
        private readonly IRoughnessCalculator _roughCalc;

        // 日志回调，用于输出滤波过程提示
        public Action<string> LogCallback { get; set; }

        public DetrendAppService(IDetrender detrender, IRoughnessCalculator roughCalc)
        {
            _detrender = detrender;
            _roughCalc = roughCalc;
        }

        private void Log(string msg)
        {
            LogCallback?.Invoke(msg);
        }

        /// <summary>
        /// 从内存数据计算粗糙度（y 为高度 mm，等间距 dx mm）
        /// </summary>
        public DetrendResult ComputeAllFromMemory(double[] y, double dx, DetrendOptions opts, double scale)
        {
            if (y == null || y.Length == 0 || dx <= 0) return null;

            double[] y0 = y;
            int n = y0.Length;
            double L = (n - 1) * dx;

            Log($"开始计算: 点数={n}, dx={dx:F6}mm, 记录长度L={L:F3}mm");

            // 1. Hampel 异常值抑制
            int hampelWindow = Math.Max(3, opts?.HampelWindow ?? 7);
            double hampelSigma = opts?.HampelSigma ?? 3.0;
            double[] yClean = HampelFilter.Apply(y0, hampelWindow, hampelSigma);
            Log($"Hampel滤波: 窗口={hampelWindow}, σ={hampelSigma}");

            // 2. λc 护栏
            double lambdaC = Math.Max(0, opts?.LambdaC ?? 0.8);
            bool limitLambda = opts?.LimitLambdaC ?? true;
            double lambdaMin = Math.Max(9 * dx, L / 200.0);
            double lambdaMax;

            if (limitLambda)
            {
                if (lambdaC > 0 && lambdaC < lambdaMin)
                {
                    Log($"λc护栏: {lambdaC:F3}mm < 下限{lambdaMin:F3}mm, 调整为下限");
                    lambdaC = lambdaMin;
                }
                lambdaMax = 0.7 * L;
                if (lambdaC > 0 && lambdaC > lambdaMax)
                {
                    Log($"λc护栏: {lambdaC:F3}mm > 上限{lambdaMax:F3}mm, 调整为上限");
                    lambdaC = lambdaMax;
                }
            }
            else
            {
                lambdaMax = Math.Max(lambdaC * 2.0, Math.Max(L, 10 * lambdaMin));
            }
            bool nearRecord = lambdaC >= 0.5 * L;
            if (nearRecord) Log("警告: λc接近记录长度，分离可能不敏感");

            // 3. LOESS 去趋势
            double[] trend = new double[n];
            double[] detrended = new double[n];
            int loessWindowPtsUsed = 0;
            bool loessClipped = false;

            if (opts?.UseRobustLoess ?? true)
            {
                if (_detrender is RobustLoessDetrender loess)
                {
                    double weakFactor = (opts?.WeakLoess ?? true) ? 0.7 : 1.0;
                    loess.TrendLengthOverride = weakFactor * lambdaC;
                    int degree = Math.Max(1, Math.Min(2, opts?.Degree ?? 1));
                    loess.Detend(yClean, dx, degree, out trend, out detrended);

                    int windowPts = (int)Math.Round(loess.TrendLengthOverride / dx);
                    int hardCap = Math.Min(n - 1, RobustLoessDetrender.DefaultHardCap);
                    if (windowPts > hardCap) { windowPts = hardCap; loessClipped = true; }
                    if ((windowPts & 1) == 0) windowPts++;
                    loessWindowPtsUsed = Math.Max(5, windowPts);

                    Log($"LOESS去趋势: 阶数={degree}, 窗口={loessWindowPtsUsed}点, 弱化={weakFactor:F1}×λc" +
                        (loessClipped ? " (触硬上限)" : ""));
                }
                else
                {
                    Array.Clear(trend, 0, n);
                    Array.Copy(yClean, detrended, n);
                    Log("LOESS去趋势: 未启用（detrender类型不匹配）");
                }
            }
            else
            {
                Array.Clear(trend, 0, n);
                Array.Copy(yClean, detrended, n);
                Log("LOESS去趋势: 已禁用");
            }

            // 4. 带宽分离
            double[] lowpass = Array.Empty<double>();
            double[] rough = Array.Empty<double>();
            var hp = new HighpassDetrender();
            var mode = opts?.Mode ?? RoughnessMode.HighpassByLambdaC;

            if (mode == RoughnessMode.HighpassByLambdaC)
            {
                Log($"分离器: 严格高通 (ISO 16610-21), λc={lambdaC:F3}mm");
                hp.HighpassByGaussian(detrended, dx, lambdaC, out lowpass, out rough);
            }
            else if (mode == RoughnessMode.DenoisedResidual)
            {
                Log("分离器: 轻度去噪（不按λc切低频）");
                double sigmaPx = 0.0;
                if (dx > 0 && lambdaC > 0)
                {
                    double lambdaCpx = lambdaC / dx;
                    sigmaPx = Math.Min(2.0, 0.2 * lambdaCpx);
                }
                if (sigmaPx >= 0.6)
                {
                    int ksize = (int)(2 * Math.Ceiling(3 * sigmaPx) + 1);
                    if ((ksize & 1) == 0) ksize++;
                    rough = GaussianFilter.Filter(detrended, sigmaPx, ksize, GaussianFilter.BoundaryMode.Reflect);
                    Log($"轻度去噪: σ={sigmaPx:F2}px, 核大小={ksize}");
                }
                else
                {
                    rough = (double[])detrended.Clone();
                    Log("轻度去噪: σ过小，跳过滤波");
                }
                lowpass = new double[n];
            }
            else
            {
                Log($"分离器: 稳健高斯回归 (ISO 16610-31), λc={lambdaC:F3}mm");
                var rg = new RobustGaussianDetrender();
                rg.RobustHighpass(detrended, dx, lambdaC, opts?.HuberC ?? 1.345, out lowpass, out rough);
            }

            // 5. PSD 自检
            double dropPct = 0.0;
            double lambdaMinSafe = Math.Max(3 * dx, 3 * dx);

            if (mode == RoughnessMode.HighpassByLambdaC)
            {
                if (lambdaC <= lambdaMinSafe + 1e-12)
                {
                    lambdaC = Math.Max(lambdaMinSafe * 1.2, lambdaMin);
                    hp.HighpassByGaussian(detrended, dx, lambdaC, out lowpass, out rough);
                }

                PsdHelper.PowerSpectrum(yClean, dx, out var f0, out var P0);
                PsdHelper.PowerSpectrum(rough, dx, out var f1, out var P1);
                var band = PsdHelper.BandFromWavelength(lambdaMinSafe, lambdaC);
                double r0 = PsdHelper.BandEnergyRatio(f0, P0, band.Item1, band.Item2);
                double r1 = PsdHelper.BandEnergyRatio(f1, P1, band.Item1, band.Item2);
                dropPct = (r0 > 0) ? 100.0 * Math.Max(0, (r0 - r1) / r0) : 0.0;

                double limitPct = Math.Max(10.0, opts?.SelfCheckDropPct ?? 50.0);

                if (dropPct > limitPct && (opts?.AutoRelaxLambda ?? true))
                {
                    Log($"PSD自检: drop={dropPct:F1}% > 阈值{limitPct}%, 开始二分放宽λc");
                    double originalLambda = lambdaC;
                    double lo = lambdaC;
                    double hi = Math.Min(lambdaMax, Math.Max(lambdaC * 2.0, lambdaC + dx));

                    for (int t = 0; t < 10; t++)
                    {
                        double mid = 0.5 * (lo + hi);
                        hp.HighpassByGaussian(detrended, dx, mid, out var lp2, out var rough2);
                        PsdHelper.PowerSpectrum(rough2, dx, out var f2, out var P2);
                        var band2 = PsdHelper.BandFromWavelength(lambdaMinSafe, mid);
                        double rr0 = PsdHelper.BandEnergyRatio(f0, P0, band2.Item1, band2.Item2);
                        double rr1 = PsdHelper.BandEnergyRatio(f2, P2, band2.Item1, band2.Item2);
                        double d2 = (rr0 > 0) ? 100.0 * Math.Max(0, (rr0 - rr1) / rr0) : 0.0;
                        if (d2 > limitPct) lo = mid; else hi = mid;
                    }
                    lambdaC = hi;
                    hp.HighpassByGaussian(detrended, dx, lambdaC, out lowpass, out rough);

                    PsdHelper.PowerSpectrum(rough, dx, out var f3, out var P3);
                    var band3 = PsdHelper.BandFromWavelength(lambdaMinSafe, lambdaC);
                    double r0b = PsdHelper.BandEnergyRatio(f0, P0, band3.Item1, band3.Item2);
                    double r1b = PsdHelper.BandEnergyRatio(f3, P3, band3.Item1, band3.Item2);
                    dropPct = (r0b > 0) ? 100.0 * Math.Max(0, (r0b - r1b) / r0b) : 0.0;

                    Log($"λc放宽: {originalLambda:F3}mm → {lambdaC:F3}mm, 最终drop={dropPct:F1}%");
                }
                else
                {
                    Log($"PSD自检: drop={dropPct:F1}%, 无需放宽");
                }
            }
            else
            {
                PsdHelper.PowerSpectrum(yClean, dx, out var f0, out var P0);
                PsdHelper.PowerSpectrum(rough, dx, out var f1, out var P1);
                var band = PsdHelper.BandFromWavelength(lambdaMinSafe, lambdaC);
                double r0 = PsdHelper.BandEnergyRatio(f0, P0, band.Item1, band.Item2);
                double r1 = PsdHelper.BandEnergyRatio(f1, P1, band.Item1, band.Item2);
                dropPct = (r0 > 0) ? 100.0 * Math.Max(0, (r0 - r1) / r0) : 0.0;
                Log($"PSD能量变化: {dropPct:F1}%");
            }

            // 6. 参数计算
            double[] AbbottTp, AbbottHeight;
            double mr1, mr2, rpk, rvk, rk;
            _roughCalc.ComputeAbbottFirestone(rough, dx,
                out mr1, out mr2, out rpk, out rvk, out rk,
                out AbbottTp, out AbbottHeight,
                out double coreA, out double coreB, out double coreStart, out double coreEnd);

            // 补丁: 手动进行单位换算 (mm -> um)
            rpk *= scale;
            rvk *= scale;
            rk *= scale;

            double ra, rz, rms, mad;
            _roughCalc.ComputeRaRz(rough, scale, out ra, out rz, out rms, out mad);

            Log($"计算完成: Ra={ra:F4}μm, Rz={rz:F4}μm");

            return new DetrendResult
            {
                Height = y0,
                Trend = trend,
                Residual = detrended,
                Roughness = rough,
                RMSResidual = rms,
                MADResidual = mad,
                Ra = ra,
                Rz = rz,
                Mr1 = mr1,
                Mr2 = mr2,
                Rpk = rpk,
                Rvk = rvk,
                Rk = rk,
                AbbottTp = AbbottTp,
                AbbottHeight = AbbottHeight,
                Scale = scale,
                UsedLambdaC = lambdaC,
                LoessWindowPts = (opts?.UseRobustLoess ?? true) ? loessWindowPtsUsed : 0,
                ClippedByHardCap = loessClipped,
                NearRecordLength = nearRecord,
                SelfCheckDropPct = dropPct,
                CoreA = coreA,
                CoreB = coreB,
                CoreStart = coreStart,
                CoreEnd = coreEnd
            };
        }
    }
}
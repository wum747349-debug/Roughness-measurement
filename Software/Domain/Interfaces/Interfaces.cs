using System;
using ConfocalMeter.Models; // 稍后定义

namespace ConfocalMeter.Interfaces
{
    // 1. AF 曲线计算接口
    public interface IAbbottFirestoneCalculator
    {
        void ComputeAbbottFirestoneCurve(
            double[] rough, double dx,
            out double mr1, out double mr2,
            out double rpk, out double rvk, out double rk,
            out double[] AbbottTp, out double[] AbbottHeight,
            out double coreA, out double coreB, out double coreStart, out double coreEnd);

        // 兼容旧调用
        void ComputeAbbottFirestoneCurve(
            double[] rough, double dx,
            out double mr1, out double mr2,
            out double rpk, out double rvk, out double rk,
            out double[] AbbottTp, out double[] AbbottHeight);
    }

    // 2. 去趋势接口
    public interface IDetrender
    {
        void Detend(double[] y, double dx, int degree, out double[] trend, out double[] residual);
        void DetendWithHampel(double[] y, double dx, int degree, int hampelWindow, double hampelSigma, out double[] trend, out double[] residual);
    }

    // 3. 粗糙度计算接口
    public interface IRoughnessCalculator
    {
        void ComputeAbbottFirestone(double[] rough, double dx,
            out double mr1, out double mr2,
            out double rpk, out double rvk, out double rk,
            out double[] AbbottTp, out double[] AbbottHeight);

        void ComputeAbbottFirestone(double[] rough, double dx,
            out double mr1, out double mr2,
            out double rpk, out double rvk, out double rk,
            out double[] AbbottTp, out double[] AbbottHeight,
            out double coreA, out double coreB, out double coreStart, out double coreEnd);

        void ComputeRaRz(double[] residual, double scale, out double ra, out double rz, out double rms, out double mad);
    }

    // 4. 数据加载接口 (虽然这次不用，但保留以防报错)
    public interface IContourLoader
    {
        ContourData Load(string path);
    }
}
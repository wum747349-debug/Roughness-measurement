using System;

namespace ConfocalMeter.Models
{
    // 1. 基础数据类
    public class ContourData
    {
        public double DxMm { get; set; }
        public double[] YMm { get; set; }
    }

    // 2. 滤波模式枚举
    public enum RoughnessMode
    {
        HighpassByLambdaC,    // 线性高斯 (ISO 16610-21)
        DenoisedResidual,     // 轻度去噪
        RobustGaussian        // 稳健高斯 (ISO 16610-31)
    }

    // 3. 推荐预设枚举
    public enum RecommendProfile
    {
        Standard,       // 标准模式
        ShortRecord,    // 短记录模式
        RobustSpecial   // 稳健模式
    }

    // 4. 算法参数设置类
    public class DetrendOptions
    {
        public double LambdaC { get; set; } = 0.8;
        public int Degree { get; set; } = 1;
        public int MaxIter { get; set; } = 1;
        public double HuberC { get; set; } = 1.345;
        public int HampelWindow { get; set; } = 7;
        public double HampelSigma { get; set; } = 3.0;
        public bool UseRobustLoess { get; set; } = true;
        public bool WeakLoess { get; set; } = true;
        public bool UseRobustGaussian { get; set; } = false;
        public RecommendProfile Recommend { get; set; } = RecommendProfile.Standard;
        public double SelfCheckDropPct { get; set; } = 50.0;
        public bool AutoRelaxLambda { get; set; } = true;
        public bool LimitLambdaC { get; set; } = true;
        public RoughnessMode Mode { get; set; } = RoughnessMode.HighpassByLambdaC;
    }

    // 5. 计算结果类
    public class DetrendResult
    {
        public double[] Height { get; set; }        // 原始轮廓
        public double[] Trend { get; set; }         // 趋势线
        public double[] Residual { get; set; }      // 去趋势后轮廓
        public double[] Roughness { get; set; }     // 最终粗糙度轮廓

        public double RMSResidual { get; set; }
        public double MADResidual { get; set; }

        public double Ra { get; set; }
        public double Rz { get; set; }

        // Abbott-Firestone 参数
        public double Mr1 { get; set; }
        public double Mr2 { get; set; }
        public double Rpk { get; set; }
        public double Rvk { get; set; }
        public double Rk { get; set; }

        // AF 曲线绘图数据
        public double[] AbbottTp { get; set; }
        public double[] AbbottHeight { get; set; }

        // 过程参数记录
        public double Scale { get; set; }
        public double UsedLambdaC { get; set; }
        public int LoessWindowPts { get; set; }
        public bool ClippedByHardCap { get; set; }
        public bool NearRecordLength { get; set; }
        public double SelfCheckDropPct { get; set; }

        // AF 核心线参数
        public double CoreA { get; set; }
        public double CoreB { get; set; }
        public double CoreStart { get; set; }
        public double CoreEnd { get; set; }
        public double EvaluationLengthUsed { get; set; }
    }
}
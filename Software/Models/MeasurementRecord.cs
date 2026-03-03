using System;

namespace ConfocalMeter.Models
{
    /// <summary>
    /// 粗糙度测量记录模型 - 用于数据库存储
    /// </summary>
    public class MeasurementRecord
    {
        /// <summary>
        /// 数据库主键 (自增)
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 测量时间
        /// </summary>
        public DateTime MeasurementTime { get; set; }

        /// <summary>
        /// 评定长度 (mm)
        /// </summary>
        public double EvalLength { get; set; }

        /// <summary>
        /// 采样间隔 (mm)
        /// </summary>
        public double Interval { get; set; }

        /// <summary>
        /// 扫描速度 (mm/min)
        /// </summary>
        public double Speed { get; set; }

        /// <summary>
        /// 算术平均偏差 Ra (μm)
        /// </summary>
        public double Ra { get; set; }

        /// <summary>
        /// 最大高度 Rz (μm)
        /// </summary>
        public double Rz { get; set; }

        /// <summary>
        /// 材料率参数 Mr1 (%)
        /// </summary>
        public double Mr1 { get; set; }

        /// <summary>
        /// 材料率参数 Mr2 (%)
        /// </summary>
        public double Mr2 { get; set; }

        /// <summary>
        /// 核心粗糙度 Rpk (μm)
        /// </summary>
        public double Rpk { get; set; }

        /// <summary>
        /// 核心粗糙度 Rvk (μm)
        /// </summary>
        public double Rvk { get; set; }

        /// <summary>
        /// 核心粗糙度 Rk (μm)
        /// </summary>
        public double Rk { get; set; }

        /// <summary>
        /// 原始高度数据 (序列化为 BLOB 存储)
        /// </summary>
        public double[] RawData { get; set; }
    }

    /// <summary>
    /// 测量记录 DTO - 轻量级版本，用于列表显示 (不含 RawData)
    /// </summary>
    public class MeasurementRecordDTO
    {
        public long Id { get; set; }
        public DateTime MeasurementTime { get; set; }
        public double EvalLength { get; set; }
        public double Interval { get; set; }
        public double Speed { get; set; }
        public double Ra { get; set; }
        public double Rz { get; set; }
        public double Mr1 { get; set; }
        public double Mr2 { get; set; }
        public double Rpk { get; set; }
        public double Rvk { get; set; }
        public double Rk { get; set; }
    }
}

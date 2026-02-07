using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using ConfocalMeter.Models;

namespace ConfocalMeter.Infrastructure.Data
{
    /// <summary>
    /// 粗糙度测量数据服务 - SQLite 数据库操作
    /// 提供测量记录的存储、查询和删除功能
    /// </summary>
    public class MeasurementDataService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbPath">数据库文件路径</param>
        public MeasurementDataService(string dbPath)
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath};Version=3;";
        }

        /// <summary>
        /// 初始化数据库 - 创建表和索引
        /// </summary>
        public void Initialize()
        {
            // 确保目录存在
            string dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                // 创建测量记录表
                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS MeasurementRecords (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        MeasurementTime DATETIME NOT NULL,
                        EvalLength REAL NOT NULL,
                        Interval REAL NOT NULL,
                        Speed REAL NOT NULL,
                        Ra REAL NOT NULL,
                        Rz REAL NOT NULL,
                        Mr1 REAL NOT NULL,
                        Mr2 REAL NOT NULL,
                        Rpk REAL NOT NULL,
                        Rvk REAL NOT NULL,
                        Rk REAL NOT NULL,
                        RawData BLOB
                    );
                ";

                using (var cmd = new SQLiteCommand(createTableSql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // 创建时间索引 (加速按日期查询)
                string createIndexSql = @"
                    CREATE INDEX IF NOT EXISTS idx_measurement_time 
                    ON MeasurementRecords(MeasurementTime);
                ";

                using (var cmd = new SQLiteCommand(createIndexSql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 保存测量记录
        /// </summary>
        /// <param name="record">测量记录对象</param>
        /// <returns>插入的记录 ID</returns>
        public long SaveRecord(MeasurementRecord record)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                string insertSql = @"
                    INSERT INTO MeasurementRecords 
                    (MeasurementTime, EvalLength, Interval, Speed, Ra, Rz, Mr1, Mr2, Rpk, Rvk, Rk, RawData)
                    VALUES 
                    (@time, @evalLength, @interval, @speed, @ra, @rz, @mr1, @mr2, @rpk, @rvk, @rk, @rawData);
                    SELECT last_insert_rowid();
                ";

                using (var cmd = new SQLiteCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@time", record.MeasurementTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@evalLength", record.EvalLength);
                    cmd.Parameters.AddWithValue("@interval", record.Interval);
                    cmd.Parameters.AddWithValue("@speed", record.Speed);
                    cmd.Parameters.AddWithValue("@ra", record.Ra);
                    cmd.Parameters.AddWithValue("@rz", record.Rz);
                    cmd.Parameters.AddWithValue("@mr1", record.Mr1);
                    cmd.Parameters.AddWithValue("@mr2", record.Mr2);
                    cmd.Parameters.AddWithValue("@rpk", record.Rpk);
                    cmd.Parameters.AddWithValue("@rvk", record.Rvk);
                    cmd.Parameters.AddWithValue("@rk", record.Rk);
                    cmd.Parameters.AddWithValue("@rawData", SerializeDoubleArray(record.RawData));

                    return (long)cmd.ExecuteScalar();
                }
            }
        }

        /// <summary>
        /// 按日期范围查询记录 (轻量级，不含 RawData)
        /// </summary>
        /// <param name="start">开始日期</param>
        /// <param name="end">结束日期</param>
        /// <returns>记录列表</returns>
        public List<MeasurementRecordDTO> QueryByDateRange(DateTime start, DateTime end)
        {
            var results = new List<MeasurementRecordDTO>();

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                // 不查询 RawData 以提高速度
                string querySql = @"
                    SELECT Id, MeasurementTime, EvalLength, Interval, Speed, Ra, Rz, Mr1, Mr2, Rpk, Rvk, Rk
                    FROM MeasurementRecords
                    WHERE MeasurementTime BETWEEN @start AND @end
                    ORDER BY MeasurementTime DESC;
                ";

                using (var cmd = new SQLiteCommand(querySql, conn))
                {
                    cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd 00:00:00"));
                    cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd 23:59:59"));

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new MeasurementRecordDTO
                            {
                                Id = reader.GetInt64(0),
                                MeasurementTime = DateTime.Parse(reader.GetString(1)),
                                EvalLength = reader.GetDouble(2),
                                Interval = reader.GetDouble(3),
                                Speed = reader.GetDouble(4),
                                Ra = reader.GetDouble(5),
                                Rz = reader.GetDouble(6),
                                Mr1 = reader.GetDouble(7),
                                Mr2 = reader.GetDouble(8),
                                Rpk = reader.GetDouble(9),
                                Rvk = reader.GetDouble(10),
                                Rk = reader.GetDouble(11)
                            });
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 按 ID 加载原始数据 (用于波形回放)
        /// </summary>
        /// <param name="id">记录 ID</param>
        /// <returns>原始高度数组</returns>
        public double[] LoadRawData(long id)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                string querySql = "SELECT RawData FROM MeasurementRecords WHERE Id = @id;";

                using (var cmd = new SQLiteCommand(querySql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return DeserializeDoubleArray((byte[])result);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 按日期范围删除记录
        /// </summary>
        /// <param name="start">开始日期</param>
        /// <param name="end">结束日期</param>
        /// <returns>删除的记录数</returns>
        public int DeleteByDateRange(DateTime start, DateTime end)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                string deleteSql = @"
                    DELETE FROM MeasurementRecords
                    WHERE MeasurementTime BETWEEN @start AND @end;
                ";

                using (var cmd = new SQLiteCommand(deleteSql, conn))
                {
                    cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd 00:00:00"));
                    cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd 23:59:59"));

                    return cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 获取记录总数
        /// </summary>
        /// <returns>记录数</returns>
        public long GetRecordCount()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM MeasurementRecords;", conn))
                {
                    return (long)cmd.ExecuteScalar();
                }
            }
        }

        #region 序列化辅助方法

        /// <summary>
        /// 将 double[] 序列化为 byte[] (BLOB 存储)
        /// </summary>
        private byte[] SerializeDoubleArray(double[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            // 每个 double 占用 8 字节
            byte[] bytes = new byte[data.Length * sizeof(double)];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// 将 byte[] 反序列化为 double[]
        /// </summary>
        private double[] DeserializeDoubleArray(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            double[] data = new double[bytes.Length / sizeof(double)];
            Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
            return data;
        }

        #endregion
    }
}

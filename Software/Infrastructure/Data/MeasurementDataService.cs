using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using ConfocalMeter.Models;

namespace ConfocalMeter.Infrastructure.Data
{
    /// <summary>
    /// 粗糙度测量数据服务 - SQLite 数据库操作。
    /// </summary>
    public class MeasurementDataService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public MeasurementDataService(string dbPath)
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath};Version=3;Journal Mode=WAL;BusyTimeout=5000;Pooling=True;";
        }

        public void Initialize()
        {
            string dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                using (var pragma = new SQLiteCommand("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;", conn))
                {
                    pragma.ExecuteNonQuery();
                }

                string createTableSql = @"
                    CREATE TABLE IF NOT EXISTS MeasurementRecords (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        MeasurementTime TEXT NOT NULL,
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

        public long SaveRecord(MeasurementRecord record)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    string insertSql = @"
                        INSERT INTO MeasurementRecords
                        (MeasurementTime, EvalLength, Interval, Speed, Ra, Rz, Mr1, Mr2, Rpk, Rvk, Rk, RawData)
                        VALUES
                        (@time, @evalLength, @interval, @speed, @ra, @rz, @mr1, @mr2, @rpk, @rvk, @rk, @rawData);
                        SELECT last_insert_rowid();
                    ";

                    using (var cmd = new SQLiteCommand(insertSql, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@time", record.MeasurementTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
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

                        long id = (long)cmd.ExecuteScalar();
                        tx.Commit();
                        return id;
                    }
                }
            }
        }

        public List<MeasurementRecordDTO> QueryByDateRange(DateTime start, DateTime end)
        {
            var results = new List<MeasurementRecordDTO>();

            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                string querySql = @"
                    SELECT Id, MeasurementTime, EvalLength, Interval, Speed, Ra, Rz, Mr1, Mr2, Rpk, Rvk, Rk
                    FROM MeasurementRecords
                    WHERE MeasurementTime BETWEEN @start AND @end
                    ORDER BY MeasurementTime DESC;
                ";

                using (var cmd = new SQLiteCommand(querySql, conn))
                {
                    cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new MeasurementRecordDTO
                            {
                                Id = reader.GetInt64(0),
                                MeasurementTime = DateTime.ParseExact(reader.GetString(1), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
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
                    cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));

                    return cmd.ExecuteNonQuery();
                }
            }
        }

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

        private byte[] SerializeDoubleArray(double[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            byte[] bytes = new byte[data.Length * sizeof(double)];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private double[] DeserializeDoubleArray(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            double[] data = new double[bytes.Length / sizeof(double)];
            Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
            return data;
        }
    }
}

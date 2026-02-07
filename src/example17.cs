/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 单侧测距基本流程，设置峰选择参数为最大值，选择输出数据为距离1，设置距离修正系数，零点偏移
* 
*/
using System;
using System.Threading;
namespace tscmcnet
{
    using System.Collections.Generic;
    using System.IO;
    partial class Program
    {
        
        static void example17()
        {
            //通信类
            TSCMCAPINET protocol = new TSCMCAPINET();
            //控制器编号 
            int controller_idx = 0;

            IPAddr deviceAddr;
            deviceAddr.c1 = 192;
            deviceAddr.c2 = 168;
            deviceAddr.c3 = 0;
            deviceAddr.c4 = 10;
            int localPort = 8001;

            ERRCODE err;

            Console.WriteLine("打开网络监听通道");

            err = protocol.OpenConnectionEthernet(deviceAddr, localPort);

            checkError(err);
            if (err != ERRCODE.OK)
            {
                return;
            }

            Console.Write("建立连接");
            err = protocol.SetConnectionOn(controller_idx);
            checkError(err);
            if (err != ERRCODE.OK)
            {

                protocol.CloseConnectionPort();
                Console.WriteLine("关闭连接通道");

                return;
            }
            /***********************向下位机发送配置指令区域********************/
            //设置自动曝光目标
            ushort auto_exp_target = 4000;
            int sensor_idx = 1;
            CONNECTION_TYPE connection_type = protocol.GetConnectionType();
            Console.Write("设置自动曝光目标");
            err = protocol.SetConfigAutoExposureTarget(controller_idx, sensor_idx, auto_exp_target);
            checkError(err);
            //设置曝光为自动曝光，此时手动曝光时间无效，
            ExposureConfig exposure_config;
            exposure_config.auto_control = STATE.ON;
            exposure_config.exposure_time = 100;
            Console.Write("设置曝光参数");
            err = protocol.SetConfigExposure(controller_idx, sensor_idx, exposure_config);
            checkError(err);
            //设置采样间隔
            SAMPLING_INTERVAL sampling_interval = SAMPLING_INTERVAL._10MS;
            Console.Write("设置采样间隔");
            err = protocol.SetConfigSamplingInterval(controller_idx, sampling_interval);
            checkError(err);
            //设置峰检测参数
            PeakDetection peak_detection;
            peak_detection.threshold = 100;
            peak_detection.sharpness = 200;
            peak_detection.minimum_spacing = 10;
            Console.Write("设置峰检测参数");
            err = protocol.SetConfigPeakDetection(controller_idx, sensor_idx, peak_detection);
            checkError(err);
            //设置峰选择模式为最大值模式
            PeakSelection peak_selection = new PeakSelection();
            peak_selection.mode = PEAK_SELECTION_MODE.MAX;
            Console.Write("设置峰选择参数");
            err = protocol.SetConfigPeakSelection(controller_idx, sensor_idx, peak_selection);
            checkError(err);
            //选择输出的数据为距离1
            List<int> data_selection = new List<int>();
            data_selection.Add((int)SENSOR_OUTPUT_DATA.DIST1);
            Console.Write("选择输出数据");
            err = protocol.SetConfigOutputSignals(controller_idx, sensor_idx, connection_type, data_selection.ToArray());
            //将通道2和控制器通道数据输出设置为空
            data_selection.Clear();
            err = protocol.SetConfigOutputSignals(controller_idx, 0, connection_type, data_selection.ToArray());
            //err = protocol.SetConfigOutputSignals(controller_idx, 2, connection_type, data_selection.ToArray());
            checkError(err);
            Thread.Sleep(100);
            int data_count = 0;
            for (int i = 0; i <= ConstDef.MAX_SENSOR_CHANNEL; ++i)
            {
                int[] data_selection_ret = new int[] { };
                err = protocol.GetConfigOutputSignals(controller_idx, i, connection_type, ref data_selection_ret);
                data_count += data_selection_ret.Length;
            }
            //设置零点偏移
            protocol.SetConfigZeroOffset( controller_idx, sensor_idx, 0);
            //设置距离修正系数
            protocol.SetConfigMapping( controller_idx, sensor_idx, 1);

            Console.WriteLine("开始连续输出测量值");
            //向设备发送数据输出指令
            err = protocol.SetDataOutputOn(controller_idx);
            Thread.Sleep(100);
            if (IS_ERR_OK(err))
            {
                StreamWriter sw = new StreamWriter("data.txt");
                DataNode[] data = new DataNode[] { };
                DateTime time_start = System.DateTime.Now;
                const int wait_time = 10000;
                while ((System.DateTime.Now - time_start).TotalMilliseconds < wait_time)
                {
                    printTimeProgressBar(time_start, wait_time);
                    int nread = 0;
                    err = protocol.TransferDataNode(ref data, data_count * 10);
                    if (err == ERRCODE.NO_DATA_IN_BUFFER)
                    {
                        continue;
                    }
                    nread = data.Length;
                    for (int i = 0; i < nread; i++)
                    {
                        var str = string.Format("{0} ", data[i].data);
                        sw.Write(str);
                        if ((i + 1) % data_count == 0)
                        {
                            sw.Write("\n");
                        }
                    }
                    Thread.Sleep(500);
                }
                sw.Close();
            }
            else
            {
                Console.WriteLine("错误：{0}", getErrorCodeString(err));
            }
            if (IS_ERR_OK(err))
            {
                Console.WriteLine("停止连续输出测量值");
                err = protocol.SetDataOutputOff(controller_idx);
                checkError(err);
            }
            /*******************************************************************/
            //向下位机发送断开指令
            Console.Write("断开连接");
            err = protocol.SetConnectionOff(controller_idx);
            checkError(err);

            protocol.CloseConnectionPort();
            Console.WriteLine("关闭连接通道");

        }

    }
}
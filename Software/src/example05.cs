/*!
* 本样例实现了打开以太网通道，建立连接
* 实现读取单次测量数据
* 
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
        static void example05()
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
            //选择要输出的数据
            List<int> data_selection = new List<int>();
            data_selection.Add((int)SENSOR_OUTPUT_DATA.DIST1);
            data_selection.Add((int)SENSOR_OUTPUT_DATA.DIST2);
            data_selection.Add((int)SENSOR_OUTPUT_DATA.INTENSITY);
            data_selection.Add((int)SENSOR_OUTPUT_DATA.EXPTIME);
            data_selection.Add((int)SENSOR_OUTPUT_DATA.THICKNESS);
            Console.Write("选择输出数据");
            CONNECTION_TYPE connection_type = CONNECTION_TYPE.ETHERNET;
            //只选择探头通道1数据，其它探头和控制器数据都不选择
            int targetChannel = 1;
            err = protocol.SetConfigOutputSignals(controller_idx, targetChannel, connection_type, data_selection.ToArray());
            data_selection.Clear();
            for(int i = 0;i<protocol.MaxSensorChannels();++i)
            {
                if (i == targetChannel) continue;
                err = protocol.SetConfigOutputSignals(controller_idx, 0, connection_type, data_selection.ToArray());
            }
            if (!IS_ERR_OK(err))
            {
                Console.Write("错误：{0}\n", getErrorCodeString(err));
                protocol.CloseConnectionPort();
                Console.Write("关闭USB\n");
                return;
            }
            else
            {
                Console.Write("\n");
            }
            //获取单次测量数据
            int n_read = 0;
            var measurement_data = new double[] { };
            Console.Write("获取单次测量数据\n");
            err = protocol.GetSingleData(controller_idx, ref measurement_data);
            if (IS_ERR_OK(err))
            {
                n_read = measurement_data.Length;
                Console.Write("数据长度为：{0}\n", n_read);
                for (int i = 0; i < n_read; ++i)
                {
                    Console.Write("{0}: {1}\n", i + 1, (float)measurement_data[i]);
                }
            }
            else
            {
                Console.Write("错误：{0}\n", getErrorCodeString(err));
            }
            Console.Write("距离1警告：{0}\n", protocol.getWarning(WARNING_INPUT_CHANNEL.CH1, WARNING_SOURCE.DIST1));
            //Console.Write("距离2警告：{0}\n", protocol.getWarning(WARNING_INPUT_CHANNEL.CH1, WARNING_SOURCE.DIST2));
            //Console.Write("厚度警告：{0}\n", protocol.getWarning(WARNING_INPUT_CHANNEL.CH1, WARNING_SOURCE.THICKNESS));
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

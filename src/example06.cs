/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 读取连续测量数据
*/
using System;
using System.Threading;
namespace tscmcnet
{
    using System.Collections.Generic;
    using System.IO;

    partial class Program
    {

        static void example06()
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
            CONNECTION_TYPE connection_type = CONNECTION_TYPE.ETHERNET;
            List<int> data_selection = new List<int>();
            data_selection.Add((int)CONTROLLER_OUTPUT_DATA.TIMESTAMP);
            print_data_msg(0, data_selection);

            err = protocol.SetConfigOutputSignals(controller_idx, 0, connection_type, data_selection.ToArray());
            data_selection.Clear();
            data_selection.Add((int)SENSOR_OUTPUT_DATA.DIST1);//距离1
            //data_selection.Add((int)SENSOR_OUTPUT_DATA.DIST2);//距离2
            data_selection.Add((int)SENSOR_OUTPUT_DATA.EXPTIME);
            //data_selection.Add((int)SENSOR_OUTPUT_DATA.THICKNESS);//厚度
            Console.Write("选择输出数据\n");
            print_data_msg(1, data_selection);
            err = protocol.SetConfigOutputSignals(controller_idx, 1, connection_type, data_selection.ToArray());
            data_selection.Clear();
            err = protocol.SetConfigOutputSignals(controller_idx, 2, connection_type, data_selection.ToArray());
            int data_count = 0;
            int[] data_selection_ret = new int[] { };
            err = protocol.GetConfigOutputSignals(controller_idx, 0, connection_type, ref data_selection_ret);
            data_count += data_selection_ret.Length;
            err = protocol.GetConfigOutputSignals(controller_idx, 1, connection_type, ref data_selection_ret);
            data_count += data_selection_ret.Length;
            if (!IS_ERR_OK(err))
            {
                Console.WriteLine("错误：{0}", TSCMCAPINET.GetErrorCodeString(err));
                protocol.CloseConnectionPort();
                Console.WriteLine("关闭连接通道");
                return;
            }
            else
            {
                Console.WriteLine("");
            }
            Thread.Sleep(100);
            Console.WriteLine("开始连续输出测量值");
            //向下位机发送数据输出指令
            err = protocol.SetDataOutputOn(controller_idx);
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
                    err = protocol.TransferAllDataNode(ref data, data_count * 10);
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
                Console.Write("停止连续输出测量值");
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
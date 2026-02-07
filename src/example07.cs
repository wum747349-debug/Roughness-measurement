/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
*  连续测量时，读取缓冲区中数据组数，达到目标值后再读取数据
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
        static void example07()
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
            CONNECTION_TYPE connection_type = CONNECTION_TYPE.ETHERNET;
            List<int> data_selection = new List<int>();
            data_selection.Add((int)CONTROLLER_OUTPUT_DATA.TIMESTAMP);//时间戳
            print_data_msg(0, data_selection);

            err = protocol.SetConfigOutputSignals(controller_idx, 0, connection_type, data_selection.ToArray());
            data_selection.Clear();
            data_selection.Add((int)SENSOR_OUTPUT_DATA.DIST1);//距离1

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
                Console.WriteLine("错误：{0}", getErrorCodeString(err));
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

            const int maxNum = 2000;
            if (IS_ERR_OK(err))
            {
                int dataGroupsNum = 0;
                int numIndex = 1;
                while (true)
                {
                    protocol.RingBufferDataSize(ref dataGroupsNum);
                    if (dataGroupsNum > numIndex * 1000)
                    {
                        Console.WriteLine("当前组数:{0}\n", dataGroupsNum);
                        numIndex++;
                    }

                    if (dataGroupsNum > maxNum)
                    {
                        break;
                    }
                }

                StreamWriter sw = new StreamWriter("data.txt");
                DataNode[] data = new DataNode[] { };

                int nread = 0;
                err = protocol.TransferAllDataNode(ref data, maxNum * data_count);
            
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
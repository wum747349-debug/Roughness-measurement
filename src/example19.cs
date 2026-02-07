/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 双侧测厚基本流程，判断是否为双通道以上控制器，设置MATH计算方法，选择输出数据为MATH
*/
using System;
using System.Threading;
namespace tscmcnet
{
    using System.Collections.Generic;
    using System.IO;
    partial class Program
    {
        static void example19()
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
            CONNECTION_TYPE connection_type = protocol.GetConnectionType();
            //设置通道1使能，通道2使能
            ChannelEnable ce;
            ce.channelCnt = 2;//实际通道数目为2
            ce.channelState = (short)CHANNEL_ENABLE_MODE.CH1 | (short)CHANNEL_ENABLE_MODE.CH2;
            Console.Write("设置通道1和2使能");
            protocol.SetContorllerChannelEnable(controller_idx, ce);
            checkError(err);
            //设置通道1用于计算厚度的数据为距离1，符号为正
            ChannelSetting chst1, chst2;
            chst1.sign = MATHSIGN.POS;
            chst1.src = MATH_DATA_SRC.DIST1;
            //设置通道2用于计算厚度的数据为距离1，符号为负
            chst2.sign = MATHSIGN.NEG;
            chst2.src = MATH_DATA_SRC.DIST1;
            Console.Write("MATH数据输出配置");
            err = protocol.SetConfigMath(controller_idx, chst1, chst2);
            checkError(err);
            //选择输出数据
            List<int> data_selection = new List<int>();
            data_selection.Add((int)CONTROLLER_OUTPUT_DATA.MATH1);
            Console.Write("选择控制器输出数据为MATH1");
            err = protocol.SetConfigOutputSignals(controller_idx, 0, connection_type, data_selection.ToArray());
            checkError(err);
            data_selection.Clear();
            for (int i = 1; i <= ConstDef.MAX_SENSOR_CHANNEL; ++i)
            {
                Console.Write("设置通道{0}输出数据为空", i);
                err = protocol.SetConfigOutputSignals(controller_idx, i, connection_type, data_selection.ToArray());
                checkError(err);
            }
            int data_count = 0;
            for (int i = 0; i <= ConstDef.MAX_SENSOR_CHANNEL; ++i)
            {
                int[] data_selection_ret = new int[] { };
                err = protocol.GetConfigOutputSignals(controller_idx, i, connection_type, ref data_selection_ret);
                data_count += data_selection_ret.Length;
            }
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
            //向设备发送数据输出指令
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
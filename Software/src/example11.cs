/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 设置基础测量配置，包含抽样系数、滑动平均窗宽、无效数据保持
*/
using System;
using System.Threading;
namespace tscmcnet
{

    partial class Program
    {
        static void example11()
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
            protocol.SetDataSubSamplingFactor( controller_idx, 100);
            ushort factor = 0;
            Thread.Sleep(100);
            protocol.GetDataSubSamplingFactor( controller_idx, ref factor);
            Console.WriteLine("抽样系数为:{0}", factor);

            FILTER_WINDOW_WIDTH windowWidth = FILTER_WINDOW_WIDTH._256;
            protocol.SetConfigMoveAvarage( controller_idx, windowWidth);
            Thread.Sleep(100);
            FILTER_WINDOW_WIDTH windowWidthOut = new FILTER_WINDOW_WIDTH();
            err = protocol.GetConfigMoveAvarage( controller_idx, ref windowWidthOut);
            print_msg(err, windowWidthOut);

            protocol.SetWarningHoldPoints( controller_idx, 1000);
            Thread.Sleep(100);
            int points = 0;
            protocol.GetWarningHoldPoints( controller_idx, ref points);
            Console.WriteLine("警告保持点数为:{0}\n", points);
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
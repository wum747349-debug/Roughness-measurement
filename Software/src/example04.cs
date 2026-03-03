/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 上位机和下位机通信IP地址和端口号应先通过配置软件发送到下位机，否则无法建立连接
* 以太网通信之前确定PC与下位机连接的网口的IP地址，
*/
using System;
using System.Threading;
namespace tscmcnet
{

    partial class Program
    {
        static void example04()
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

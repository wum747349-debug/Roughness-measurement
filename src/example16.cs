/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 写入配置到Flash、恢复出厂设置、保存配置到文件、从文件读取配置并发送到设备
*/
using System;
using System.Threading;
namespace tscmcnet
{
    partial class Program
    {
        static void example16()
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
            string filename = "test.dat";
            Console.WriteLine("保存配置到文件");
            err = protocol.SaveControllerConfig(filename);
            Console.Write("{0}\n", err);
            checkError(err);
            Thread.Sleep(1000);
            Console.Write("从文件读取配置并发送到控制器");
            err = protocol.ReadControllerConfig(filename);
            checkError(err);
            Console.Write("{0}\n", err);
            Thread.Sleep(1000);

            Console.WriteLine("将控制器参数恢复到默认出厂配置 \n");
            protocol.RestoreFactorySetting();
            Thread.Sleep(1000);
            Console.WriteLine("将控制器参数写入Flash \n");
            protocol.SetConfigControllerSettings( controller_idx);
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
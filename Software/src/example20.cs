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
        static void example20()
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
            Console.Write("打开监听通道");
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
            int maxlength = ConstDef.FFT_FRAME_PIXEL_NUM;
            int nread = 0;
            Console.WriteLine("dataFrame\n");
            double[] data1 = new double[1];
            protocol.GetFFTDataFrame(controller_idx, 1, ref data1);
            for (int i = 0; i < data1.Length; i++)
            {
                Console.WriteLine("{0} {1}", i, data1[i]);
            }
            protocol.SetInterferenceThickCorrectionFactor(controller_idx, 1, 0.8);
            double factor = 0.0;
            Console.WriteLine("factor: ");
            protocol.GetInterferenceThickCorrectionFactor(controller_idx, 1, ref factor);
            Console.WriteLine("{0}\n", factor);
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

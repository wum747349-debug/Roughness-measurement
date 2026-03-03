/*
*  以太网连接
*  读取一帧原始图像，读取量程起终点像素
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
        static void example08()
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
            double[] intensityFrame = new double[ConstDef.CMOS_PIXEL_NUM];
            Console.Write("读取单帧图像\n");
            err = protocol.GetDataFrameSingle(controller_idx, 1, ref intensityFrame);
            if (!IS_ERR_OK(err))
            {
                Console.Write("失败\n");
                protocol.CloseConnectionPort();
                Console.Write("关闭以太网\n");

                return;
            }
            StreamWriter sw = new StreamWriter("frame_cs.txt");

            for (int k = 0; k < intensityFrame.Length; ++k)
            {
                var str = String.Format("{0} {1}\n", k, intensityFrame[k]);
                sw.Write(str);
                Console.Write(str);
            }

            //读取量程起终点像素
            int range_start_pixel = 0;
            int range_end_pixel = 0;

            protocol.GetConfigRangeEdgePixel( controller_idx, 1,ref range_start_pixel, ref range_end_pixel);
            Console.Write("量程起点对应的像素位置:{0}\n", range_start_pixel);
            Console.Write("量程终点对应的像素位置:{0}\n", range_end_pixel);

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
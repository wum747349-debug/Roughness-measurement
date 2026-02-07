/*!
* 本样例实现了打开以太网通道，并从控制器下载探头校准表
*/
using System;
using System.Threading;



namespace tscmcnet
{
    using System.Collections.Generic;
    using System.IO;
    
    partial class Program
    {
        static void test01()
        {
            //通信类
            TSCMCAPINET protocol = new TSCMCAPINET();
            //控制器编号 
            int controller_idx = 0;

            bool ret = false;
            Console.WriteLine("设置通信类型为以太网");
            protocol.SetConnectionType(CONNECTION_TYPE.ETHERNET);
            //设置本地网口监听端口
            int localPort = 8001;
            protocol.SetUdpPort(localPort);
            //设置要连接的下位机IP地址与通信端口
            int destPort = 8000;
            string destAddress = "192.168.0.10";
            Console.Write("设置下位机IP");
            ret = protocol.SetDestUdpEndPoint(destAddress, destPort);
            checkError(ret);
            if (!ret)
            {
                return;
            }
            //绑定以太网端口，开始监听
            Console.Write("打开连接通道");
            ret = protocol.OpenConnectionPort();
            checkError(ret);
            if (!ret)
            {
                return;
            }
            ERRCODE err;

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

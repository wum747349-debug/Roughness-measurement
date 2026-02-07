/*!
* 本样例实现了打开USB通道，并通过串口向下位机发送连接确认指令
*/
using System;
using System.Threading;
namespace tscmcnet
{
    using System.Collections.Generic;

    partial class Program
    {
        static void example03()
        {
            //通信类
            TSCMCAPINET protocol = new TSCMCAPINET();
            //控制器编号 
            int controller_idx = 0;
            int portCOM = 4;
            ERRCODE err;
            Console.Write("打开USB通道");
            err = protocol.OpenConnectionUSBPort(portCOM);
            checkError(err);
            if (!IS_ERR_OK(err))
            {
                return;
            }

            Console.Write("建立连接");
            err = protocol.SetConnectionOn(controller_idx);
            checkError(err);
            if (!IS_ERR_OK(err))
            {
                protocol.CloseConnectionPort();
                Console.WriteLine("关闭连接通道");
                return;
            }
            /***********************向下位机发送配置指令区域********************/
            Thread.Sleep(1000);
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

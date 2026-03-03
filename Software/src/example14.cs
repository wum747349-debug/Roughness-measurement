/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 设置模拟输出参数
*/
using System;
using System.Threading;
namespace tscmcnet
{

    partial class Program
    {
        static void example14()
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
            ANALOG_CHANNEL analog_channel;
            AnalogOutputSetting aos;
            analog_channel = ANALOG_CHANNEL.CH1;
            aos.output_en = STATE.ON;
            aos.input_channel = ANALOG_INPUT_CHANNEL.CH1;
            aos.source = ANALOG_SOURCE.DIST1;
            aos.range = ANALOG_OUT_RANGE.V_0TO10;
            aos.distance_start = 0;
            aos.distance_end = 1;
            err = protocol.SetAnalogOutputSetting( 0, analog_channel, aos);
            Console.WriteLine("设置模拟输出");
            print_msg(err, analog_channel, aos);
            if (IS_ERR_OK(err))
            {
                Console.WriteLine("读取模拟输出");
                AnalogOutputSetting aos_r = new AnalogOutputSetting();
                err = protocol.GetAnalogOutputSetting( 0, analog_channel, ref aos_r);
                checkError(err);
                print_msg(err, analog_channel, aos);
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
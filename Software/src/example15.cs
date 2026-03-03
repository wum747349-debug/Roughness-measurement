/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 设置数字输出参数，包括警告上下限、数字输出配置
*/
using System;
using System.Threading;
namespace tscmcnet
{
    partial class Program
    {
        static void example15()
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
            ChannelDigitalOutput digital_output;
            digital_output.input_channel = DIGITAL_INPUT_CHANNEL.CH1;
            digital_output.input_source = DIGITAL_INPUT_SRC.DIST1;
            digital_output.output_cond = DIGITAL_OUTPUT_COND.OVER_LIMIT;
            digital_output.output_en = STATE.OFF;
            digital_output.output_level = DIGITAL_OUTPUT_LEVEL.HIGH;
            Console.WriteLine("设置数字输出参数\n");
            DIGITAL_CHANNEL digital_output_channel = DIGITAL_CHANNEL.CH1;
            err = protocol.SetConfigDigitalOutput( controller_idx, digital_output_channel, digital_output);
            print_msg(err, digital_output_channel, digital_output);
            if (IS_ERR_OK(err))
            {
                ChannelDigitalOutput digital_output_ret = new ChannelDigitalOutput();
                Console.WriteLine("读取数字输出参数\n");
                err = protocol.GetConfigDigitalOutput( controller_idx, digital_output_channel, ref digital_output_ret);
                print_msg(err, digital_output_channel, digital_output_ret);
            }

            double upper_limit = 10.56;
            double hysteresis = 0.2;
            Console.Write("设置警告量程上限");
            DIGITAL_INPUT_CHANNEL channel = DIGITAL_INPUT_CHANNEL.CH1;
            DIGITAL_INPUT_SRC src = DIGITAL_INPUT_SRC.DIST1;
            err = protocol.SetConfigUpperlimit(controller_idx, channel, src, upper_limit, hysteresis);
            print_msg(err, channel, src, upper_limit, hysteresis);
            if (IS_ERR_OK(err))
            {
                Console.Write("读取警告量程上限");
                double upper_limit_ret = 0;
                double hysteresis_ret = 0;
                err = protocol.GetConfigUpperlimit(controller_idx, channel, src, ref upper_limit_ret, ref hysteresis_ret);
                print_msg(err, channel, src, upper_limit_ret, hysteresis_ret);
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
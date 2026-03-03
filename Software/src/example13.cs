/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
*
* 设置外部触发相关参数
*/
using System;
using System.Threading;
namespace tscmcnet
{

    partial class Program
    {
        static void example13()
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
            ExternalTrigger et = new ExternalTrigger();
            //以下为配置不同触发方式时的参数组合，用户根据实际情况选取其中一种
            //内部触发
            et.trig_method = TRIG_METHOD.NONE;
            et.sync_setting.state = STATE.OFF;
            et.sync_setting.input_mode = SYNC_INPUT_MODE.LEVEL;
            et.sync_setting.valid_level = SYNC_VALID_LEVEL.HIGH;//根据实际情况选择
            et.sync_setting.sample_per_trigger = 1;
            //内部触发+电平控制采样
            et.trig_method = TRIG_METHOD.NONE;
            et.sync_setting.state = STATE.ON;
            et.sync_setting.input_mode = SYNC_INPUT_MODE.LEVEL;
            et.sync_setting.valid_level = SYNC_VALID_LEVEL.HIGH;//根据实际情况选择
            et.sync_setting.sample_per_trigger = 1;
            //编码器
            et.trig_method = TRIG_METHOD.ENCODER;
            et.sync_setting.state = STATE.OFF;
            et.sync_setting.input_mode = SYNC_INPUT_MODE.LEVEL;
            et.sync_setting.valid_level = SYNC_VALID_LEVEL.HIGH;//根据实际情况选择
            et.sync_setting.sample_per_trigger = 1;
            //编码器+电平控制采样
            et.trig_method = TRIG_METHOD.ENCODER;
            et.sync_setting.state = STATE.ON;
            et.sync_setting.input_mode = SYNC_INPUT_MODE.LEVEL;
            et.sync_setting.valid_level = SYNC_VALID_LEVEL.HIGH;//根据实际情况选择
            et.sync_setting.sample_per_trigger = 1;
            //SYNC边沿触发采样特定点数
            et.trig_method = TRIG_METHOD.SYNCIN;
            et.sync_setting.state = STATE.ON;
            et.sync_setting.input_mode = SYNC_INPUT_MODE.EDGE;
            et.sync_setting.valid_level = SYNC_VALID_LEVEL.HIGH;//根据实际情况选择
            et.sync_setting.sample_per_trigger = 1;
            err = protocol.SetConfigExternalTrigger(controller_idx, et);
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
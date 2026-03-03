/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 设置编码器触发相关参数，
* 包括脉冲比例系数、手动置位、Z信号使能、计数使能，运行参数，触发配置、外部触发方式
*/
using System;
using System.Threading;
namespace tscmcnet
{

    partial class Program
    {
        static void example12()
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
            /******外部触发方式********/
            ExternalTrigger et = new ExternalTrigger();
            //编码器
            et.trig_method = TRIG_METHOD.ENCODER;
            et.sync_setting.state = STATE.OFF;
            et.sync_setting.input_mode = SYNC_INPUT_MODE.LEVEL;
            et.sync_setting.valid_level = SYNC_VALID_LEVEL.HIGH;//根据实际情况选择
            et.sync_setting.sample_per_trigger = 1;
            err = protocol.SetConfigExternalTrigger( controller_idx, et);

            /******触发配置********/
            TriggerSetting trigger_setting;
            trigger_setting.channel = ENCODER_CHANNEL.CH1;      //编码器通道1   
            trigger_setting.downsample_factor = 5;                 //采样系数
            trigger_setting.direction = TRIG_DIRECTION.POS;     //编码器通道触发方向 正向
            trigger_setting.mode = TRIG_MODE.POSITION;          //编码器通道触发方式 位置触发 
            trigger_setting.track_mode = TRIG_TRACK_MODE.ON;    //编码器通道换向模式 ON
            Console.WriteLine("设置编码器触发参数");
            err = protocol.SetConfigTriggerSetting( controller_idx, trigger_setting);
            print_msg(err, trigger_setting);
            if (IS_ERR_OK(err))
            {
                TriggerSetting trigger_setting_ret = new TriggerSetting();
                Console.WriteLine("设置编码器触发参数");
                err = protocol.GetConfigTriggerSetting( controller_idx,ref trigger_setting_ret);
                print_msg(err, trigger_setting_ret);
            }

            /******设置编码器参数********/
            ENCODER_CHANNEL encoder_channel = ENCODER_CHANNEL.CH1;
            EncoderSetting encoder_setting;
            encoder_setting.filter_width = ENCODER_FILTER_WIDTH._4;
            encoder_setting.input_mode = ENCODER_INPUT_MODE.A;
            encoder_setting.output_mode = ENCODER_OUTPUT_MODE.X2;
            encoder_setting.z_phase = false;
            Console.WriteLine("设置编码器输入输出参数\n");
            err = protocol.SetConfigEncoderSetting( controller_idx, encoder_channel, encoder_setting);
            if (!IS_ERR_OK(err))
            {
                Console.WriteLine("失败\n");
                protocol.CloseConnectionPort();
                Console.WriteLine("关闭连接通道\n");
                return;
            }
            print_msg(err, encoder_channel, encoder_setting);
            EncoderSetting encoder_setting_ret = new EncoderSetting();
            Console.WriteLine("读取编码器输入输出参数\n");
            err = protocol.GetConfigEncoderSetting( controller_idx, encoder_channel, ref encoder_setting_ret);
            print_msg(err, encoder_channel, encoder_setting_ret);

            /******手动置位********/

            double encoder_position = 10.1;//mm
            Console.WriteLine("设置编码器手动置位位置\n");
            err = protocol.SetConfigEncoderPosition( controller_idx, encoder_channel, encoder_position);
            if (!IS_ERR_OK(err))
            {
                Console.WriteLine("失败\n");
                //protocol.CloseConnectionPort();
                Console.WriteLine("关闭连接通道\n");
                protocol.CloseConnectionPort();
                return;
            }
            Console.WriteLine("编码器手动置位位置: {0} mm\n", encoder_position);
            
            double encoder_position_ret = 0;
            Console.WriteLine("读取编码器手动置位位置\n");
            err = protocol.GetConfigEncoderPosition( controller_idx, encoder_channel, ref encoder_position_ret);
            Console.WriteLine("编码器手动置位位置: {0} mm\n", encoder_position);

            /******脉冲比例系数(分辨率)********/

            double resolution = 0.0001;//mm
            Console.WriteLine("设置编码器分辨率\n");
            err = protocol.SetConfigEncoderResolution( controller_idx, encoder_channel, resolution);
            checkError(err);
            print_msg(err, ENCODER_CHANNEL.CH1, resolution);
            double resolution_ret = 0;
            Console.WriteLine("读取编码器分辨率\n");
            err = protocol.GetConfigEncoderResolution( controller_idx, encoder_channel, ref resolution_ret);
            print_msg(err, ENCODER_CHANNEL.CH1, resolution_ret);

            /*******Z相信号置位*******/
            double zphase_position = 1.126;//mm
            Console.WriteLine("设置编码器Z相信号置位位置\n");
            err = protocol.SetConfigZPhasePosition( controller_idx, encoder_channel, zphase_position);
            if (!IS_ERR_OK(err))
            {
                Console.WriteLine("失败\n");
                Console.WriteLine("关闭连接通道\n");
                protocol.CloseConnectionPort();
                return;
            }
            print_msg_zphase_position(err, encoder_channel, zphase_position);
           
            double zphase_position_ret = 0;
            Console.WriteLine("读取编码器Z相信号置位位置\n");
            err = protocol.GetConfigZPhasePosition(controller_idx, encoder_channel, ref zphase_position_ret);
            print_msg_zphase_position(err, encoder_channel, zphase_position);

            /*******计数使能*******/
            STATE counter_enable = STATE.OFF;
            Console.WriteLine("设置编码器{0}计数使能状态\n", encoder_channel);
            err = protocol.SetConfigEncoderCounterEnable( controller_idx, encoder_channel, counter_enable);
            if (!IS_ERR_OK(err))
            {
                Console.WriteLine("失败\n");
                protocol.CloseConnectionPort();
                Console.WriteLine("关闭连接通道\n");
                return;
            }
            print_msg(err, counter_enable);
            STATE counter_enable_ret = new STATE();
            Console.WriteLine("读取编码器{0}计数使能状态\n", encoder_channel);
            err = protocol.GetConfigEncoderCounterEnable( controller_idx, encoder_channel, ref counter_enable_ret);
            print_msg(err, counter_enable_ret);
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
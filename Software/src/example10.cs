/*!
* 本样例实现了打开以太网通道，并通过以太网向下位机发送连接确认指令
* 设置光源开关、光强、采样间隔、曝光参数、目标曝光值、峰检测
* 
*/

using System;
using System.Threading;
namespace tscmcnet
{

    partial class Program
    {
        static void example10()
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
            /*******设置光源开关*******/
            STATE state = STATE.OFF;                      //可观察光斑
            protocol.SetConfigLightSource( controller_idx, 1, state);
            Thread.Sleep(2000);

            state = STATE.ON;
            protocol.SetConfigLightSource( controller_idx, 1, state);
            Thread.Sleep(100);

            /*******设置LED光源强度*******/
            protocol.SetConfigLightIntensity( controller_idx, 1, 100);
            double intensity = 0;
            Thread.Sleep(100);
            protocol.GetConfigLightIntensity( controller_idx, 1, ref intensity);
            Console.WriteLine("当前探头LED光源强度:{0}\n", intensity);

            /*******设置采样间隔*******/
            SAMPLING_INTERVAL sampling_interval = SAMPLING_INTERVAL._10MS;
            Console.WriteLine("设置采样间隔：");
            err = protocol.SetConfigSamplingInterval( controller_idx, sampling_interval);
            print_msg(err, sampling_interval);
            Thread.Sleep(100);
            if (IS_ERR_OK(err))
            {
                SAMPLING_INTERVAL sampling_interval_ret = SAMPLING_INTERVAL._250US;
                Console.WriteLine("读取采样间隔：");
                err = protocol.GetConfigSamplingInterval( controller_idx, ref sampling_interval_ret);
                print_msg(err, sampling_interval_ret);
            }

            /*******设置曝光参数*******/
            ExposureConfig exposureConfig;
            exposureConfig.auto_control = STATE.OFF;
            exposureConfig.exposure_time = 5000;
            protocol.SetConfigExposure( controller_idx, 1, exposureConfig);
            Thread.Sleep(100);
            ExposureConfig exposureConfigOut = new ExposureConfig();
            protocol.GetConfigExposure(controller_idx, 1, ref exposureConfigOut);
            Console.WriteLine("是否为自动模式(1为是)：{0}\n", (exposureConfigOut.auto_control == STATE.ON) ? 1 : 0);
            Console.WriteLine("手动曝光时间为:{0}us\n", exposureConfigOut.exposure_time);

            /*******设置目标曝光值*******/
            protocol.SetConfigAutoExposureTarget( controller_idx, 1, 4000);
            ushort target = 0;
            Thread.Sleep(100);
            protocol.GetConfigAutoExposureTarget( controller_idx, 1,ref target);
            Console.WriteLine("目标曝光值:{0}", target);

            /*******设置峰值检测参数*******/
            PeakDetection peakDetection;
            peakDetection.minimum_spacing = 20;
            peakDetection.threshold = 200;
            peakDetection.sharpness = 100;
            protocol.SetConfigPeakDetection(controller_idx, 1, peakDetection);
            Thread.Sleep(100);
            PeakDetection peakDetectionOut = new PeakDetection();
            protocol.GetConfigPeakDetection( controller_idx, 1, ref peakDetectionOut);
            Console.WriteLine("峰间隔:{0}\n", peakDetectionOut.minimum_spacing);
            Console.WriteLine("峰锐度:{0}\n", peakDetectionOut.sharpness);
            Console.WriteLine("峰阈值:{0}\n", peakDetectionOut.threshold);
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
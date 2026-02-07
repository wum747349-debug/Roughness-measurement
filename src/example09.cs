/*
*  USB连接
*  设置网络通信参数
*  TSCMCAPI_SetConfigEthernet
*  设置控制器网络参数 
*  
*/

using System;
using System.Threading;
namespace tscmcnet
{

    partial class Program
    {
        /*
        * @brief 打印采样间隔信息
        */
        static void print_msg(ERRCODE err, SAMPLING_INTERVAL sampling_interval)
        {

            if (IS_ERR_OK(err))
            {
                switch (sampling_interval)
                {
                    case SAMPLING_INTERVAL._250US:
                        Console.WriteLine("250us");
                        break;
                    case SAMPLING_INTERVAL._500US:
                        Console.WriteLine("500us");
                        break;
                    case SAMPLING_INTERVAL._1MS:
                        Console.WriteLine("1ms");
                        break;
                    case SAMPLING_INTERVAL._2MS:
                        Console.WriteLine("2ms");
                        break;
                    case SAMPLING_INTERVAL._5MS:
                        Console.WriteLine("5ms");
                        break;
                    case SAMPLING_INTERVAL._10MS:
                        Console.WriteLine("10ms");
                        break;
                    case SAMPLING_INTERVAL._100US:
                        Console.WriteLine("100us");
                        break;
                    case SAMPLING_INTERVAL._125US:
                        Console.WriteLine("125us");
                        break;
                    case SAMPLING_INTERVAL._160US:
                        Console.WriteLine("160us");
                        break;
                    case SAMPLING_INTERVAL._200US:
                        Console.WriteLine("200us");
                        break;
                    default:
                        break;
                }
            }
            else
            {
                Console.WriteLine("错误：{0}", getErrorCodeString(err));
            }
        }

        static void example09()
        {
            //通信类
            TSCMCAPINET protocol = new TSCMCAPINET();
            //控制器编号 
            int controller_idx = 0;
            int portCOM = 4;
            ERRCODE err;
            Console.WriteLine("打开USB通道");
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
            EthernetConfiguration ethernet_configuration = new EthernetConfiguration();
            ethernet_configuration.ip.c1 = 192;
            ethernet_configuration.ip.c2 = 168;
            ethernet_configuration.ip.c3 = 0;
            ethernet_configuration.ip.c4 = 10;
            ethernet_configuration.subnet_mask.c1 = 255;
            ethernet_configuration.subnet_mask.c2 = 255;
            ethernet_configuration.subnet_mask.c3 = 255;
            ethernet_configuration.subnet_mask.c4 = 0;
            ethernet_configuration.gateway.c1 = 192;
            ethernet_configuration.gateway.c2 = 168;
            ethernet_configuration.gateway.c3 = 0;
            ethernet_configuration.gateway.c4 = 1;
            ethernet_configuration.host_addr_last_char = 20;
            ethernet_configuration.host_port = 8001;
            Console.Write("设置网络参数");
            err = protocol.SetConfigEthernet(controller_idx, ethernet_configuration);
            print_msg(err, ethernet_configuration);
            //等待下位机将设置的参数保存
            Thread.Sleep(2000);
            if (IS_ERR_OK(err))
            {
                EthernetConfiguration ethernet_configuration_ret = new EthernetConfiguration();
                Console.Write("读取网络参数");
                err = protocol.GetConfigEthernet(controller_idx, ref ethernet_configuration_ret);
                print_msg(err, ethernet_configuration_ret);
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
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
        static void test02()
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

            tscmcnet.DarkReferenceTable table_ret = new DarkReferenceTable();
            Console.WriteLine("下载暗校准表");
            err = protocol.DownloadDarkReference(controller_idx, 1, ref table_ret);
            checkError(err);
            if (err == ERRCODE.OK)
            {
                using (StreamWriter sw = new StreamWriter("dark.txt"))
                {
                    for (int i = 0; i < table_ret.refr.data.Length; ++i)
                    {
                        string str = string.Format("{0},{1}\n", table_ret.refr.data[i], table_ret.coeff.data[i]);
                        sw.Write(str);
                        Console.Write(str);
                    }
                }

            }

            /*******************************************************************/
            //向下位机发送断开指令
            Console.Write("断开连接");
            err = protocol.SetConnectionOff(controller_idx);
            checkError(err);

            protocol.CloseConnectionPort();
            Console.WriteLine("关闭连接通道");

        }
        //测试单次测量数据，用double形式读取
        static void test03()
        {
            //通信类
            TSCMCAPINET protocol = new TSCMCAPINET();
            //控制器编号 
            int controller_idx = 0;
            Console.Write("设置通信类型为以太网");
            bool ret = false;
            CONNECTION_TYPE connection_type = CONNECTION_TYPE.ETHERNET;
            protocol.SetConnectionType(connection_type);
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
            if (!IS_ERR_OK(err))
            {

                protocol.CloseConnectionPort();
                Console.WriteLine("关闭连接通道");
                return;
            }
            /***********************向下位机发送配置指令区域********************/
            List<int> data_selection = new List<int>();
            data_selection.Add((int)CONTROLLER_OUTPUT_DATA.TIMESTAMP);
            print_data_msg(0, data_selection);
            err = protocol.SetConfigOutputSignals(controller_idx, 0, connection_type, data_selection.ToArray()); ;
            data_selection.Clear();
            data_selection.Add((int)SENSOR_OUTPUT_DATA.DIST1);//输出通道1距离1
            data_selection.Add((int)SENSOR_OUTPUT_DATA.DIST2);//输出通道1距离2
            data_selection.Add((int)SENSOR_OUTPUT_DATA.THICKNESS);//输出通道1厚度
            for (int i = 1; i <= ConstDef.MAX_SENSOR_CHANNEL; ++i)
            {
                //print_msg(i, data_selection);
                err = protocol.SetConfigOutputSignals(controller_idx, i, connection_type, data_selection.ToArray());
            }
            if (!IS_ERR_OK(err))
            {
                Console.WriteLine("错误：{0}", getErrorCodeString(err));

                protocol.CloseConnectionPort();
                Console.WriteLine("关闭连接通道");
                return;
            }
            else
            {
                Console.WriteLine("");
            }
            //获取单次测量数据
            int nread = 0;
            double[] data = null;
            Console.Write("获取单次测量数据");
            err = protocol.GetSingleData(controller_idx, ref data);
            if (IS_ERR_OK(err))
            {
                nread = data.Length;
                Console.WriteLine("数据长度为：{0}", nread);
                for (int i = 0; i < nread; ++i)
                {
                    Console.Write("{0}: {1}\n", i + 1,data[i]);
                }
            }
            else
            {
                Console.WriteLine("错误：{0}", getErrorCodeString(err));
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

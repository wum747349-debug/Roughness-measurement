/*!
* 本样例实现了打开USB通道，并通过串口向下位机发送连接确认指令
*/
using System;
using System.Threading;
namespace tscmcnet
{

	partial class Program
	{
		static void example01()
		{
			//通信类
			TSCMCAPINET protocol = new TSCMCAPINET();
			//控制器编号 
			int controller_idx = 0;
			bool ret = false;
			Console.WriteLine("设置连接通信类型为USB");
			ret = protocol.SetConnectionType(CONNECTION_TYPE.USB);
			//设置USB端口号，可根据设备实际USB端口号修改
			int portCOM = 4;
			protocol.SetUSBPort(portCOM);

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
			BAUDRATE rate = BAUDRATE._57600;
			protocol.SetConfigBaudRate(controller_idx, rate);
			double pos = 0;
			protocol.GetConfigEncoderPosition(controller_idx, ENCODER_CHANNEL.CH1, ref pos);
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

/*
 * motor_io.h
 * 功能：CM35 运动控制器的 IO 逻辑控制 (BSP层)
 * 版权：Antigravity (基于 PROJECT_RULES 规范)
 */
#ifndef __MOTOR_IO_H__
#define __MOTOR_IO_H__

#include "sys.h"

/*
 * 初始化函数
 * 配置 GPIO 输入输出模式，并将输出引脚置为安全状态 (HIGH)
 */
void MotorIO_Init(void);

/*
 * 任务处理函数
 * 建议在主循环中周期性调用 (如 50ms 一次)
 */
void MotorIO_Process(void);

/*
 * 数据投喂接口
 * 供系统层串口中断 (uart1.c) 调用
 */
void MotorIO_FeedByte(uint8_t byte);

/*
 * 强制进入安全态
 * 将所有输出控制位清零，并刷新通信时间戳，避免异常后保持旧状态
 */
void MotorIO_ForceSafeState(void);

#endif

#include "delay.h"
#include "sys.h"
#include "uart1.h"


/*
 * 只需要包含 BSP 头文件，不需要包含具体逻辑
 * 注意：必须在 Keil 中添加 Drivers/BSP/MotorIO 到 Include Path
 */
#include "motor_io.h"

int main(void) {
  /* 1. 基础系统初始化 (HAL) */
  HAL_Init();
  stm32_clock_init(RCC_PLL_MUL9); // 72MHz

  /* 2. BSP/SYSTEM 初始化 */
  uart1_init(115200); // 串口初始化
  MotorIO_Init();     // 电机IO初始化 (含安全复位)

  /* 3. 主循环 */
  while (1) {
    MotorIO_Process(); // 周期性任务 (IO扫描 + 数据发送)
  }
}

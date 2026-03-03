/*
 * motor_io.c
 * 功能实现：CM35 IO 控制与通信协议
 */
#include "motor_io.h"
#include "delay.h" /* 系统层延时 */
#include "uart1.h" /* 系统层串口驱动 */

extern UART_HandleTypeDef uart1_handle;

/* 协议常量 */
#define FRAME_HEAD 0xAA
#define FRAME_TAIL 0x55
#define FUNC_CODE 0x01
#define FRAME_LEN 6

/* 接收缓冲区 */
static uint8_t g_rx_buf[FRAME_LEN];
static uint8_t g_rx_idx = 0;

/*
 * 内部函数：处理接收到的完整数据帧
 */
static void Process_Frame(uint8_t *frame) {
  /* 1. SUM 校验 (前4字节和的低8位) */
  uint32_t sum = frame[0] + frame[1] + frame[2] + frame[3];
  uint8_t calc_sum = (uint8_t)(sum & 0xFF);

  if (calc_sum == frame[4] && frame[5] == FRAME_TAIL) {
    /* 2. 解析控制位图 (Data_In) */
    uint8_t output_map = frame[2];

    /*
     * 3. 硬件控制 (源极驱动 NMOS) - 逻辑取反
     * 协议: 1 = Active (ON)
     * 硬件: Low (0V) = ON  => WritePin(RESET)
     *       High (3.3V) = OFF => WritePin(SET)
     */

    // Bit 0: PB11
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_11,
                      (output_map & 0x01) ? GPIO_PIN_RESET : GPIO_PIN_SET);
    // Bit 1: PB1
    HAL_GPIO_WritePin(GPIOB, GPIO_PIN_1,
                      (output_map & 0x02) ? GPIO_PIN_RESET : GPIO_PIN_SET);
    // Bit 2: PA7
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_7,
                      (output_map & 0x04) ? GPIO_PIN_RESET : GPIO_PIN_SET);
    // Bit 3: PA5
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_5,
                      (output_map & 0x08) ? GPIO_PIN_RESET : GPIO_PIN_SET);
    // Bit 4: PA3
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_3,
                      (output_map & 0x10) ? GPIO_PIN_RESET : GPIO_PIN_SET);
    // Bit 5: PA1
    HAL_GPIO_WritePin(GPIOA, GPIO_PIN_1,
                      (output_map & 0x20) ? GPIO_PIN_RESET : GPIO_PIN_SET);
    // Bit 6: PC15
    HAL_GPIO_WritePin(GPIOC, GPIO_PIN_15,
                      (output_map & 0x40) ? GPIO_PIN_RESET : GPIO_PIN_SET);
    // Bit 7: PC13
    HAL_GPIO_WritePin(GPIOC, GPIO_PIN_13,
                      (output_map & 0x80) ? GPIO_PIN_RESET : GPIO_PIN_SET);
  }
}

/*
 * 公共接口：接收字节投喂 (被 ISR 调用)
 */
void MotorIO_FeedByte(uint8_t byte) {
  if (g_rx_idx == 0) {
    if (byte == FRAME_HEAD)
      g_rx_buf[g_rx_idx++] = byte;
  } else if (g_rx_idx < FRAME_LEN - 1) {
    g_rx_buf[g_rx_idx++] = byte;
  } else if (g_rx_idx == FRAME_LEN - 1) {
    g_rx_buf[g_rx_idx] = byte;
    /* 收到完整帧 (6字节) -> 处理 */
    Process_Frame(g_rx_buf);
    g_rx_idx = 0;
  }
}

/*
 * 初始化函数
 */
void MotorIO_Init(void) {
  GPIO_InitTypeDef GPIO_InitStruct = {0};

  __HAL_RCC_GPIOA_CLK_ENABLE();
  __HAL_RCC_GPIOB_CLK_ENABLE();
  __HAL_RCC_GPIOC_CLK_ENABLE();
  __HAL_RCC_AFIO_CLK_ENABLE(); // AFIO 时钟必须开启

  /*
   * [关键] 禁用 JTAG，仅保留 SWD 调试功能
   * 这样 PB3 (JTDO), PB4 (NJTRST), PA15 (JTDI) 才能作为普通 GPIO 使用
   * 注意：禁用后只能用 SWD 方式烧录程序
   */
  __HAL_AFIO_REMAP_SWJ_NOJTAG();

  /* [安全关键] 配置前先拉高，防止 MOS 误导通 */
  HAL_GPIO_WritePin(GPIOB, GPIO_PIN_1 | GPIO_PIN_11, GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_1 | GPIO_PIN_3 | GPIO_PIN_5 | GPIO_PIN_7,
                    GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOC, GPIO_PIN_13 | GPIO_PIN_15, GPIO_PIN_SET);

  /* 配置输出 (推挽) */
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;

  GPIO_InitStruct.Pin = GPIO_PIN_1 | GPIO_PIN_11;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = GPIO_PIN_1 | GPIO_PIN_3 | GPIO_PIN_5 | GPIO_PIN_7;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = GPIO_PIN_13 | GPIO_PIN_15;
  HAL_GPIO_Init(GPIOC, &GPIO_InitStruct);

  /* 配置输入 (根据原理图，光耦侧可能有外部上拉，使用 INPUT 模式) */
  GPIO_InitStruct.Mode = GPIO_MODE_INPUT;

  GPIO_InitStruct.Pin = GPIO_PIN_12 | GPIO_PIN_14 | GPIO_PIN_3 | GPIO_PIN_5 |
                        GPIO_PIN_7 | GPIO_PIN_9;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = GPIO_PIN_8 | GPIO_PIN_11;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);
}

/*
 * 主循环任务 (50ms)
 */
void MotorIO_Process(void) {
  static uint32_t last_tick = 0;

  if (HAL_GetTick() - last_tick >= 50) {
    last_tick = HAL_GetTick();

    uint8_t input_map = 0;

    /* 读取输入状态 (反向逻辑: RESET=Active) */
    if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_12) == GPIO_PIN_RESET)
      input_map |= (1 << 0);
    if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_14) == GPIO_PIN_RESET)
      input_map |= (1 << 1);
    if (HAL_GPIO_ReadPin(GPIOA, GPIO_PIN_8) == GPIO_PIN_RESET)
      input_map |= (1 << 2);
    if (HAL_GPIO_ReadPin(GPIOA, GPIO_PIN_11) == GPIO_PIN_RESET)
      input_map |= (1 << 3);
    if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_3) == GPIO_PIN_RESET)
      input_map |= (1 << 4);
    if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_5) == GPIO_PIN_RESET)
      input_map |= (1 << 5);
    if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_7) == GPIO_PIN_RESET)
      input_map |= (1 << 6);
    if (HAL_GPIO_ReadPin(GPIOB, GPIO_PIN_9) == GPIO_PIN_RESET)
      input_map |= (1 << 7);

    /* 组包发送 */
    uint8_t tx_frame[6] = {0};
    tx_frame[0] = FRAME_HEAD;
    tx_frame[1] = FUNC_CODE;
    tx_frame[2] = 0x00;      // Data_In 占位
    tx_frame[3] = input_map; // Data_Out 状态

    uint32_t sum = tx_frame[0] + tx_frame[1] + tx_frame[2] + tx_frame[3];
    tx_frame[4] = (uint8_t)(sum & 0xFF);
    tx_frame[5] = FRAME_TAIL;

    HAL_UART_Transmit(&uart1_handle, tx_frame, 6, 10);
  }
}

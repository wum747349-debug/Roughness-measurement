#include "uart1.h"
#include "sys.h"
#include "motor_io.h"


/* 外部引用：应用层回调 */
extern void MotorIO_FeedByte(uint8_t byte);

UART_HandleTypeDef uart1_handle;
uint8_t g_isr_rx_byte; /* 用于中断接收的单字节缓存 */

/* 重定向 fputc */
int fputc(int ch, FILE *f) {
  while ((USART1->SR & 0X40) == 0)
    ;
  USART1->DR = (uint8_t)ch;
  return ch;
}

/**
 * @brief  HAL UART MSP 初始化回调 (由 HAL_UART_Init 自动调用)
 * @note   配置 USART1 使用的 GPIO 和 NVIC
 * @param  huart UART 句柄
 */
void HAL_UART_MspInit(UART_HandleTypeDef *huart) {
  GPIO_InitTypeDef GPIO_InitStruct = {0};

  if (huart->Instance == USART1) {
    /* 1. 使能时钟 */
    __HAL_RCC_USART1_CLK_ENABLE();
    __HAL_RCC_GPIOA_CLK_ENABLE();

    /* 2. 配置 GPIO: PA9 (TX), PA10 (RX) */
    // TX: 复用推挽输出
    GPIO_InitStruct.Pin = GPIO_PIN_9;
    GPIO_InitStruct.Mode = GPIO_MODE_AF_PP;
    GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_HIGH;
    HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

    // RX: 浮空输入 (或上拉输入)
    GPIO_InitStruct.Pin = GPIO_PIN_10;
    GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
    GPIO_InitStruct.Pull = GPIO_NOPULL; // 硬件已有上拉可改为 GPIO_PULLUP
    HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

    /* 3. 配置 NVIC 中断 */
    HAL_NVIC_SetPriority(USART1_IRQn, 0, 0);
    HAL_NVIC_EnableIRQ(USART1_IRQn);
  }
}

/* 串口初始化 */
void uart1_init(uint32_t bound) {
  uart1_handle.Instance = USART1;
  uart1_handle.Init.BaudRate = bound;
  uart1_handle.Init.WordLength = UART_WORDLENGTH_8B;
  uart1_handle.Init.StopBits = UART_STOPBITS_1;
  uart1_handle.Init.Parity = UART_PARITY_NONE;
  uart1_handle.Init.Mode = UART_MODE_TX_RX;
  uart1_handle.Init.HwFlowCtl = UART_HWCONTROL_NONE;
  uart1_handle.Init.OverSampling = UART_OVERSAMPLING_16;

  HAL_UART_Init(&uart1_handle);

  /* 开启接收中断 */
  HAL_UART_Receive_IT(&uart1_handle, &g_isr_rx_byte, 1);
}

/* 中断入口 */
void USART1_IRQHandler(void) { HAL_UART_IRQHandler(&uart1_handle); }

/* HAL 库接收回调 */
void HAL_UART_RxCpltCallback(UART_HandleTypeDef *huart) {
  if (huart->Instance == USART1) {
    /* 将数据喂给应用层 */
    MotorIO_FeedByte(g_isr_rx_byte);

    /* 重新开启中断 */
    HAL_UART_Receive_IT(&uart1_handle, &g_isr_rx_byte, 1);
  }
}

/* HAL 库串口错误回调：发生噪声/帧错误/溢出时立即进入安全态并恢复接收 */
void HAL_UART_ErrorCallback(UART_HandleTypeDef *huart) {
  if (huart->Instance == USART1) {
    MotorIO_ForceSafeState();
    __HAL_UART_CLEAR_PEFLAG(huart);
    __HAL_UART_CLEAR_FEFLAG(huart);
    __HAL_UART_CLEAR_NEFLAG(huart);
    __HAL_UART_CLEAR_OREFLAG(huart);
    HAL_UART_AbortReceive(huart);
    HAL_UART_Receive_IT(&uart1_handle, &g_isr_rx_byte, 1);
  }
}

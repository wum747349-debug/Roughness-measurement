/*
 * motor_io.c
 * 功能实现：CM35 IO 控制与通信协议
 */
#include "motor_io.h"
#include "delay.h"
#include "uart1.h"

extern UART_HandleTypeDef uart1_handle;

#define FRAME_HEAD 0xAA
#define FRAME_TAIL 0x55
#define FUNC_CODE 0x01
#define FRAME_LEN 6
#define COMM_TIMEOUT_MS 500

static uint8_t g_rx_buf[FRAME_LEN];
static uint8_t g_rx_idx = 0;

/* ISR 中仅设置命令，真正 GPIO 输出放到主循环执行 */
static volatile uint8_t g_pending_output_map = 0;
static volatile uint8_t g_pending_output_valid = 0;
static volatile uint32_t g_last_cmd_tick = 0;

static void Apply_OutputMap(uint8_t output_map) {
  HAL_GPIO_WritePin(GPIOB, GPIO_PIN_11,
                    (output_map & 0x01) ? GPIO_PIN_RESET : GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOB, GPIO_PIN_1,
                    (output_map & 0x02) ? GPIO_PIN_RESET : GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_7,
                    (output_map & 0x04) ? GPIO_PIN_RESET : GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_5,
                    (output_map & 0x08) ? GPIO_PIN_RESET : GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_3,
                    (output_map & 0x10) ? GPIO_PIN_RESET : GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_1,
                    (output_map & 0x20) ? GPIO_PIN_RESET : GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOC, GPIO_PIN_15,
                    (output_map & 0x40) ? GPIO_PIN_RESET : GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOC, GPIO_PIN_13,
                    (output_map & 0x80) ? GPIO_PIN_RESET : GPIO_PIN_SET);
}

static void Process_Frame(uint8_t *frame) {
  uint32_t sum = frame[0] + frame[1] + frame[2] + frame[3];
  uint8_t calc_sum = (uint8_t)(sum & 0xFF);

  if (calc_sum == frame[4] && frame[5] == FRAME_TAIL) {
    g_pending_output_map = frame[2];
    g_pending_output_valid = 1;
    g_last_cmd_tick = HAL_GetTick();
  }
}

void MotorIO_FeedByte(uint8_t byte) {
  if (g_rx_idx == 0) {
    if (byte == FRAME_HEAD) {
      g_rx_buf[g_rx_idx++] = byte;
    }
  } else if (g_rx_idx < FRAME_LEN - 1) {
    g_rx_buf[g_rx_idx++] = byte;
  } else {
    g_rx_buf[g_rx_idx] = byte;
    Process_Frame(g_rx_buf);
    g_rx_idx = 0;
  }
}

void MotorIO_ForceSafeState(void) {
  g_pending_output_map = 0x00;
  g_pending_output_valid = 1;
  g_last_cmd_tick = HAL_GetTick();
}

void MotorIO_Init(void) {
  GPIO_InitTypeDef GPIO_InitStruct = {0};

  __HAL_RCC_GPIOA_CLK_ENABLE();
  __HAL_RCC_GPIOB_CLK_ENABLE();
  __HAL_RCC_GPIOC_CLK_ENABLE();
  __HAL_RCC_AFIO_CLK_ENABLE();

  __HAL_AFIO_REMAP_SWJ_NOJTAG();

  HAL_GPIO_WritePin(GPIOB, GPIO_PIN_1 | GPIO_PIN_11, GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_1 | GPIO_PIN_3 | GPIO_PIN_5 | GPIO_PIN_7,
                    GPIO_PIN_SET);
  HAL_GPIO_WritePin(GPIOC, GPIO_PIN_13 | GPIO_PIN_15, GPIO_PIN_SET);

  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;

  GPIO_InitStruct.Pin = GPIO_PIN_1 | GPIO_PIN_11;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = GPIO_PIN_1 | GPIO_PIN_3 | GPIO_PIN_5 | GPIO_PIN_7;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = GPIO_PIN_13 | GPIO_PIN_15;
  HAL_GPIO_Init(GPIOC, &GPIO_InitStruct);

  GPIO_InitStruct.Mode = GPIO_MODE_INPUT;

  GPIO_InitStruct.Pin = GPIO_PIN_12 | GPIO_PIN_14 | GPIO_PIN_3 | GPIO_PIN_5 |
                        GPIO_PIN_7 | GPIO_PIN_9;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = GPIO_PIN_8 | GPIO_PIN_11;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  g_pending_output_map = 0;
  g_pending_output_valid = 1;
  g_last_cmd_tick = HAL_GetTick();
}

void MotorIO_Process(void) {
  static uint32_t last_tick = 0;

  if (HAL_GetTick() - last_tick < 50) {
    return;
  }
  last_tick = HAL_GetTick();

  if (g_pending_output_valid) {
    uint8_t map = g_pending_output_map;
    g_pending_output_valid = 0;
    Apply_OutputMap(map);
  }

  if ((HAL_GetTick() - g_last_cmd_tick) > COMM_TIMEOUT_MS) {
    Apply_OutputMap(0x00);
  }

  uint8_t input_map = 0;

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

  uint8_t tx_frame[FRAME_LEN] = {0};
  tx_frame[0] = FRAME_HEAD;
  tx_frame[1] = FUNC_CODE;
  tx_frame[2] = 0x00;
  tx_frame[3] = input_map;

  uint32_t sum = tx_frame[0] + tx_frame[1] + tx_frame[2] + tx_frame[3];
  tx_frame[4] = (uint8_t)(sum & 0xFF);
  tx_frame[5] = FRAME_TAIL;

  HAL_UART_Transmit(&uart1_handle, tx_frame, FRAME_LEN, 10);
}

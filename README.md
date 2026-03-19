# 粗糙度测量系统（Roughness Measurement System）

本项目是一个软硬件协同的工业级非接触粗糙度测量系统，采用 Monorepo 结构，覆盖：
- 下位机（STM32F103C8T6）：24V 工业 I/O 桥接、串口协议、状态回传
- 上位机（C# WinForms）：设备通信、扫描流程、同步控制、粗糙度算法、SQLite 持久化

目标是在复杂微观表面（如缸孔内壁、小口径容器、交叉纹理）实现稳定的非接触粗糙度评定。

---

## 1. 当前项目状态（截至目前）

系统已完成端到端闭环验证，已跑通：
- 电机控制器程序触发与回位闭环
- 光谱共焦采集与电机扫描同步
- 粗糙度参数计算（Ra / Rz / Rk / Mr1 / Mr2 / Rpk / Rvk）
- SQLite 记录保存与历史回放

已落地的重要工程改进：
- 串口帧解析稳健性增强（粘包/半包处理）
- IO 点击后立即下发（不再依赖慢周期）
- 上下位机“断链回安全态”双保险
- 主界面增加 MCU 通信状态可见性（通信正常/超时）
- 支持“无传感器联调模式”（可先调电机与握手）
- IO 控制窗口中文显示修复，输入标签改为 IN11~IN18

---

## 2. 仓库结构

```text
Roughness-measurement/
├─ Firmware/                         # STM32 固件
│  ├─ Drivers/
│  │  ├─ BSP/MotorIO/                # 电机控制器 IO 逻辑
│  │  └─ SYSTEM/uart1/               # UART1 初始化与中断回调
│  └─ Users/                         # 主循环入口
├─ Software/                         # C# WinForms 上位机
│  ├─ Presentation/Forms/            # 主界面、IO控制、历史记录等
│  ├─ Infrastructure/Communication/  # MCU/传感器通信
│  ├─ Infrastructure/Data/           # SQLite 数据服务
│  ├─ Domain/Algorithms/             # 去趋势与粗糙度算法
│  └─ ConfocalMeter.sln
└─ README.md
```

---

## 3. 系统握手协议（当前版本）

### 3.1 上位机 -> 控制器（通过 STM32 转发）
- `IN11`：启动请求（Start Request）

### 3.2 控制器 -> 上位机（状态回读）
- `OUT1`：`BUSY`（运行中）
- `OUT2`：`SYNC`（扫描起点同步）
- `OUT3`：`DONE`（扫描段完成）
- `OUT4`：`FAULT`（故障，可选）

### 3.3 协议位映射（上位机帧）
- `Data1 bit0` -> 控制器 `IN11`
- `Data2 bit0` <- 控制器 `OUT1(BUSY)`
- `Data2 bit1` <- 控制器 `OUT2(SYNC)`
- `Data2 bit2` <- 控制器 `OUT3(DONE)`
- `Data2 bit3` <- 控制器 `OUT4(FAULT)`

---

## 4. 扫描流程（上位机状态机）

1. 点击开始扫描，置 `IN11=通`
2. 等待 `BUSY=1`
3. 等待 `SYNC=1`
4. 开始正式采集
5. 收到 `DONE=1` 立即停采
6. 等待 `BUSY=0` 判定回位完成
7. 释放 `IN11=断`
8. 执行去趋势与参数计算（若联调模式则跳过）

---

## 5. 超时参数（当前建议）

`expectedScanMs = L / (Vscan / 60) * 1000`  
（L: 扫描长度 mm，Vscan: 速度 mm/min）

- `waitBusyTimeoutMs = 1000`
- `waitSyncTimeoutMs = 1000`
- `doneTimeoutMs = expectedScanMs * 2 + 1000`
- `returnTimeoutMs = expectedScanMs * 2 + 2000`

说明：若控制器与上位机工艺参数不一致，`expectedScanMs` 计算会失真，建议先对齐参数再做收敛。

---

## 6. 无传感器联调模式

为保护光谱共焦传感器，可在未连接传感器时先做电机联调：
- 允许执行完整握手（IN11/BUSY/SYNC/DONE/回位）
- 输出时序日志 `t0~t4`、`T_busy/T_sync/T_scan/T_return/T_total`
- 跳过粗糙度计算与数据入库

---

## 7. 安全策略（失联默认安全态）

### 上位机
- 断开串口前主动发送清零命令（`InputMap=0`）
- 重连默认清零输入请求

### 固件
- 通信超时自动清零输出
- UART 错误回调中立即强制安全态并恢复接收中断

目标：通信中断后控制输入回到默认断开，避免误动作。

---

## 8. 历史回放说明

历史回放为保证流畅性使用降采样绘图（min/max bucket）。  
因此历史图与主界面“原始轮廓”在视觉上可能不完全一致，但关键峰谷特征保留。

---

## 9. 开发与调试建议

1. 先验证 `IO控制` 页面是否持续有 `RX` 帧
2. 再做握手联调（BUSY/SYNC/DONE）
3. 最后接入共焦传感器做同步采集与粗糙度计算
4. 每次改动后关注主界面 MCU 状态：`已打开(待通信) / 通信正常 / 通信超时`

---

## 10. 已知后续优化方向

- 毫秒级同步标定（SYNC 边沿精度）
- 历史回放全分辨率对照模式
- 工艺参数模板化（不同工件自动加载长度/速度/超时）
- 结构化日志与长期追溯能力增强


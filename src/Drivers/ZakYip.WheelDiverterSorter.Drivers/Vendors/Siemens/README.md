# S7-1200/1500 PLC 驱动程序

本文件夹包含用于西门子S7-1200/1500 PLC的驱动程序实现。

## 功能特性

- ✅ **S7协议通信**: 使用S7.Net.Plus库实现以太网通信
- ✅ **多CPU支持**: 支持S7-1200、S7-1500、S7-300、S7-400系列
- ✅ **数据块操作**: 支持DB数据块的位读写操作
- ✅ **自动重连**: 内置连接管理和自动重连机制
- ✅ **异常处理**: 完善的异常处理和日志记录
- ✅ **批量操作**: 支持批量读写IO端口

## 技术栈

- **S7.Net.Plus**: 开源、活跃维护的S7协议库
- **版本**: 0.20.0
- **协议**: S7 over TCP/IP

## 配置说明

### 配置示例

在 `appsettings.json` 中添加S7配置：

```json
{
  "Driver": {
    "UseHardwareDriver": true,
    "Vendor": "Siemens",
    "S7": {
      "IpAddress": "192.168.0.100",
      "Rack": 0,
      "Slot": 1,
      "CpuType": "S71200",
      "ConnectionTimeout": 5000,
      "ReadWriteTimeout": 2000,
      "MaxReconnectAttempts": 3,
      "ReconnectDelay": 1000,
      "Diverters": [
        {
          "DiverterId": "D1",
          "OutputDbNumber": 1,
          "OutputStartByte": 0,
          "OutputStartBit": 0,
          "FeedbackInputDbNumber": 2,
          "FeedbackInputByte": 0,
          "FeedbackInputBit": 0
        },
        {
          "DiverterId": "D2",
          "OutputDbNumber": 1,
          "OutputStartByte": 0,
          "OutputStartBit": 2,
          "FeedbackInputDbNumber": 2,
          "FeedbackInputByte": 0,
          "FeedbackInputBit": 1
        },
        {
          "DiverterId": "D3",
          "OutputDbNumber": 1,
          "OutputStartByte": 0,
          "OutputStartBit": 4,
          "FeedbackInputDbNumber": 2,
          "FeedbackInputByte": 0,
          "FeedbackInputBit": 2
        }
      ]
    }
  }
}
```

### 配置参数说明

#### S7Options

| 参数 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| IpAddress | string | PLC IP地址 | 192.168.0.1 |
| Rack | short | 机架号 | 0 |
| Slot | short | 插槽号 | 1 |
| CpuType | enum | CPU类型 | S71200 |
| ConnectionTimeout | int | 连接超时时间(ms) | 5000 |
| ReadWriteTimeout | int | 读写超时时间(ms) | 2000 |
| MaxReconnectAttempts | int | 最大重连次数 | 3 |
| ReconnectDelay | int | 重连延迟(ms) | 1000 |
| Diverters | List | 摆轮配置列表 | [] |

#### S7CpuType 枚举

- `S71200`: S7-1200系列PLC
- `S71500`: S7-1500系列PLC
- `S7300`: S7-300系列PLC
- `S7400`: S7-400系列PLC

#### S7DiverterConfigDto

| 参数 | 类型 | 说明 | 必填 |
|------|------|------|------|
| DiverterId | string | 摆轮ID | 是 |
| OutputDbNumber | int | 输出DB块号 | 是 |
| OutputStartByte | int | 输出起始字节 | 是 |
| OutputStartBit | int | 输出起始位(0-7) | 是 |
| FeedbackInputDbNumber | int? | 反馈输入DB块号 | 否 |
| FeedbackInputByte | int? | 反馈输入字节 | 否 |
| FeedbackInputBit | int? | 反馈输入位(0-7) | 否 |

## PLC配置要求

### 网络配置

1. PLC必须配置为允许PUT/GET通信
2. 在TIA Portal中设置PLC的IP地址
3. 确保防火墙允许102端口（S7协议默认端口）

### 数据块配置

1. 创建全局数据块（Global DB）用于输入输出
2. 建议创建两个DB：
   - DB1: 输出数据块（控制摆轮）
   - DB2: 输入数据块（读取传感器）
3. 禁用"优化块访问"选项（Optimized block access），以便进行绝对地址访问

### 示例数据块结构

**DB1 (输出):**
```
DB1.DBX0.0  - D1摆轮控制位0
DB1.DBX0.1  - D1摆轮控制位1
DB1.DBX0.2  - D2摆轮控制位0
DB1.DBX0.3  - D2摆轮控制位1
DB1.DBX0.4  - D3摆轮控制位0
DB1.DBX0.5  - D3摆轮控制位1
```

**DB2 (输入):**
```
DB2.DBX0.0  - D1摆轮反馈
DB2.DBX0.1  - D2摆轮反馈
DB2.DBX0.2  - D3摆轮反馈
```

## 角度编码

摆轮使用2个位进行二进制编码，支持4种角度：

| 角度 | Bit1 | Bit0 | 说明 |
|------|------|------|------|
| 0°   | 0    | 0    | 直行 |
| 30°  | 0    | 1    | 小角度转向 |
| 45°  | 1    | 0    | 中等角度转向 |
| 90°  | 1    | 1    | 大角度转向 |

## 连接管理

### 自动重连机制

1. 检测到连接断开时自动触发重连
2. 可配置最大重连次数和重连延迟
3. 重连失败后会记录错误日志
4. 支持取消令牌，可中断重连过程

### 线程安全

- 使用信号量（SemaphoreSlim）保证连接操作的线程安全
- 所有IO操作都经过连接检查
- 支持并发读写操作

## 性能特性

- **异步操作**: 所有IO操作都是异步的，不阻塞线程
- **连接复用**: 单个连接实例在整个应用生命周期内复用
- **批量操作**: 支持批量读写以提高效率
- **超时控制**: 可配置连接和读写超时时间

## 错误处理

### 常见错误及解决方案

#### 连接失败

**可能原因:**
- PLC IP地址配置错误
- 网络不通
- PLC未开启PUT/GET通信
- 防火墙阻止102端口

**解决方案:**
1. 使用ping命令测试网络连通性
2. 检查TIA Portal中的连接机制设置
3. 确认Rack和Slot配置正确
4. 检查防火墙规则

#### 读写失败

**可能原因:**
- 数据块不存在
- 地址超出范围
- 数据块开启了"优化块访问"
- PLC处于STOP模式

**解决方案:**
1. 在TIA Portal中验证DB块存在
2. 检查字节和位地址配置
3. 禁用数据块的"优化块访问"选项
4. 确保PLC处于RUN模式

#### 间歇性连接问题

**可能原因:**
- 网络不稳定
- PLC负载过高
- 其他设备占用连接资源

**解决方案:**
1. 增加重连次数和延迟时间
2. 检查PLC性能指标
3. 减少并发连接数

## 开发与调试

### 日志级别建议

- **生产环境**: Information
- **测试环境**: Debug
- **开发环境**: Trace

### 使用模拟器测试

开发阶段可以使用S7 Simulator进行测试：
1. 安装Snap7 Server模拟器
2. 配置模拟器监听地址和端口
3. 在配置中指向模拟器地址

### 单元测试

项目包含完整的单元测试，使用Mock对象模拟S7设备：
```bash
dotnet test ZakYip.WheelDiverterSorter.Drivers.Tests
```

## 参考资源

- [S7.Net.Plus GitHub](https://github.com/S7NetPlus/s7netplus)
- [S7.Net.Plus文档](https://s7netplus.github.io/s7netplus/)
- [Siemens S7协议文档](https://support.industry.siemens.com/)
- [TIA Portal使用手册](https://support.industry.siemens.com/cs/document/109742691)

## 许可证

本驱动程序依赖S7.Net.Plus库，该库采用MIT许可证。

## 技术支持

如遇问题，请提交Issue或联系开发团队。

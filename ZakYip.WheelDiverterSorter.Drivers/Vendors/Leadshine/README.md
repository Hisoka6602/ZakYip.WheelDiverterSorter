# 雷赛（Leadshine）驱动器

本目录包含用于控制雷赛（Leadshine）运动控制器的驱动程序和接口。

## 文件说明

- **LTDMC.cs**: 雷赛LTDMC控制器的P/Invoke声明文件
- **LTDMC.dll**: 雷赛LTDMC控制器的原生DLL库
- **LeadshineDiverterController.cs**: 基于雷赛控制器的摆轮控制器实现

## 使用方法

### 1. 初始化控制器

```csharp
var config = new LeadshineDiverterConfig
{
    DiverterId = "D1",
    OutputStartBit = 0,  // 使用输出位0和1控制摆轮
    FeedbackInputBit = 10  // 可选：使用输入位10读取反馈
};

var controller = new LeadshineDiverterController(logger, cardNo: 0, config);
```

### 2. 设置摆轮角度

```csharp
// 设置摆轮到30度
bool success = await controller.SetAngleAsync(30);

// 支持的角度：0, 30, 45, 90
```

### 3. 复位摆轮

```csharp
// 复位到0度
bool success = await controller.ResetAsync();
```

## 角度编码

摆轮角度通过2个输出位进行二进制编码：

| 角度 | Bit1 | Bit0 |
|------|------|------|
| 0°   | 0    | 0    |
| 30°  | 0    | 1    |
| 45°  | 1    | 0    |
| 90°  | 1    | 1    |

## 配置说明

- **OutputStartBit**: 控制摆轮的起始输出位索引，需要连续2个位
- **FeedbackInputBit**: （可选）用于读取摆轮状态反馈的输入位索引

## 硬件要求

- 雷赛LTDMC系列运动控制卡
- Windows操作系统（LTDMC.dll仅支持Windows）
- 正确配置的IO端口连接

## 参考

本实现参考了 [ZakYip.Singulation](https://github.com/Hisoka6602/ZakYip.Singulation) 项目的IO控制逻辑。

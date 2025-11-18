#!/bin/bash
# dotnet-trace 采样脚本 (Linux/Mac 版本)
# 用于对运行中的.NET应用程序进行性能采样

# 默认参数
DURATION=30
PROFILE="cpu-sampling"
OUTPUT_PATH="trace-$(date +%Y%m%d-%H%M%S).nettrace"

# 显示使用说明
usage() {
    echo "======================================"
    echo "dotnet-trace 性能采样工具"
    echo "======================================"
    echo ""
    echo "用法: $0 -p <process_id> [-d duration] [-o output] [-f profile]"
    echo ""
    echo "参数:"
    echo "  -p    目标进程 PID (必需)"
    echo "  -d    采样时长（秒），默认 30"
    echo "  -o    输出文件路径"
    echo "  -f    采样配置: cpu-sampling (默认), gc-verbose, gc-collect"
    echo "  -h    显示帮助信息"
    echo ""
    echo "示例:"
    echo "  $0 -p 12345"
    echo "  $0 -p 12345 -d 60 -f gc-verbose"
    exit 1
}

# 解析参数
while getopts "p:d:o:f:h" opt; do
    case $opt in
        p) PROCESS_ID=$OPTARG ;;
        d) DURATION=$OPTARG ;;
        o) OUTPUT_PATH=$OPTARG ;;
        f) PROFILE=$OPTARG ;;
        h) usage ;;
        *) usage ;;
    esac
done

# 检查必需参数
if [ -z "$PROCESS_ID" ]; then
    echo "错误: 必须指定进程 PID"
    usage
fi

echo "======================================"
echo "dotnet-trace 性能采样工具"
echo "======================================"
echo ""
echo "配置信息:"
echo "  进程ID: $PROCESS_ID"
echo "  采样时长: $DURATION 秒"
echo "  输出文件: $OUTPUT_PATH"
echo "  采样配置: $PROFILE"
echo ""

# 检查是否安装了 dotnet-trace
if ! command -v dotnet-trace &> /dev/null; then
    echo "未检测到 dotnet-trace 工具，正在安装..."
    dotnet tool install --global dotnet-trace
    if [ $? -ne 0 ]; then
        echo "安装 dotnet-trace 失败"
        exit 1
    fi
    # 添加到 PATH
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# 检查进程是否存在
if ! ps -p $PROCESS_ID > /dev/null 2>&1; then
    echo "错误: 进程 $PROCESS_ID 不存在"
    exit 1
fi

PROCESS_NAME=$(ps -p $PROCESS_ID -o comm=)
echo "开始采样进程: $PROCESS_NAME (PID: $PROCESS_ID)"
echo "采样进行中，请等待 $DURATION 秒..."
echo ""

# 执行采样
dotnet-trace collect --process-id $PROCESS_ID --duration ${DURATION}s --output $OUTPUT_PATH --profile $PROFILE

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ 采样完成!"
    echo "追踪文件已保存至: $OUTPUT_PATH"
    echo ""
    echo "分析方式:"
    echo "  1. 使用 PerfView (Windows): 打开 .nettrace 文件"
    echo "  2. 使用 Visual Studio: 文件 -> 打开 -> 文件 -> 选择 .nettrace"
    echo "  3. 使用 speedscope (跨平台): https://www.speedscope.app/"
    echo ""
    
    # 显示文件大小
    FILE_SIZE=$(du -h "$OUTPUT_PATH" | cut -f1)
    echo "追踪文件大小: $FILE_SIZE"
else
    echo ""
    echo "✗ 采样失败"
    exit 1
fi

#!/bin/bash
# dotnet-counters 实时监控脚本 (Linux/Mac 版本)
# 用于实时监控.NET应用程序的性能指标

# 默认参数
REFRESH_INTERVAL=1
OUTPUT_FILE=""
MODE="default"

# 显示使用说明
usage() {
    echo "======================================"
    echo "dotnet-counters 实时监控工具"
    echo "======================================"
    echo ""
    echo "用法: $0 -p <process_id> [-r interval] [-o output] [-m mode]"
    echo ""
    echo "参数:"
    echo "  -p    目标进程 PID (必需)"
    echo "  -r    刷新间隔（秒），默认 1"
    echo "  -o    输出到 CSV 文件"
    echo "  -m    监控模式: default, gc, cpu, memory"
    echo "  -h    显示帮助信息"
    echo ""
    echo "示例:"
    echo "  $0 -p 12345"
    echo "  $0 -p 12345 -m gc"
    echo "  $0 -p 12345 -o metrics.csv"
    exit 1
}

# 解析参数
while getopts "p:r:o:m:h" opt; do
    case $opt in
        p) PROCESS_ID=$OPTARG ;;
        r) REFRESH_INTERVAL=$OPTARG ;;
        o) OUTPUT_FILE=$OPTARG ;;
        m) MODE=$OPTARG ;;
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
echo "dotnet-counters 实时监控工具"
echo "======================================"
echo ""

# 检查是否安装了 dotnet-counters
if ! command -v dotnet-counters &> /dev/null; then
    echo "未检测到 dotnet-counters 工具，正在安装..."
    dotnet tool install --global dotnet-counters
    if [ $? -ne 0 ]; then
        echo "安装 dotnet-counters 失败"
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
echo "监控进程: $PROCESS_NAME (PID: $PROCESS_ID)"
echo "刷新间隔: $REFRESH_INTERVAL 秒"
echo ""

# 根据模式选择计数器
case $MODE in
    gc)
        COUNTERS="System.Runtime[gen-0-gc-count],System.Runtime[gen-1-gc-count],System.Runtime[gen-2-gc-count],System.Runtime[gen-0-size],System.Runtime[gen-1-size],System.Runtime[gen-2-size],System.Runtime[loh-size],System.Runtime[alloc-rate],System.Runtime[gc-heap-size],System.Runtime[time-in-gc]"
        echo "监控模式: GC 指标"
        ;;
    cpu)
        COUNTERS="System.Runtime[cpu-usage],System.Runtime[threadpool-thread-count],System.Runtime[monitor-lock-contention-count],System.Runtime[threadpool-queue-length],System.Runtime[threadpool-completed-items-count]"
        echo "监控模式: CPU 和线程池指标"
        ;;
    memory)
        COUNTERS="System.Runtime[working-set],System.Runtime[gc-heap-size],System.Runtime[alloc-rate],System.Runtime[gen-0-size],System.Runtime[gen-1-size],System.Runtime[gen-2-size],System.Runtime[loh-size]"
        echo "监控模式: 内存指标"
        ;;
    *)
        COUNTERS="System.Runtime[cpu-usage],System.Runtime[working-set],System.Runtime[gc-heap-size],System.Runtime[gen-0-gc-count],System.Runtime[gen-1-gc-count],System.Runtime[gen-2-gc-count],System.Runtime[alloc-rate],System.Runtime[threadpool-thread-count],System.Runtime[threadpool-queue-length],System.Runtime[exception-count],System.Runtime[time-in-gc],System.Runtime[monitor-lock-contention-count]"
        echo "监控模式: 综合指标"
        ;;
esac

echo ""
echo "按 Ctrl+C 停止监控"
echo "======================================"
echo ""

# 构建命令
CMD="dotnet-counters monitor --process-id $PROCESS_ID --refresh-interval $REFRESH_INTERVAL --counters $COUNTERS"

if [ -n "$OUTPUT_FILE" ]; then
    CMD="$CMD --output $OUTPUT_FILE"
    echo "输出将保存到: $OUTPUT_FILE"
fi

# 执行监控
$CMD

if [ -n "$OUTPUT_FILE" ] && [ -f "$OUTPUT_FILE" ]; then
    echo ""
    echo "✓ 监控数据已保存至: $OUTPUT_FILE"
fi

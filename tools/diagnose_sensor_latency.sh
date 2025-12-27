#!/bin/bash
# 传感器延迟诊断脚本
# 用于快速诊断包裹到达传感器但程序未及时检测的问题

echo "===================================================="
echo "传感器延迟诊断工具"
echo "===================================================="
echo ""

DB_PATH="${1:-wheeldivertersorter.db}"

if [ ! -f "$DB_PATH" ]; then
    echo "错误：数据库文件不存在: $DB_PATH"
    echo "用法: $0 [数据库路径]"
    exit 1
fi

# 1. 检查传感器轮询间隔
echo "【1. 传感器轮询间隔配置】"
echo "传感器轮询间隔直接影响检测延迟。建议值：5-10ms"
echo "-----------------------------------------------"
sqlite3 "$DB_PATH" <<EOF
.mode column
.headers on
SELECT 
    SensorId,
    VendorName,
    PollingIntervalMs,
    CASE 
        WHEN PollingIntervalMs <= 5 THEN '✓ 优秀'
        WHEN PollingIntervalMs <= 10 THEN '✓ 良好'
        WHEN PollingIntervalMs <= 20 THEN '⚠ 可优化'
        ELSE '✗ 太慢'
    END as 评估
FROM SensorVendorConfiguration;
EOF
echo ""

# 2. 检查状态变化忽略窗口
echo "【2. 状态变化忽略窗口配置】"
echo "过大的忽略窗口会导致包裹信号被忽略。建议值：0-100ms"
echo "-----------------------------------------------"
sqlite3 "$DB_PATH" <<EOF
.mode column
.headers on
SELECT 
    SensorId,
    StateChangeIgnoreWindowMs,
    CASE 
        WHEN StateChangeIgnoreWindowMs = 0 THEN '✓ 已禁用'
        WHEN StateChangeIgnoreWindowMs <= 100 THEN '✓ 合理'
        WHEN StateChangeIgnoreWindowMs <= 500 THEN '⚠ 较大'
        ELSE '✗ 过大'
    END as 评估
FROM SensorVendorConfiguration;
EOF
echo ""

# 3. 检查提前触发检测开关
echo "【3. 提前触发检测和超时检测开关】"
echo "如果启用了检测但配置不当，可能导致正常包裹被判定为干扰"
echo "-----------------------------------------------"
sqlite3 "$DB_PATH" <<EOF
.mode column
.headers on
SELECT 
    EnableEarlyTriggerDetection as 提前触发检测,
    EnableTimeoutDetection as 超时检测,
    CASE 
        WHEN EnableEarlyTriggerDetection = 0 AND EnableTimeoutDetection = 0 THEN '✓ 都已禁用'
        WHEN EnableEarlyTriggerDetection = 1 AND EnableTimeoutDetection = 0 THEN '⚠ 仅启用提前触发检测'
        WHEN EnableEarlyTriggerDetection = 0 AND EnableTimeoutDetection = 1 THEN '⚠ 仅启用超时检测'
        ELSE '⚠ 都已启用'
    END as 状态
FROM SystemConfiguration;
EOF
echo ""

# 4. 检查输送段容差配置
echo "【4. 输送段容差配置】"
echo "容差过小会导致正常包裹被判定为提前触发"
echo "建议：TimeToleranceMs >= 理论传输时间 × 0.5"
echo "-----------------------------------------------"
sqlite3 "$DB_PATH" <<EOF
.mode column
.headers on
SELECT 
    SegmentId,
    LengthMm as '长度(mm)',
    SpeedMmps as '速度(mm/s)',
    ROUND(LengthMm * 1.0 / SpeedMmps * 1000, 0) as '理论传输时间(ms)',
    TimeToleranceMs as '容差(ms)',
    ROUND(TimeToleranceMs * 1.0 / (LengthMm * 1.0 / SpeedMmps * 1000), 2) as '容差比例',
    CASE 
        WHEN TimeToleranceMs >= LengthMm * 1.0 / SpeedMmps * 1000 * 0.5 THEN '✓ 合理'
        WHEN TimeToleranceMs >= LengthMm * 1.0 / SpeedMmps * 1000 * 0.3 THEN '⚠ 偏小'
        ELSE '✗ 过小'
    END as 评估
FROM ConveyorSegmentConfiguration
WHERE SegmentId NOT IN (0, 999);
EOF
echo ""

# 5. 检查传感器类型分布
echo "【5. 传感器类型分布】"
echo "确保只有一个ParcelCreation类型的传感器"
echo "-----------------------------------------------"
sqlite3 "$DB_PATH" <<EOF
.mode column
.headers on
SELECT 
    SensorType,
    COUNT(*) as 数量,
    CASE SensorType
        WHEN 0 THEN 'ParcelCreation(入口传感器)'
        WHEN 1 THEN 'WheelFront(摆轮前传感器)'
        WHEN 2 THEN 'ChuteLock(落格传感器)'
        ELSE '未知类型'
    END as 说明
FROM SensorConfiguration
GROUP BY SensorType;
EOF
echo ""

# 6. 检查日志文件（如果存在）
if [ -d "logs" ]; then
    echo "【6. 日志分析（最近1小时）】"
    echo "-----------------------------------------------"
    
    # 查找最新的日志文件
    LATEST_LOG=$(find logs -name "*.log" -type f -mmin -60 | head -1)
    
    if [ -n "$LATEST_LOG" ]; then
        echo "分析日志文件: $LATEST_LOG"
        echo ""
        
        # 统计提前触发事件
        EARLY_TRIGGER_COUNT=$(grep -c "提前触发检测" "$LATEST_LOG" 2>/dev/null || echo "0")
        echo "提前触发事件数: $EARLY_TRIGGER_COUNT"
        
        # 统计干扰信号
        INTERFERENCE_COUNT=$(grep -c "干扰信号" "$LATEST_LOG" 2>/dev/null || echo "0")
        echo "干扰信号数: $INTERFERENCE_COUNT"
        
        # 统计队列为空
        EMPTY_QUEUE_COUNT=$(grep -c "队列为空" "$LATEST_LOG" 2>/dev/null || echo "0")
        echo "队列为空事件数: $EMPTY_QUEUE_COUNT"
        
        echo ""
        
        if [ "$EARLY_TRIGGER_COUNT" -gt 10 ]; then
            echo "⚠ 警告：提前触发事件过多！"
            echo "建议：检查TimeToleranceMs配置或禁用EnableEarlyTriggerDetection"
            echo ""
            echo "最近5条提前触发事件："
            grep "提前触发检测" "$LATEST_LOG" | tail -5
            echo ""
        fi
        
        if [ "$INTERFERENCE_COUNT" -gt 10 ]; then
            echo "⚠ 警告：干扰信号过多！"
            echo "建议：检查是否有残留包裹，或队列管理逻辑是否正确"
            echo ""
            echo "最近5条干扰信号："
            grep "干扰信号" "$LATEST_LOG" | tail -5
            echo ""
        fi
    else
        echo "未找到最近1小时内的日志文件"
        echo ""
    fi
else
    echo "【6. 日志分析】"
    echo "-----------------------------------------------"
    echo "未找到logs目录，跳过日志分析"
    echo ""
fi

# 7. 诊断总结和建议
echo "【7. 诊断总结和建议】"
echo "===================================================="
echo ""

# 检查是否存在明显问题
ISSUES=0

# 检查轮询间隔
HIGH_POLLING=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM SensorVendorConfiguration WHERE PollingIntervalMs > 20;")
if [ "$HIGH_POLLING" -gt 0 ]; then
    echo "✗ 发现 $HIGH_POLLING 个传感器的轮询间隔过大（>20ms）"
    echo "  建议：将PollingIntervalMs降低到10ms或更低"
    ISSUES=$((ISSUES + 1))
fi

# 检查忽略窗口
HIGH_WINDOW=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM SensorVendorConfiguration WHERE StateChangeIgnoreWindowMs > 500;")
if [ "$HIGH_WINDOW" -gt 0 ]; then
    echo "✗ 发现 $HIGH_WINDOW 个传感器的状态变化忽略窗口过大（>500ms）"
    echo "  建议：降低StateChangeIgnoreWindowMs或设置为0"
    ISSUES=$((ISSUES + 1))
fi

# 检查容差配置
LOW_TOLERANCE=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM ConveyorSegmentConfiguration WHERE TimeToleranceMs < LengthMm * 1.0 / SpeedMmps * 1000 * 0.3 AND SegmentId NOT IN (0, 999);")
if [ "$LOW_TOLERANCE" -gt 0 ]; then
    echo "✗ 发现 $LOW_TOLERANCE 个输送段的容差配置过小"
    echo "  建议：增加TimeToleranceMs，建议为理论传输时间的50%以上"
    ISSUES=$((ISSUES + 1))
fi

# 检查检测开关
DETECTION_ENABLED=$(sqlite3 "$DB_PATH" "SELECT EnableEarlyTriggerDetection FROM SystemConfiguration;")
if [ "$DETECTION_ENABLED" = "1" ]; then
    echo "⚠ 提前触发检测已启用"
    echo "  如果提前触发事件频繁，考虑禁用或调整容差配置"
fi

if [ $ISSUES -eq 0 ]; then
    echo "✓ 未发现明显的配置问题"
    echo ""
    echo "如果仍然存在延迟问题，建议："
    echo "1. 使用性能分析工具（dotTrace）定位具体瓶颈"
    echo "2. 检查IO卡的通信延迟"
    echo "3. 检查系统资源使用情况（CPU/内存）"
    echo "4. 添加性能日志记录各环节耗时"
else
    echo ""
    echo "发现 $ISSUES 个配置问题，请优先处理上述问题"
fi

echo ""
echo "===================================================="
echo "详细诊断指南：docs/SENSOR_LATENCY_DIAGNOSIS.md"
echo "===================================================="

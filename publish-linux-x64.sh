#!/bin/bash
# 发布脚本 - Linux x64 自包含部署
# Publish Script - Linux x64 Self-Contained Deployment

CONFIGURATION="${1:-Release}"
OUTPUT_PATH="${2:-./publish/linux-x64}"

echo -e "\033[32m开始发布 Linux x64 自包含版本...\033[0m"
echo -e "\033[32mPublishing Linux x64 self-contained version...\033[0m"

# 清理旧的发布输出
if [ -d "$OUTPUT_PATH" ]; then
    echo -e "\033[33m清理旧的发布输出 / Cleaning old publish output...\033[0m"
    rm -rf "$OUTPUT_PATH"
fi

# 发布项目
dotnet publish src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj \
    --configuration "$CONFIGURATION" \
    --runtime linux-x64 \
    --self-contained true \
    --output "$OUTPUT_PATH" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishReadyToRun=true

if [ $? -eq 0 ]; then
    echo -e "\n\033[32m发布成功！输出位置: $OUTPUT_PATH\033[0m"
    echo -e "\033[32mPublished successfully! Output location: $OUTPUT_PATH\033[0m"
    echo -e "\n\033[36m可执行文件: $OUTPUT_PATH/ZakYip.WheelDiverterSorter.Host\033[0m"
    echo -e "\033[36mExecutable: $OUTPUT_PATH/ZakYip.WheelDiverterSorter.Host\033[0m"
    
    # 设置可执行权限
    chmod +x "$OUTPUT_PATH/ZakYip.WheelDiverterSorter.Host"
    
    # 显示文件大小
    if [ -f "$OUTPUT_PATH/ZakYip.WheelDiverterSorter.Host" ]; then
        SIZE=$(du -h "$OUTPUT_PATH/ZakYip.WheelDiverterSorter.Host" | cut -f1)
        echo -e "\n\033[33m可执行文件大小: $SIZE\033[0m"
        echo -e "\033[33mExecutable size: $SIZE\033[0m"
    fi
else
    echo -e "\n\033[31m发布失败！\033[0m"
    echo -e "\033[31mPublish failed!\033[0m"
    exit 1
fi

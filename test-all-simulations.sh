#!/bin/bash

# Script to test all simulation programs
# This script runs both unit tests and executable simulations

set -e

echo "=========================================="
echo "测试所有仿真程序 (Test All Simulation Programs)"
echo "=========================================="
echo ""

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Counters
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Function to run a test and track results
run_test() {
    local test_name="$1"
    local test_command="$2"
    
    echo "----------------------------------------"
    echo "运行测试: $test_name"
    echo "----------------------------------------"
    
    TOTAL_TESTS=$((TOTAL_TESTS + 1))
    
    if eval "$test_command" > /tmp/test_output_$$.log 2>&1; then
        echo -e "${GREEN}✓ PASSED${NC}: $test_name"
        PASSED_TESTS=$((PASSED_TESTS + 1))
        return 0
    else
        echo -e "${RED}✗ FAILED${NC}: $test_name"
        echo "错误输出:"
        tail -20 /tmp/test_output_$$.log
        FAILED_TESTS=$((FAILED_TESTS + 1))
        return 1
    fi
}

echo "=========================================="
echo "第一部分: 运行单元测试"
echo "=========================================="
echo ""

# Get the base directory
BASE_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Run all simulation unit tests
run_test "仿真单元测试 (Simulation Unit Tests)" \
    "cd '$BASE_DIR/ZakYip.WheelDiverterSorter.E2ETests' && dotnet test --filter 'DisplayName~Simulation' --logger 'console;verbosity=minimal'"

echo ""
echo "=========================================="
echo "第二部分: 运行可执行仿真程序"
echo "=========================================="
echo ""

# Navigate to simulation project
cd "$BASE_DIR/ZakYip.WheelDiverterSorter.Simulation"

# Test different sorting modes
run_test "仿真程序 - RoundRobin 模式" \
    "dotnet run -- --Simulation:ParcelCount=5 --Simulation:SortingMode=RoundRobin --Simulation:IsPauseAtEnd=false"

run_test "仿真程序 - FixedChute 模式" \
    "dotnet run -- --Simulation:ParcelCount=5 --Simulation:SortingMode=FixedChute --Simulation:IsPauseAtEnd=false"

run_test "仿真程序 - Formal 模式" \
    "dotnet run -- --Simulation:ParcelCount=5 --Simulation:SortingMode=Formal --Simulation:IsPauseAtEnd=false"

# Test with different friction factors
run_test "仿真程序 - 高摩擦因子" \
    "dotnet run -- --Simulation:ParcelCount=5 --Simulation:FrictionModel:MinFactor=0.7 --Simulation:FrictionModel:MaxFactor=1.3 --Simulation:IsPauseAtEnd=false"

# Test with dropout
run_test "仿真程序 - 启用掉包模拟" \
    "dotnet run -- --Simulation:ParcelCount=10 --Simulation:IsEnableRandomDropout=true --Simulation:DropoutModel:DropoutProbabilityPerSegment=0.1 --Simulation:IsPauseAtEnd=false"

# Test Scenario E: High friction with dropout (NEW)
run_test "仿真程序 - 场景E: 高摩擦有丢失" \
    "dotnet run -- --Simulation:ParcelCount=10 --Simulation:IsEnableRandomFriction=true --Simulation:IsEnableRandomDropout=true --Simulation:FrictionModel:MinFactor=0.7 --Simulation:FrictionModel:MaxFactor=1.3 --Simulation:DropoutModel:DropoutProbabilityPerSegment=0.1 --Simulation:IsPauseAtEnd=false"

# Test with no friction or dropout (ideal conditions)
run_test "仿真程序 - 理想条件 (无摩擦无掉包)" \
    "dotnet run -- --Simulation:ParcelCount=5 --Simulation:IsEnableRandomFriction=false --Simulation:IsEnableRandomDropout=false --Simulation:IsPauseAtEnd=false"

# Test with larger parcel count
run_test "仿真程序 - 大包裹数量测试" \
    "dotnet run -- --Simulation:ParcelCount=20 --Simulation:IsPauseAtEnd=false"

echo ""
echo "=========================================="
echo "测试总结"
echo "=========================================="
echo ""
echo "总测试数: $TOTAL_TESTS"
echo -e "${GREEN}通过: $PASSED_TESTS${NC}"
echo -e "${RED}失败: $FAILED_TESTS${NC}"
echo ""

# Clean up temporary files
rm -f /tmp/test_output_$$.log

if [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}=========================================="
    echo "所有仿真程序测试通过! ✓"
    echo -e "==========================================${NC}"
    exit 0
else
    echo -e "${RED}=========================================="
    echo "部分测试失败，请检查上面的错误信息"
    echo -e "==========================================${NC}"
    exit 1
fi

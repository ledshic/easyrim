#!/bin/bash

# EasyRim Mod - Steam Workshop Upload Script
# 用于上传轻松环世界 mod 到 Steam 创意工坊的脚本

# 配置变量 / Configuration variables
STEAM_USERNAME=""  # 请填入你的 Steam 用户名 / Enter your Steam username
STEAM_PASSWORD=""  # 留空将提示输入 / leave empty for prompt
MOD_PATH="/Users/dongxuli/Documents/workspace/easyrim"
VDF_FILE="$MOD_PATH/easyrim.vdf"
WORKSHOP_STAGING_PATH="$MOD_PATH/.workshop-upload"
WORKSHOP_VDF_FILE="/tmp/easyrim_workshop_$(date +%s).vdf"
STEAMCMD_PATH="/usr/local/bin/steamcmd"  # macOS 默认 SteamCMD 路径 / Default macOS SteamCMD path

# 颜色输出 / Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 日志函数 / Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 检查 SteamCMD 是否已安装 / Check if SteamCMD is installed
check_steamcmd() {
    log_info "检查 SteamCMD 安装状态... / Checking SteamCMD installation..."

    if ! command -v steamcmd &> /dev/null; then
        log_warning "SteamCMD 未找到，尝试通过 Homebrew 安装... / SteamCMD not found, trying to install via Homebrew..."

        # 检查 Homebrew 是否已安装 / Check if Homebrew is installed
        if ! command -v brew &> /dev/null; then
            log_error "Homebrew 未安装，请先安装 Homebrew: /bin/bash -c \"\$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)\""
            exit 1
        fi

        # 安装 SteamCMD / Install SteamCMD
        log_info "正在安装 SteamCMD... / Installing SteamCMD..."
        brew install steamcmd

        if [ $? -eq 0 ]; then
            log_success "SteamCMD 安装成功! / SteamCMD installed successfully!"
        else
            log_error "SteamCMD 安装失败! / SteamCMD installation failed!"
            exit 1
        fi
    else
        log_success "SteamCMD 已安装 / SteamCMD is installed"
    fi
}

# 验证文件 / Validate files
validate_files() {
    log_info "验证 mod 文件... / Validating mod files..."

    # 检查 VDF 文件 / Check VDF file
    if [ ! -f "$VDF_FILE" ]; then
        log_error "VDF 文件未找到: $VDF_FILE"
        exit 1
    fi

    # 检查 About.xml / Check About.xml
    if [ ! -f "$MOD_PATH/About/About.xml" ]; then
        log_error "About.xml 文件未找到: $MOD_PATH/About/About.xml"
        exit 1
    fi

    # 检查预览图 / Check preview image
    if [ ! -f "$MOD_PATH/About/Preview.png" ]; then
        log_warning "预览图未找到，建议添加预览图以提升可见度 / Preview image not found, recommend adding for better visibility"
    fi

    log_success "文件验证完成 / File validation completed"
}

# 准备上传目录 / Prepare workshop content folder
prepare_workshop_content() {
    log_info "准备创意工坊上传内容... / Preparing Workshop upload content..."

    rm -rf "$WORKSHOP_STAGING_PATH"
    mkdir -p "$WORKSHOP_STAGING_PATH"

    local required_dirs=("About" "Assemblies" "Defs" "Languages" "Patches")
    for dir in "${required_dirs[@]}"; do
        if [ -d "$MOD_PATH/$dir" ]; then
            cp -R "$MOD_PATH/$dir" "$WORKSHOP_STAGING_PATH/"
        fi
    done

    if [ -f "$MOD_PATH/readme.md" ]; then
        cp "$MOD_PATH/readme.md" "$WORKSHOP_STAGING_PATH/"
    fi

    sed "s#\"contentfolder\"[[:space:]]*\"[^\"]*\"#\"contentfolder\"		\"$WORKSHOP_STAGING_PATH\"#" "$VDF_FILE" > "$WORKSHOP_VDF_FILE"
    log_success "上传内容已准备到: $WORKSHOP_STAGING_PATH"
}

# 获取 Steam 凭据 / Get Steam credentials
get_steam_credentials() {
    if [ -z "$STEAM_USERNAME" ]; then
        echo -n "请输入 Steam 用户名 / Enter Steam username: "
        read STEAM_USERNAME
    fi

    if [ -z "$STEAM_PASSWORD" ]; then
        echo -n "请输入 Steam 密码 / Enter Steam password: "
        read -s STEAM_PASSWORD
        echo
    fi

    if [ -z "$STEAM_USERNAME" ] || [ -z "$STEAM_PASSWORD" ]; then
        log_error "Steam 用户名和密码不能为空 / Steam username and password cannot be empty"
        exit 1
    fi
}

# 创建临时上传脚本 / Create temporary upload script
create_upload_script() {
    local temp_script="/tmp/steamcmd_upload_$(date +%s).txt"

    cat > "$temp_script" << EOF
@sSteamCmdForcePlatformType windows
login $STEAM_USERNAME $STEAM_PASSWORD
workshop_build_item "$WORKSHOP_VDF_FILE"
quit
EOF

    echo "$temp_script"
}

# 上传到 Steam Workshop / Upload to Steam Workshop
upload_to_steam() {
    log_info "开始上传到 Steam 创意工坊... / Starting upload to Steam Workshop..."

    # 创建临时脚本 / Create temporary script
    local upload_script=$(create_upload_script)

    # 执行上传 / Execute upload
    log_info "执行 SteamCMD 上传命令... / Executing SteamCMD upload command..."
    steamcmd +runscript "$upload_script"

    local exit_code=$?

    # 清理临时文件 / Clean up temporary file
    rm -f "$upload_script"
    rm -f "$WORKSHOP_VDF_FILE"

    if [ $exit_code -eq 0 ]; then
        log_success "Mod 上传成功! / Mod uploaded successfully!"
        log_info "你可以在 Steam 创意工坊查看你的 mod: https://steamcommunity.com/sharedfiles/filedetails/?id=3418717349"
    else
        log_error "上传失败，退出代码: $exit_code / Upload failed with exit code: $exit_code"
        exit 1
    fi
}

# 显示帮助信息 / Show help information
show_help() {
    echo "EasyRim Mod Steam Workshop 上传脚本 / Upload Script"
    echo ""
    echo "用法 / Usage:"
    echo "  $0 [选项] / [options]"
    echo ""
    echo "选项 / Options:"
    echo "  -u, --username USERNAME    Steam 用户名 / Steam username"
    echo "  -p, --password PASSWORD    Steam 密码 / Steam password"
    echo "  -h, --help                显示帮助信息 / Show this help message"
    echo ""
    echo "示例 / Examples:"
    echo "  $0                        # 交互式上传 / Interactive upload"
    echo "  $0 -u myusername          # 指定用户名 / Specify username"
    echo "  $0 -u myusername -p mypass # 指定用户名和密码 / Specify username and password"
}

# 解析命令行参数 / Parse command line arguments
parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -u|--username)
                STEAM_USERNAME="$2"
                shift 2
                ;;
            -p|--password)
                STEAM_PASSWORD="$2"
                shift 2
                ;;
            -h|--help)
                show_help
                exit 0
                ;;
            *)
                log_error "未知参数: $1 / Unknown argument: $1"
                show_help
                exit 1
                ;;
        esac
    done
}

# 主函数 / Main function
main() {
    echo "======================================"
    echo "EasyRim Mod Steam Workshop 上传工具"
    echo "EasyRim Mod Steam Workshop Upload Tool"
    echo "======================================"
    echo ""

    # 解析参数 / Parse arguments
    parse_arguments "$@"

    # 检查并安装 SteamCMD / Check and install SteamCMD
    check_steamcmd

    # 验证文件 / Validate files
    validate_files

    # 准备干净的上传内容 / Prepare clean upload content
    prepare_workshop_content

    # 获取 Steam 凭据 / Get Steam credentials
    get_steam_credentials

    # 上传到 Steam / Upload to Steam
    upload_to_steam

    log_success "上传流程完成! / Upload process completed!"
}

# 运行主函数 / Run main function
main "$@"

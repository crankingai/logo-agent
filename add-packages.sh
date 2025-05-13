#!/bin/bash

# Colors and symbols for better visibility
GREEN='\033[0;32m'
RED='\033[0;31m'
BOLD='\033[1m'
YELLOW='\033[0;33m'
NC='\033[0m' # No Color
CHECK_MARK="${GREEN}✓${NC}"
CROSS_MARK="${RED}✗${NC}"
INFO_MARK="${YELLOW}ℹ${NC}"

# Define packages with optional version specifications
# Package specification format:
# ============================
# * "PackageName" - Installs the latest stable version
# * "PackageName|1.2.3" - Installs a specific version 
# * "PackageName||prerelease" - Installs the latest prerelease version
# * "PackageName|1.2.3|prerelease" - Installs a specific prerelease version
#
declare -a PACKAGES=(
    "Azure.Identity"
    "Microsoft.Extensions.Hosting"
    "Microsoft.Extensions.Logging"
    "Microsoft.Extensions.Logging.Console"
    "Microsoft.Extensions.Logging.Debug"
    "Microsoft.Extensions.Configuration"
    "Microsoft.Extensions.Configuration.EnvironmentVariables"
    "Microsoft.Extensions.Configuration.UserSecrets"
    "Microsoft.SemanticKernel"
    "Microsoft.SemanticKernel.Agents.Abstractions"
    "Microsoft.SemanticKernel.Agents.Core"
    "Microsoft.SemanticKernel.Agents.AzureAI||prerelease"
    "Microsoft.SemanticKernel.Connectors.OpenAI"
    "Microsoft.SemanticKernel.Core"
    "Microsoft.SemanticKernel.Plugins.Web||prerelease"
    # "Microsoft.SemanticKernel.Agents.OpenAI||prerelease"  # Latest prerelease version
    # https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/sdk-overview?tabs=sync&pivots=programming-language-csharp#get-started-with-projects
    # "Azure.AI.Projects||prerelease"
    # "Azure.AI.Projects|1.0.0-beta.7|prerelease"
    # "Azure.AI.Projects|1.0.0-beta.7"
    "Azure.AI.Projects|1.0.0-beta.8"
    # "Azure.Identity"
    # "Azure.AI.OpenAI" # 2.2.0-beta.4
    "Azure.AI.Inference||prerelease"
    "Azure.Search.Documents"
    # MCP
    "ModelContextProtocol||prerelease"
    # "ModelContextProtocol.Client||prerelease"
    # "ModelContextProtocol.Client"
)

# Track total packages and failures
TOTAL_PACKAGES=${#PACKAGES[@]}
SUCCESSFUL=0
FAILED=0
FAILED_PACKAGES=""

echo -e "${BOLD}Starting package installation (${TOTAL_PACKAGES} packages)...${NC}\n"

# Function to install a package and handle output
install_package() {
    local package_info=$1

    # Parse package information
    IFS='|' read -r package_name version prerelease <<< "$package_info"

    # Build install command
    install_cmd="dotnet add package \"$package_name\""
    display_name="$package_name"

    # Add version if specified
    if [ -n "$version" ]; then
        install_cmd="$install_cmd -v \"$version\""
        display_name="$display_name (v$version)"
    fi

    # Add prerelease flag if specified
    if [ "$prerelease" == "prerelease" ]; then
        install_cmd="$install_cmd --prerelease"
        display_name="$display_name [prerelease]"
    fi

    echo -e "${BOLD}Installing:${NC} $display_name"

    # Capture all output and error in a temporary file
    temp_output=$(mktemp)
    if eval "$install_cmd" > "$temp_output" 2>&1; then
        echo -e "  ${CHECK_MARK} ${GREEN}Successfully installed:${NC} $display_name"
        ((SUCCESSFUL++))
    else
        echo -e "  ${CROSS_MARK} ${RED}FAILED TO INSTALL:${NC} $display_name"
        echo -e "${RED}Error details:${NC}"
        # Only show the important error lines, not the entire verbose output
        grep -E "error|fail|could not|unable to" "$temp_output" | head -5
        echo ""
        ((FAILED++))
        FAILED_PACKAGES="$FAILED_PACKAGES\n  ${CROSS_MARK} $display_name"
    fi
    rm "$temp_output"
}

# Example of how to modify the packages array for specific versions
# Uncomment and modify these examples as needed
# PACKAGES=(
#    "Microsoft.SemanticKernel|1.0.1"  # Specific version
#    "Microsoft.SemanticKernel.Connectors.OpenAI|1.0.0-beta"  # Specific version
#    "Microsoft.SemanticKernel.Agents.Abstractions||prerelease"  # Latest prerelease
#    "Microsoft.SemanticKernel.Agents.Core"  # Latest stable
#    "Microsoft.SemanticKernel.Agents.OpenAI|1.2.0|prerelease"  # Specific prerelease version
# )

# Install each package
for package in "${PACKAGES[@]}"; do
    install_package "$package"
done

# Print summary
echo -e "\n${BOLD}=============== INSTALLATION SUMMARY ===============${NC}"
echo -e "${BOLD}Total packages:${NC} $TOTAL_PACKAGES"
echo -e "${GREEN}${BOLD}Successfully installed:${NC} $SUCCESSFUL"

if [ $FAILED -gt 0 ]; then
    echo -e "${RED}${BOLD}Failed installations:${NC} $FAILED"
    echo -e "${RED}${BOLD}Failed packages:${NC}$FAILED_PACKAGES"
    echo -e "\n${BOLD}Please review the errors above and try again.${NC}"
    exit 1
else
    echo -e "\n${GREEN}${BOLD}All packages installed successfully!${NC}"
    exit 0
fi
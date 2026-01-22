CORE=7
FRAMEWORK=net10.0
RUNTIME=linux-x64

STD_DIR=PathSum.Standard
SIMD_DIR=PathSum.Simd

STD_BIN=$(STD_DIR)/bin/Release/$(FRAMEWORK)/$(RUNTIME)/publish/PathSum.Standard
SIMD_BIN=$(SIMD_DIR)/bin/Release/$(FRAMEWORK)/$(RUNTIME)/publish/PathSum.Simd

YELLOW=\033[1;33m
GREEN=\033[1;32m
RED=\033[1;31m
NC=\033[0m

.PHONY: all help std simd clean up down build-std build-simd build-all run-comparison

help: ## Display this help message
	@echo -e "$(YELLOW)Available commands:$(NC)"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-10s$(NC) %s\n", $$1, $$2}'

up: clean build-all run-comparison ## Build everything from scratch and run comparison

down: clean ## Synonym for clean (removes build artifacts)

clean: ## Remove all bin/ and obj/ directories
	@echo -e "$(RED)Cleaning build artifacts...$(NC)"
	@rm -rf $(STD_DIR)/bin $(STD_DIR)/obj
	@rm -rf $(SIMD_DIR)/bin $(SIMD_DIR)/obj

std: build-std ## Build and run only the Standard implementation
	@echo -e "$(YELLOW)>>> Running Standard implementation...$(NC)"
	@./$(STD_BIN)

simd: build-simd ## Build and run only the SIMD implementation
	@echo -e "$(YELLOW)>>> Running SIMD implementation on Core $(CORE)...$(NC)"
	@sudo tuna isolate --cpus $(CORE) > /dev/null
	@sudo env DOTNET_gcServer=0 DOTNET_Thread_UseAllCpuGroups=0 setarch $$(uname -m) -R chrt -f 99 taskset -c $(CORE) ./$(SIMD_BIN)

build-all: build-std build-simd

build-std:
	@echo -e "$(YELLOW)Building Standard (JIT Mode)...$(NC)"
	@dotnet publish $(STD_DIR)/$(STD_DIR).csproj -c Release -r $(RUNTIME) --self-contained true > /dev/null

build-simd:
	@echo -e "$(YELLOW)Building SIMD (Native AOT Mode)...$(NC)"
	@dotnet publish $(SIMD_DIR)/$(SIMD_DIR).csproj -c Release -r $(RUNTIME) -p:PublishAot=true -p:StackTraceSupport=false > /dev/null

run-comparison:
	@echo -e "\n$(YELLOW)========================================================$(NC)"
	@echo -e "$(YELLOW)   EXECUTING BENCHMARKS$(NC)"
	@echo -e "$(YELLOW)========================================================$(NC)"
	@set -e; \
	printf "$(YELLOW)Standard Implementation: $(NC)"; \
	OUT_STD=$$(./$(STD_BIN)); \
	TIME_STD=$$(echo "$$OUT_STD" | grep "\[AVG\]" | awk '{print $$2}'); \
	RES_STD=$$(echo "$$OUT_STD" | grep -oP '(?<=Result for 1024x1024: )\d+'); \
	if echo "$$OUT_STD" | grep -q "\[PASS\]"; then printf "$(GREEN)VERIFIED$(NC)\n"; else printf "$(RED)FAILED$(NC)\n"; fi; \
	\
	printf "$(YELLOW)SIMD Implementation:     $(NC)"; \
	sudo tuna isolate --cpus $(CORE) > /dev/null; \
	OUT_SIMD=$$(sudo env DOTNET_gcServer=0 DOTNET_Thread_UseAllCpuGroups=0 setarch $$(uname -m) -R chrt -f 99 taskset -c $(CORE) ./$(SIMD_BIN)); \
	TIME_SIMD=$$(echo "$$OUT_SIMD" | grep "\[AVG\]" | awk '{print $$2}'); \
	RES_SIMD=$$(echo "$$OUT_SIMD" | grep -oP '(?<=Result )\d+(?= matches reference)'); \
	if echo "$$OUT_SIMD" | grep -q "ALL TESTS PASSED"; then printf "$(GREEN)VERIFIED$(NC)\n"; else printf "$(RED)FAILED$(NC)\n"; fi; \
	\
	echo -e "\n$(YELLOW)========================================================$(NC)"; \
	echo -e "$(YELLOW)   BENCHMARK RESULTS SUMMARY$(NC)"; \
	echo -e "$(YELLOW)========================================================$(NC)"; \
	if [ "$$RES_STD" = "$$RES_SIMD" ]; then \
		echo -e "Status:      $(GREEN)SUCCESS (Results Match)$(NC)"; \
	else \
		echo -e "Status:      $(RED)FAILURE (Mismatch!)$$NC"; \
	fi; \
	echo -e "Standard:    $$TIME_STD us"; \
	echo -e "SIMD:        $$TIME_SIMD us"; \
	\
	SPEEDUP=$$(echo "scale=2; $$TIME_STD / $$TIME_SIMD" | bc); \
	IMPROVEMENT=$$(echo "scale=2; ($$TIME_STD - $$TIME_SIMD) / $$TIME_STD * 100" | bc); \
	echo -e "\n$(GREEN)>>> SIMD version is $$SPEEDUP x faster ($$IMPROVEMENT% reduction)$(NC)"; \
	echo -e "$(YELLOW)========================================================$(NC)"
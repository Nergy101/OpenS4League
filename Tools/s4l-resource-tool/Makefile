# S4 League Resource Tool (.NET 10 + Avalonia) — dev tasks.
# Requires the .NET 10 SDK. On Windows you can run these with `make` (if installed) or use the
# equivalent `dotnet` commands directly.

APP     = src/S4LResourceTool.App
CORE    = src/S4League.Resource
SELFTEST = tests/S4League.Resource.SelfTest
UITEST   = tests/S4LResourceTool.App.HeadlessTests
RID ?= osx-arm64

.DEFAULT_GOAL := help

.PHONY: run
run: ## Launch the GUI resource viewer/editor
	dotnet run --project $(APP)

.PHONY: build
build: ## Build the whole solution (Release)
	dotnet build -c Release

.PHONY: test
test: selftest uitest ## Run all tests (crypto self-test + headless UI)

.PHONY: selftest
selftest: ## Crypto/format round-trip self-test (creates a real resource.s4hd and reads it back)
	dotnet run -c Release --project $(SELFTEST)

.PHONY: uitest
uitest: ## Headless Avalonia UI tests
	dotnet test $(UITEST)

.PHONY: open
open: ## Open a real archive and dump a few entries:  make open ARCHIVE=/path/to/dir-or-resource.s4hd
	dotnet run -c Release --project $(SELFTEST) -- "$(ARCHIVE)"

.PHONY: extract
extract: ## Extract server data from a client:  make extract CLIENT=/path/to/client OUT=/path/to/data
	dotnet run -c Release --project $(SELFTEST) -- extract "$(CLIENT)" "$(OUT)"

.PHONY: publish
publish: ## Publish a self-contained app for RID (default osx-arm64; e.g. RID=win-x64)
	dotnet publish $(APP) -c Release -r $(RID) --self-contained -o publish/$(RID)

.PHONY: clean
clean: ## Remove build output
	dotnet clean -c Release; rm -rf publish

.PHONY: help
help: ## List targets
	@grep -hE '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) | \
	  awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'

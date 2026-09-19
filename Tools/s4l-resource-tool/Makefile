# S4 League Resource Tool (.NET 10 + Avalonia) — dev tasks.
# Requires the .NET 10 SDK. On Windows you can run these with `make` (if installed) or use the
# equivalent `dotnet` commands directly.

APP     = src/S4LResourceTool.App
CORE    = src/S4League.Resource
SELFTEST = tests/S4League.Resource.SelfTest
UITEST   = tests/S4LResourceTool.App.HeadlessTests
RID ?= osx-arm64

.DEFAULT_GOAL := help

# The interpreter every recipe uses: candidates are executed directly (a missing one only complains
# on stderr, which `$(shell ...)` ignores), so this needs no `command -v`/`where` and behaves the same
# under cmd.exe and sh. Override with PYTHON=<interpreter>.
PYTHON ?= $(shell python3 -c "import sys;print(sys.executable)" || py -c "import sys;print(sys.executable)" || python -c "import sys;print(sys.executable)")
PYTHON_REQUIRED = $(if $(PYTHON),,\
    $(error No Python 3 interpreter found (tried python3, py, python). Install one, or pass PYTHON=<interpreter>.))

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
	dotnet clean -c Release
	@$(PYTHON_REQUIRED)$(PYTHON) ../../scripts/remove.py publish

.PHONY: help
help: ## List targets
	@$(PYTHON_REQUIRED)$(PYTHON) ../../scripts/make-help.py Makefile

# OpenS4L — top-level build orchestration (Windows-first; portable dotnet commands).
# Requires the .NET 10 SDK. On Windows use git-bash / MSYS `make`, or run the dotnet commands directly.

.PHONY: help build tools admin bootstrap tool threejs server clean cleanup test coverage threejs-asset-upscale

THREEJS_GOALS := map-viewer character-viewer convert-assets
# Map recipes are accepted as goals too, so `make threejs map-viewer station-2` and
# `make threejs convert-assets neden-1` work without teaching the Makefile about individual maps.
# The recipes are the map identities both before and after conversion.
THREEJS_RECIPES := $(notdir $(basename $(wildcard Tools/s4l-threejs-converter/maps/*.json)))
THREEJS_MAP_GOALS := $(sort $(THREEJS_RECIPES))
THREEJS_ASSET_DIR ?= Client/Models/Characters/Wardrobe
# Path to YOUR unpacked Season-8 client ZIP. Never defaulted and never committed:
# pass THREEJS_SOURCE_ZIP=... or export S4_CLIENT_ZIP=...
THREEJS_SOURCE_ZIP ?= $(S4_CLIENT_ZIP)
THREEJS_ESRGAN_DIR ?= .cache/opens4l-realesrgan
THREEJS_PYTHON ?= $(shell for p in python3.13 python3.12 python3.11 python3.10 python3.9 /usr/bin/python3; do command -v $$p 2>/dev/null && break; done)
THREEJS_ESRGAN_WEIGHTS := $(THREEJS_ESRGAN_DIR)/RealESRGAN_x4plus.pth
THREEJS_ESRGAN_PYTHON := $(THREEJS_ESRGAN_DIR)/venv/bin/python
THREEJS_ESRGAN_URL := https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth
.PHONY: $(THREEJS_GOALS) $(THREEJS_MAP_GOALS)

# Tool names are also phony goals so `make tool s4l-map-editor` works.
TOOL_GOALS := s4l-resource-tool s4l-character-viewer s4l-map-editor s4l-animation-creator s4l-item-editor s4l-client-configurator s4l-client-mod-packer s4l-server-config-tool s4l-legacy-migration s4l-resource-diff s4l-localisation-editor s4l-admin-console
.PHONY: $(TOOL_GOALS)

.DEFAULT_GOAL := help

help: ## List targets
	@grep -hE '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) | \
	  awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-10s\033[0m %s\n", $$1, $$2}'

build: tools server ## Build everything (tools + server)

tools: ## Build the resource tool + desktop tooling (needs .NET 10)
	$(MAKE) -C Tools/s4l-resource-tool build
	dotnet build -c Release Tools/s4l-character-viewer
	dotnet build -c Release Tools/s4l-map-editor
	dotnet build -c Release Tools/s4l-client-configurator
	dotnet build -c Release Tools/s4l-animation-creator
	dotnet build -c Release Tools/s4l-resource-diff
	dotnet build -c Release Tools/s4l-client-mod-packer
	dotnet build -c Release Tools/s4l-localisation-editor
	dotnet build -c Release Tools/s4l-item-editor
	dotnet build -c Release Tools/s4l-server-config-tool
	dotnet build -c Release Tools/s4l-legacy-migration

admin: ## Build the server admin console web dashboard (needs pnpm)
	cd Tools/s4l-admin-console/web && pnpm install && pnpm run build

bootstrap: ## Start the server + admin dashboard and open the dashboard in a browser
	@python3 scripts/bootstrap.py || py scripts/bootstrap.py || python scripts/bootstrap.py

tool: ## List tools, or build and launch one: make tool <toolname>
	@python3 scripts/tool.py "$(if $(TOOL),$(TOOL),$(word 2,$(MAKECMDGOALS)))" || py scripts/tool.py "$(if $(TOOL),$(TOOL),$(word 2,$(MAKECMDGOALS)))" || python scripts/tool.py "$(if $(TOOL),$(TOOL),$(word 2,$(MAKECMDGOALS)))"

$(TOOL_GOALS):
	@:

threejs: ## List the Three.js viewers/maps, or launch one: make threejs map-viewer [<map>]
	@$(THREEJS_PYTHON) scripts/threejs.py "$(word 2,$(MAKECMDGOALS))" "$(word 3,$(MAKECMDGOALS))"

$(THREEJS_GOALS) $(THREEJS_MAP_GOALS):
	@:

threejs-asset-upscale: ## Generate wardrobe 1x/2x/4x assets with Real-ESRGAN (needs S4_CLIENT_ZIP/THREEJS_SOURCE_ZIP)
	@test -n "$(THREEJS_SOURCE_ZIP)" || { echo "Set S4_CLIENT_ZIP=/path/to/your Season-8 client ZIP (user-supplied client data is never committed)." >&2; exit 1; }
	@test -f "$(THREEJS_SOURCE_ZIP)" || { echo "Missing source archive: $(THREEJS_SOURCE_ZIP)" >&2; exit 1; }
	@mkdir -p "$(THREEJS_ESRGAN_DIR)"
	@if [ -x "$(THREEJS_ESRGAN_PYTHON)" ] && "$(THREEJS_ESRGAN_PYTHON)" --version 2>&1 | grep -q 'Python 3.14'; then mv "$(THREEJS_ESRGAN_DIR)/venv" "$(THREEJS_ESRGAN_DIR)/venv-incompatible-$$(date +%Y%m%d%H%M%S)"; rm -f "$(THREEJS_ESRGAN_DIR)/.installed"; fi
	@if [ ! -x "$(THREEJS_ESRGAN_PYTHON)" ]; then "$(THREEJS_PYTHON)" -m venv "$(THREEJS_ESRGAN_DIR)/venv"; fi
	@if [ ! -f "$(THREEJS_ESRGAN_DIR)/.installed" ]; then "$(THREEJS_ESRGAN_PYTHON)" -m pip install --upgrade pip 'realesrgan==0.3.0' && touch "$(THREEJS_ESRGAN_DIR)/.installed"; fi
	@if [ ! -f "$(THREEJS_ESRGAN_WEIGHTS)" ]; then curl -L --fail --retry 3 -o "$(THREEJS_ESRGAN_WEIGHTS)" "$(THREEJS_ESRGAN_URL)"; fi
	@if [ ! -f "$(THREEJS_ASSET_DIR)/index.json" ]; then dotnet run -c Release --project Tools/s4l-threejs-converter -- "$(THREEJS_SOURCE_ZIP)" "$(THREEJS_ASSET_DIR)" --wardrobe --texture-quality 1x,2x,4x; fi
	"$(THREEJS_ESRGAN_PYTHON)" Tools/s4l-threejs-converter/scripts/generate-esrgan-textures.py --root "$(THREEJS_ASSET_DIR)" --weights "$(THREEJS_ESRGAN_WEIGHTS)"

server: ## Build the .NET 10 server rebuild
	$(MAKE) -C Server build

test: server ## Build + run the server unit tests
	$(MAKE) -C Server test

coverage: server ## Build + run the server unit tests with coverage collection
	$(MAKE) -C Server coverage

clean: ## Clean all build output
	$(MAKE) -C Tools/s4l-resource-tool clean || true
	$(MAKE) -C Server clean || true

cleanup: ## Remove test coverage results + test artifacts over all tools & servers (keeps build output)
	$(MAKE) -C Tools/s4l-resource-tool clean || true
	$(MAKE) -C Server cleanup || true

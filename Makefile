# OpenS4L — top-level build orchestration (Windows-first; portable dotnet commands).
# Requires the .NET 10 SDK. On Windows use git-bash / MSYS `make`, or run the dotnet commands directly.

.PHONY: help build tools admin bootstrap tool threejs server clean cleanup test coverage threejs-asset-upscale threejs-asset-encode threejs-map-upscale threejs-map-encode threejs-avif-all

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
# Real-ESRGAN's venv + pinned model weights live here; the target bootstraps them on first run.
THREEJS_ESRGAN_DIR ?= .cache/opens4l-realesrgan
# Where the upscale run mirrors its output; empty means the driver's own timestamped default
# (<cache>/logs/upscale-<timestamp>.log).
THREEJS_UPSCALE_LOG ?=
# The interpreter every recipe uses. Each candidate is executed directly — the one that exists
# answers on stdout, a missing one only complains on stderr, which `$(shell ...)` ignores — so this
# needs neither `command -v` (sh-only) nor `where` (cmd-only) and behaves the same under cmd.exe and
# sh. Override with PYTHON=<interpreter> (or the older THREEJS_PYTHON=<interpreter>).
PYTHON ?= $(shell python3 -c "import sys;print(sys.executable)" || py -c "import sys;print(sys.executable)" || python -c "import sys;print(sys.executable)")
THREEJS_PYTHON ?= $(PYTHON)
# An empty PYTHON must fail one target with a readable line, not a shell "command not found".
PYTHON_REQUIRED = $(if $(PYTHON),,\
    $(error No Python 3 interpreter found (tried python3, py, python). Install one, or pass PYTHON=<interpreter>.))
.PHONY: $(THREEJS_GOALS) $(THREEJS_MAP_GOALS)

# Tool names are also phony goals so `make tool s4l-map-editor` works.
TOOL_GOALS := s4l-resource-tool s4l-character-viewer s4l-map-editor s4l-animation-creator s4l-item-editor s4l-client-configurator s4l-client-mod-packer s4l-server-config-tool s4l-legacy-migration s4l-resource-diff s4l-localisation-editor s4l-admin-console
.PHONY: $(TOOL_GOALS)

.DEFAULT_GOAL := help

help: ## List targets
	@$(PYTHON_REQUIRED)$(PYTHON) scripts/make-help.py Makefile

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
	@$(PYTHON_REQUIRED)$(PYTHON) scripts/bootstrap.py

tool: ## List tools, or build and launch one: make tool <toolname>
	@$(PYTHON_REQUIRED)$(PYTHON) scripts/tool.py "$(if $(TOOL),$(TOOL),$(word 2,$(MAKECMDGOALS)))"

$(TOOL_GOALS):
	@:

threejs: ## List the Three.js viewers/maps, or launch one: make threejs map-viewer [<map>]
	@$(PYTHON_REQUIRED)$(PYTHON) scripts/threejs.py "$(word 2,$(MAKECMDGOALS))" "$(word 3,$(MAKECMDGOALS))"

$(THREEJS_GOALS) $(THREEJS_MAP_GOALS):
	@:

threejs-asset-upscale: ## Generate the wardrobe 1x/4x levels, then the Real-ESRGAN 4x color+alpha (needs S4_CLIENT_ZIP/THREEJS_SOURCE_ZIP)
	@$(PYTHON_REQUIRED)$(PYTHON) Tools/s4l-threejs-converter/scripts/upscale-assets.py --source "$(THREEJS_SOURCE_ZIP)" --assets "$(THREEJS_ASSET_DIR)" --cache "$(THREEJS_ESRGAN_DIR)" --python "$(PYTHON)" $(if $(TORCH_INDEX),--torch-index-url "$(TORCH_INDEX)",) $(if $(THREEJS_UPSCALE_LOG),--log "$(THREEJS_UPSCALE_LOG)",)

threejs-map-upscale: ## Generate one map's 1x/4x levels, then its Real-ESRGAN 4x color+alpha: make threejs-map-upscale MAP=station-2
	@$(PYTHON_REQUIRED)$(PYTHON) Tools/s4l-threejs-converter/scripts/upscale-assets.py --map "$(MAP)" --source "$(THREEJS_SOURCE_ZIP)" --cache "$(THREEJS_ESRGAN_DIR)" --python "$(PYTHON)" $(if $(TORCH_INDEX),--torch-index-url "$(TORCH_INDEX)",) $(if $(THREEJS_UPSCALE_LOG),--log "$(THREEJS_UPSCALE_LOG)",)

threejs-map-encode: ## Re-encode a map's generated level as AVIF/WebP: make threejs-map-encode MAP=station-2 [FORMAT=avif] [IN_PLACE=1]
	@$(PYTHON_REQUIRED)$(PYTHON) Tools/s4l-threejs-converter/scripts/encode-texture-levels.py --map "$(MAP)" --format "$(if $(FORMAT),$(FORMAT),avif)" $(if $(QUALITY),--quality "$(QUALITY)",) $(if $(KINDS),--kinds "$(KINDS)",) $(if $(IN_PLACE),--in-place,)

threejs-asset-encode: ## Re-encode the wardrobe's 4x levels as AVIF/WebP in place: make threejs-asset-encode [FORMAT=avif] [KINDS=color,alpha,lightmap,normal]
	@$(PYTHON_REQUIRED)$(PYTHON) Tools/s4l-threejs-converter/scripts/encode-texture-levels.py "$(THREEJS_ASSET_DIR)" --manifest index.json --in-place --format "$(if $(FORMAT),$(FORMAT),avif)" --kinds "$(if $(KINDS),$(KINDS),color,alpha,lightmap,normal)"

threejs-avif-all: ## Every asset to its final state (4x pass + AVIF in place, wardrobe + every map): make threejs-avif-all [ONLY=wardrobe|maps] [MAP=station-2] [DRY_RUN=1]
	@$(PYTHON_REQUIRED)$(PYTHON) Tools/s4l-threejs-converter/scripts/avif-pass-all.py $(if $(THREEJS_SOURCE_ZIP),--source "$(THREEJS_SOURCE_ZIP)",) --format "$(if $(FORMAT),$(FORMAT),avif)" --python "$(PYTHON)" $(if $(TORCH_INDEX),--torch-index-url "$(TORCH_INDEX)",) $(if $(filter wardrobe,$(ONLY)),--skip-maps,) $(if $(filter maps,$(ONLY)),--skip-wardrobe,) $(if $(MAP),--map "$(MAP)",) $(if $(LOG),--log "$(LOG)",) $(if $(REPORT),--report "$(REPORT)",) $(if $(DRY_RUN),--dry-run,)

server: ## Build the .NET 10 server rebuild
	$(MAKE) -C Server build

test: server ## Build + run the server unit tests
	$(MAKE) -C Server test

coverage: server ## Build + run the server unit tests with coverage collection
	$(MAKE) -C Server coverage

clean: ## Clean all build output
	$(MAKE) -C Tools/s4l-resource-tool clean || exit 0
	$(MAKE) -C Server clean || exit 0

cleanup: ## Remove test coverage results + test artifacts over all tools & servers (keeps build output)
	$(MAKE) -C Tools/s4l-resource-tool clean || exit 0
	$(MAKE) -C Server cleanup || exit 0

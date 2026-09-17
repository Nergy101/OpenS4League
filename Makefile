# OpenS4L — top-level build orchestration (Windows-first; portable dotnet commands).
# Requires the .NET 10 SDK. On Windows use git-bash / MSYS `make`, or run the dotnet commands directly.

.PHONY: help build tools admin bootstrap tool threejs server clean cleanup test coverage

THREEJS_GOALS := map-viewer character-viewer convert-assets
.PHONY: $(THREEJS_GOALS)

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

threejs: ## List Three.js viewers, or launch one: make threejs map-viewer
	@python3 scripts/threejs.py "$(word 2,$(MAKECMDGOALS))" || py scripts/threejs.py "$(word 2,$(MAKECMDGOALS))" || python scripts/threejs.py "$(word 2,$(MAKECMDGOALS))"

$(THREEJS_GOALS):
	@:

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

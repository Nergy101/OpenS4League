# OpenS4L / Server — build the .NET 10 server rebuild (Windows-first).
# Requires the .NET 10 SDK. On Windows use git-bash / MSYS `make`, or run the dotnet commands directly.

SLN   = opens4l/OpenS4L.Server.slnx
CONF ?= Release

SERVERS = OpenS4L.Server.Auth OpenS4L.Server.Chat OpenS4L.Server.Game OpenS4L.Server.Relay

.PHONY: help build server publish clean cleanup test coverage

# The interpreter every recipe uses: candidates are executed directly (a missing one only complains
# on stderr, which `$(shell ...)` ignores), so this needs no `command -v`/`where` and behaves the same
# under cmd.exe and sh. Override with PYTHON=<interpreter>.
PYTHON ?= $(shell python3 -c "import sys;print(sys.executable)" || py -c "import sys;print(sys.executable)" || python -c "import sys;print(sys.executable)")
PYTHON_REQUIRED = $(if $(PYTHON),,\
    $(error No Python 3 interpreter found (tried python3, py, python). Install one, or pass PYTHON=<interpreter>.))

.DEFAULT_GOAL := help

help: ## List targets
	@$(PYTHON_REQUIRED)$(PYTHON) ../scripts/make-help.py Makefile

build: server ## Build the whole solution (Release)

server: ## Build the solution (default Release)
	dotnet build $(SLN) -c $(CONF)

test: ## Run the server unit tests (OpenS4L.Server.Mapping.Tests)
	dotnet test opens4l/tests/OpenS4L.Server.Mapping.Tests/OpenS4L.Server.Mapping.Tests.csproj -c $(CONF)

coverage: ## Run the server unit tests with line-coverage collection (coverlet)
	dotnet test opens4l/tests/OpenS4L.Server.Mapping.Tests/OpenS4L.Server.Mapping.Tests.csproj -c $(CONF) \
	  --collect:"XPlat Code Coverage" --results-directory opens4l/tests/coverage-results

publish: ## Publish all four servers to opens4l/dist/<short-name> (auth, chat, game, relay)
	dotnet publish opens4l/src/OpenS4L.Server.Auth/OpenS4L.Server.Auth.csproj -c $(CONF) -o opens4l/dist/auth
	dotnet publish opens4l/src/OpenS4L.Server.Chat/OpenS4L.Server.Chat.csproj -c $(CONF) -o opens4l/dist/chat
	dotnet publish opens4l/src/OpenS4L.Server.Game/OpenS4L.Server.Game.csproj -c $(CONF) -o opens4l/dist/game
	dotnet publish opens4l/src/OpenS4L.Server.Relay/OpenS4L.Server.Relay.csproj -c $(CONF) -o opens4l/dist/relay

clean: ## Remove build output
	dotnet clean $(SLN) -c $(CONF)
	@$(PYTHON_REQUIRED)$(PYTHON) ../scripts/remove.py opens4l/dist "opens4l/src/*/bin" "opens4l/src/*/obj"

cleanup: ## Remove test coverage results + test artifacts (keeps build output)
	@$(PYTHON_REQUIRED)$(PYTHON) ../scripts/remove.py opens4l/tests/coverage-results opens4l/tests/OpenS4L.Server.Mapping.Tests/TestResults

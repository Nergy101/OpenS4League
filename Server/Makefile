# OpenS4L / Server — build the .NET 10 server rebuild (Windows-first).
# Requires the .NET 10 SDK. On Windows use git-bash / MSYS `make`, or run the dotnet commands directly.

SLN   = opens4l/OpenS4L.Server.slnx
CONF ?= Release

SERVERS = OpenS4L.Server.Auth OpenS4L.Server.Chat OpenS4L.Server.Game OpenS4L.Server.Relay

.PHONY: help build server publish clean cleanup test coverage

.DEFAULT_GOAL := help

help: ## List targets
	@grep -hE '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) | \
	  awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-10s\033[0m %s\n", $$1, $$2}'

build: server ## Build the whole solution (Release)

server: ## Build the solution (default Release)
	dotnet build $(SLN) -c $(CONF)

test: ## Run the server unit tests (OpenS4L.Server.Mapping.Tests)
	dotnet test opens4l/tests/OpenS4L.Server.Mapping.Tests/OpenS4L.Server.Mapping.Tests.csproj -c $(CONF)

coverage: ## Run the server unit tests with line-coverage collection (coverlet)
	dotnet test opens4l/tests/OpenS4L.Server.Mapping.Tests/OpenS4L.Server.Mapping.Tests.csproj -c $(CONF) \
	  --collect:"XPlat Code Coverage" --results-directory opens4l/tests/coverage-results

publish: ## Publish all four servers to opens4l/dist/<short-name> (auth, chat, game, relay)
	@for s in $(SERVERS); do \
	  n=$$(echo $$s | sed 's/.*\.//' | tr '[:upper:]' '[:lower:]'); \
	  echo "== publishing $$s -> opens4l/dist/$$n =="; \
	  dotnet publish opens4l/src/$$s/$$s.csproj -c $(CONF) -o opens4l/dist/$$n; \
	done

clean: ## Remove build output
	dotnet clean $(SLN) -c $(CONF)
	rm -rf opens4l/dist opens4l/src/*/bin opens4l/src/*/obj

cleanup: ## Remove test coverage results + test artifacts (keeps build output)
	rm -rf opens4l/tests/coverage-results opens4l/tests/OpenS4L.Server.Mapping.Tests/TestResults

# OpenS4L — Docker run helper (Windows-first: git-bash/MSYS make + docker).
# Builds/publishes the servers, stages the plugins, then brings up the compose stack.

.PHONY: help build publish plugins up bootstrap provision provision-bots logs down clean loadtest loadtest-smoke loadtest-load loadtest-hundred loadtest-soak loadbot loadbot-help

# k6 load tests for the WebApi HTTP plugin (port 22000), via the official
# grafana/k6 image (no local install; identical on macOS/Windows/Linux).
# TARGET_URL defaults to the Docker host's published 22000 (host.docker.internal)
# so the k6 container reaches the local stack. Override for cloud, e.g.:
#   make loadtest TARGET_URL=https://game.example.com
K6_IMAGE ?= grafana/k6:latest
TARGET_URL ?= http://host.docker.internal:22000

# Load-bot scenario (single-channel | all-channels). Used by the `loadbot` target.
SCENARIO ?= single-channel

COMPOSE = docker compose

.DEFAULT_GOAL := help

help: ## List targets
	@grep -hE '^[a-zA-Z0-9_-]+:.*?## ' $(MAKEFILE_LIST) | \
	  awk 'BEGIN{FS=":.*?## "}{printf "  \033[36m%-11s\033[0m %s\n", $$1, $$2}'

build: ## Build the whole server solution (servers + plugins) on .NET 10
	$(MAKE) -C .. build

data: ## Populate the opens4l_clientdata volume from a data dir:  make data SRC=/path/to/client-data
	@./populate-data.sh "$(SRC)"

publish: ## Publish the 4 servers to ../opens4l/dist/<server> and stage plugins
	$(MAKE) -C .. publish
	$(MAKE) plugins

plugins: ## Stage plugin DLLs (+ config .hjson, + deps) into per-server plugins/ folders
	@mkdir -p plugins/auth plugins/chat plugins/game plugins/relay
	@rm -rf plugins/auth/*.dll plugins/auth/*.hjson plugins/chat/*.dll plugins/chat/*.hjson plugins/game/*.dll plugins/game/*.hjson plugins/relay/*.dll plugins/relay/*.hjson
	@# The bundled plugins all target the Game server (they reference OpenS4L.Server.Game).
	@for d in ../opens4l/src/plugins/OpenS4L.Plugins.*; do \
	  [ -d "$$d/bin/Release/net10.0" ] && cp "$$d"/bin/Release/net10.0/*.dll plugins/game/ 2>/dev/null; \
	  [ -d "$$d/bin/Release/net10.0" ] && cp "$$d"/bin/Release/net10.0/*.hjson plugins/game/ 2>/dev/null; \
	done
	@# The WebApi plugin now hosts Kestrel via the Microsoft.AspNetCore.App shared framework
	@# (the aspnet:10.0 base image already provides it), so no EmbedIO/Swan DLLs need staging.
	@echo "staged plugins: game=$$(ls plugins/game/*.dll 2>/dev/null | wc -l) dll(s), auth/chat/relay empty"

up: ## Bring up the stack (PostgreSQL + Redis + 4 servers). Run `make publish` first.
	$(COMPOSE) up -d

bootstrap: ## Full first-time bring-up: publish, stage plugins, up, provision, reload game
	$(MAKE) publish
	$(COMPOSE) up -d
	$(COMPOSE) run --rm provision
	$(COMPOSE) up -d --force-recreate game

provision: ## Create admin/admin + load the free shop (idempotent)
	$(COMPOSE) run --rm provision
	$(COMPOSE) up -d --force-recreate game

provision-bots: ## Create bot accounts for load testing (make provision-bots BOTS=100; idempotent)
	$(COMPOSE) run --rm -e ONLY_BOTS=1 -e BOT_COUNT=$(if $(BOTS),$(BOTS),100) \
	  -e BOT_PASSWORD=$(if $(PASSWORD),$(PASSWORD),admin) provision

logs: ## Follow the game server log
	$(COMPOSE) logs -f game

down: ## Stop the stack (keep the DB volume)
	$(COMPOSE) down

clean: ## Stop everything and remove the DB volume
	$(COMPOSE) down -v

# Run k6 by piping the self-contained script over stdin (no volume mount, so it
# works the same under git-bash/MSYS on Windows). Profile and volume via env vars,
# e.g. make loadtest-soak VUS=50 DURATION=1h TARGET_URL=https://game.example.com
loadtest: loadtest-smoke ## Run a quick k6 smoke test against the WebApi (default: local stack)

loadtest-smoke: ## k6 smoke test — few VUs, few iterations, quick sanity
	docker run --rm -i $(K6_IMAGE) run \
	  -e TARGET_URL=$(TARGET_URL) -e LOAD_PROFILE=smoke - < load/script.js

loadtest-load: ## k6 load test — ramp to VUS (default 50), hold 3m, ramp down
	docker run --rm -i $(K6_IMAGE) run \
	  -e TARGET_URL=$(TARGET_URL) -e LOAD_PROFILE=load \
	  -e VUS=$(if $(VUS),$(VUS),50) - < load/script.js

loadtest-hundred: ## k6 100-player test — ramp to VUS (default 100), hold 1m, ramp down
	docker run --rm -i $(K6_IMAGE) run \
	  -e TARGET_URL=$(TARGET_URL) -e LOAD_PROFILE=hundred \
	  -e VUS=$(if $(VUS),$(VUS),100) - < load/script.js

loadtest-soak: ## k6 soak test — hold VUS (default 30) for DURATION (default 30m)
	docker run --rm -i $(K6_IMAGE) run \
	  -e TARGET_URL=$(TARGET_URL) -e LOAD_PROFILE=soak \
	  -e VUS=$(if $(VUS),$(VUS),30) -e DURATION=$(if $(DURATION),$(DURATION),30m) - < load/script.js

# Real-ProudNet-client simulated players (see ../../opens4l/tests/OpenS4L.LoadBot). Each bot logs
# into auth, connects to the game server and enters a channel, so they appear in the admin
# console (/players, /channels). Runs on the host (reaches the published ports on 127.0.0.1);
# needs the stack up. For N concurrent bots set USERPREFIX=bot (each bot = account {prefix}{i};
# provision them first with `make provision-bots BOTS=N`). Env: BOTS, CHANNEL, ACCOUNT, PASSWORD,
# USERPREFIX, STAY, NICKPREFIX, GAME (remote override),
# SCENARIO (single-channel|all-channels|chat|all-channels-chat),
# PER_CHANNEL (players per channel for all-channels / all-channels-chat),
# MESSAGES (chat msgs per bot for chat / all-channels-chat), CHAT (chat endpoint).
loadbot: ## Simulate N real-game-protocol players (default BOTS=1, CHANNEL=4, PASS=admin/admin)
	dotnet run --project ../opens4l/tests/OpenS4L.LoadBot/OpenS4L.LoadBot.csproj -c Release -- \
	  --auth $(if $(AUTH),$(AUTH),127.0.0.1:28002) \
	  $(if $(GAME),--game $(GAME),) \
	  --pass $(if $(PASSWORD),$(PASSWORD),admin) \
	  --scenario $(SCENARIO) \
	  $(if $(USERPREFIX),--user-prefix $(USERPREFIX),) \
	  $(if $(NICKPREFIX),--nick-prefix $(NICKPREFIX),) \
	  --stay $(if $(STAY),$(STAY),0) \
	  $(if $(filter single-channel,$(SCENARIO)),--count $(if $(BOTS),$(BOTS),1) --channel $(if $(CHANNEL),$(CHANNEL),4),) \
	  $(if $(filter all-channels,$(SCENARIO)),--per-channel $(if $(PER_CHANNEL),$(PER_CHANNEL),1),) \
	  $(if $(filter chat,$(SCENARIO)),--count $(if $(BOTS),$(BOTS),1) \
	    $(if $(MESSAGES),--messages $(MESSAGES),) \
	    $(if $(CHAT),--chat-endpoint $(CHAT),),) \
	  $(if $(filter all-channels-chat,$(SCENARIO)),--per-channel $(if $(PER_CHANNEL),$(PER_CHANNEL),1) \
	    --messages $(if $(MESSAGES),$(MESSAGES),10) \
	    $(if $(CHAT),--chat-endpoint $(CHAT),),)

loadbot-help: ## Explain all loadbot scenarios + parameters
	@echo ""
	@echo "OpenS4L load-bot — real ProudNet clients that log in and play, so the admin"
	@echo "console (/players, /channels, /statistics) reflects real players."
	@echo ""
	@echo "USAGE:  make loadbot [VAR=value ...]    (from Server/Docker, stack up)"
	@echo ""
	@echo "PREREQUISITES"
	@echo "  make provision-bots BOTS=N   create N bot accounts (bot0..botN-1), one per bot."
	@echo "                               Every concurrent bot needs its own account."
	@echo "  Stack must be up (make up / make bootstrap). After a game restart wait ~5s so"
	@echo "  the game server announces itself to auth (else bots log 'no game server')."
	@echo ""
	@echo "SCENARIOS  (make loadbot SCENARIO=<name>)"
	@echo "  single-channel   N bots into one channel.      [default]"
	@echo "  all-channels     P players in every existing channel (discovers them from /channels)."
	@echo "  chat             N bots that also log into the chat server and send channel chat."
	@echo "  all-channels-chat  P players in every channel, each sending M chat messages."
	@echo ""
	@echo "EXAMPLES"
	@echo "  make loadbot                                   # 1 bot -> channel 4 (admin/admin)"
	@echo "  make loadbot BOTS=20 STAY=120                  # 20 bots for 2 minutes"
	@echo "  make loadbot SCENARIO=all-channels PER_CHANNEL=10 USERPREFIX=bot STAY=180"
	@echo "                     # 10 players in every channel (110 bots with 11 channels)"
	@echo "  make loadbot SCENARIO=chat BOTS=10 USERPREFIX=bot STAY=120   # 10 chatting bots"
	@echo "  make loadbot SCENARIO=all-channels-chat PER_CHANNEL=10 MESSAGES=10 USERPREFIX=bot"
	@echo "                     # 10 players in every channel, each sending 10 chat msgs"
	@echo ""
	@echo "PARAMETERS (environment overrides)"
	@echo "  SCENARIO     single-channel | all-channels | chat | all-channels-chat"
	@echo "               (default single-channel)"
	@echo "  BOTS         number of bots / players (count)         (default 1)"
	@echo "  CHANNEL      channel id for single-channel            (default 4)"
	@echo "  PER_CHANNEL  players per channel for all-channels / all-channels-chat (default 1)"
	@echo "  MESSAGES     chat messages per bot for chat / all-channels-chat (default 10)"
	@echo "  USERPREFIX   each bot uses account {prefix}{i}        (needed for N bots)"
	@echo "  ACCOUNT      single account username (no USERPREFIX)  (default admin)"
	@echo "  PASSWORD     account / bot password                   (default admin)"
	@echo "  NICKPREFIX   nickname prefix                          (default bot)"
	@echo "  STAY         seconds to stay online, 0=forever        (default 0)"
	@echo "  AUTH         auth endpoint host:port                  (default 127.0.0.1:28002)"
	@echo "  GAME         game endpoint host:port (override server list) (default: auto)"
	@echo "  CHAT         chat endpoint host:port for chat scenario(default 127.0.0.1:28003)"
	@echo ""
	@echo "NOTES"
	@echo "  * MaxSessions in config/game/config.hjson caps concurrent game connections"
	@echo "    (default 100). Raise it to run >100 concurrent bots, else you get ServerFull."
	@echo "  * Nicknames must obey NickRestrictions (4-30 chars, ASCII, max 3 repeated)."
	@echo "  * Exit 0 = every bot reached its channel; exit 1 = some/all failed."

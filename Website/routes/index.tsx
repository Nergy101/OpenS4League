import { Head } from "fresh/runtime";
import { define } from "../utils.ts";

const servers = [
  {
    name: "Auth",
    icon: "◈",
    tone: "blue",
    port: "28002",
    desc: "Login, accounts, and ban enforcement.",
  },
  {
    name: "Chat",
    icon: "◌",
    tone: "violet",
    port: "28003",
    desc: "Channels, clans, and in-game messaging.",
  },
  {
    name: "Game",
    icon: "✦",
    tone: "green",
    port: "28004",
    desc: "Rooms, rules, matches — the heavy part.",
  },
  {
    name: "Relay",
    icon: "⟷",
    tone: "orange",
    port: "28005",
    desc: "Connects the other services together.",
  },
];

const tools = [
  {
    name: "Resource editing",
    icon: "▦",
    items: ["s4l-resource-tool", "s4l-resource-diff"],
  },
  {
    name: "Characters & animation",
    icon: "✧",
    items: ["s4l-character-viewer", "s4l-animation-creator"],
  },
  {
    name: "Maps & client",
    icon: "⌘",
    items: [
      "s4l-map-editor",
      "s4l-client-configurator",
      "s4l-client-mod-packer",
    ],
  },
  {
    name: "Server operations",
    icon: "⌁",
    items: [
      "s4l-admin-console",
      "s4l-server-config-tool",
      "s4l-legacy-migration",
      "s4l-localisation-editor",
    ],
  },
];

const roadmap = [
  { label: "Server rewrite", status: "done", detail: ".NET 10 foundation" },
  {
    label: "Tooling suite",
    status: "active",
    detail: "Cross-platform editors",
  },
  {
    label: "Client rewrite",
    status: "planned",
    detail: "A native open client",
  },
  {
    label: "Sandbox play",
    status: "planned",
    detail: "Singleplayer experiments",
  },
];

export default define.page(function Home() {
  return (
    <>
      <Head>
        <title>OpenS4League — Open source S4 League infrastructure</title>
      </Head>
      <header class="site-header">
        <div class="wrap header-inner">
          <a href="#top" class="brand">
            <img
              src="/logos/os4l-crest-flat-dark.svg"
              width="32"
              height="32"
              alt=""
              class="brand-mark"
            />
            <span class="brand-name">OpenS4League</span>
          </a>
          <nav class="nav" aria-label="Main navigation">
            <a href="#servers">Servers</a>
            <a href="#tools">Tools</a>
            <a href="#status">Status</a>
            <a href="#credits">Credits</a>
          </nav>
          <a
            class="header-cta"
            href="https://github.com/Nergy101/OpenS4League"
            target="_blank"
            rel="noreferrer"
          >
            GitHub ↗
          </a>
        </div>
      </header>

      <main id="top">
        <section class="hero">
          <div class="hero-grid" aria-hidden="true">
            <span></span>
            <span></span>
            <span></span>
            <span></span>
            <span></span>
          </div>
          <div class="hero-glow" aria-hidden="true"></div>
          <div class="wrap hero-inner">
            <div class="hero-copy reveal">
              <p class="eyebrow">
                <span class="pulse-dot"></span> COMMUNITY-RUN <i></i>{" "}
                OPEN SOURCE
              </p>
              <h1>
                Open<span>S4</span>League
              </h1>
              <p class="tagline">
                A modern, self-contained rebuild of the{" "}
                <strong>S4 League</strong> server and tooling stack.
              </p>
              <p class="sub">
                An independent, unofficial, non-commercial community emulator —
                four servers rebuilt from scratch on .NET 10, plus a
                cross-platform toolkit for resource files, maps, animations, and
                configs.
              </p>
              <div class="hero-actions">
                <a class="button primary" href="#quickstart">
                  Get started <span>↓</span>
                </a>
                <a
                  class="button secondary"
                  href="https://github.com/Nergy101/OpenS4League"
                  target="_blank"
                  rel="noreferrer"
                >
                  View on GitHub <span>↗</span>
                </a>
              </div>
              <div class="hero-facts">
                <span>
                  <b>.NET 10</b> runtime
                </span>
                <span>
                  <b>2013</b> client compatible
                </span>
                <span>
                  <b>MIT</b> licensed
                </span>
              </div>
            </div>
            <div class="crest-stage reveal" aria-label="OpenS4League crest">
              <div class="crest-ring"></div>
              <img
                src="/logos/os4l-crest-dark.svg"
                alt=""
                class="hero-crest"
              />
              <span class="orbit orbit-one"></span>
              <span class="orbit orbit-two"></span>
            </div>
          </div>
        </section>

        <section class="section intro">
          <div class="wrap intro-grid">
            <div>
              <p class="eyebrow">WHY OPENS4L</p>
              <h2>
                Old-school speed.<br />
                <em>Modern foundations.</em>
              </h2>
            </div>
            <p class="intro-copy">
              The wire protocol stays byte-identical with the real 2013 client,
              while the stack underneath is rebuilt to be readable, portable,
              and yours to run.
            </p>
          </div>
        </section>

        <section id="servers" class="section servers-section">
          <div class="wrap">
            <div class="section-heading">
              <div>
                <p class="eyebrow">THE CORE STACK</p>
                <h2>
                  Four servers. <em>One world.</em>
                </h2>
              </div>
              <span class="section-count">04 / SERVICES</span>
            </div>
            <p class="section-lede">
              Compiled from source on .NET 10. One Docker Compose command away.
            </p>
            <div class="server-grid">
              {servers.map((s) => (
                <article class={`server-card ${s.tone} reveal`} key={s.name}>
                  <div class="card-top">
                    <span class="server-icon">{s.icon}</span>
                    <span class="status-live">
                      <i></i> online
                    </span>
                  </div>
                  <h3>{s.name}</h3>
                  <p>{s.desc}</p>
                  <footer>
                    <code>TCP :{s.port}</code>
                    <span>→</span>
                  </footer>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section class="section protocol">
          <div class="wrap">
            <div class="protocol-panel">
              <div class="protocol-copy">
                <p class="eyebrow">BUILT FOR COMPATIBILITY</p>
                <h2>
                  The same client.<br />
                  <em>A new foundation.</em>
                </h2>
                <p>
                  OpenS4L speaks the protocol the 2013 client already
                  understands. No proprietary client redistribution. No
                  compromises on the wire.
                </p>
              </div>
              <div
                class="protocol-flow"
                aria-label="Client to server protocol flow"
              >
                <div class="flow-node">
                  <span>◉</span>
                  <strong>2013 CLIENT</strong>
                  <small>Original game client</small>
                </div>
                <div class="flow-line">
                  <i></i>
                  <small>PROUDNET-COMPATIBLE</small>
                </div>
                <div class="flow-node active">
                  <span>✦</span>
                  <strong>OPENS4L</strong>
                  <small>Four-server stack</small>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section id="quickstart" class="section quickstart">
          <div class="wrap quick-grid">
            <div>
              <p class="eyebrow">UP AND RUNNING</p>
              <h2>
                One command<br />
                <em>to enter the arena.</em>
              </h2>
              <p class="section-lede">
                Clone the project, bring up the stack, and connect your client.
                Everything is reproducible.
              </p>
            </div>
            <div class="terminal">
              <div class="terminal-bar">
                <span class="terminal-dots">
                  <i></i>
                  <i></i>
                  <i></i>
                </span>
                <span>bash · opens4league</span>
                <button
                  type="button"
                  class="copy-button"
                  aria-label="Copy setup commands"
                  data-copy-text={`git clone https://github.com/Nergy101/OpenS4League\ncd OpenS4League\nmake bootstrap`}
                >
                  copy
                </button>
              </div>
              <pre><code><span class="muted">$</span> git clone https://github.com/Nergy101/OpenS4League<br /><span class="muted">$</span> cd OpenS4League<br /><span class="muted">$</span> make bootstrap<br /><span class="success">✓ stack ready — connect and play</span><span class="cursor">▌</span></code></pre>
            </div>
          </div>
        </section>

        <section
          class="section roadmap-section"
          aria-labelledby="roadmap-title"
        >
          <div class="wrap">
            <div class="section-heading">
              <div>
                <p class="eyebrow">WHAT'S NEXT</p>
                <h2 id="roadmap-title">
                  The road <em>ahead.</em>
                </h2>
              </div>
              <span class="section-count">ROADMAP / 04</span>
            </div>
            <div class="roadmap">
              {roadmap.map((item, index) => (
                <div class={`roadmap-item ${item.status}`} key={item.label}>
                  <span class="roadmap-node">
                    {item.status === "done"
                      ? "✓"
                      : String(index + 1).padStart(2, "0")}
                  </span>
                  <div>
                    <b>{item.label}</b>
                    <small>{item.detail}</small>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section id="tools" class="section tools-section">
          <div class="wrap">
            <div class="section-heading">
              <div>
                <p class="eyebrow">THE TOOLBOX</p>
                <h2>
                  Build the world<br />
                  <em>around the game.</em>
                </h2>
              </div>
              <span class="section-count">12 / TOOLS</span>
            </div>
            <p class="section-lede">
              A cross-platform suite for inspecting, editing, and extending
              every layer of the S4 League resource stack.
            </p>
            <div class="tools-grid">
              {tools.map((t, index) => (
                <article
                  class={`tool-card reveal ${index === 0 ? "featured" : ""}`}
                  key={t.name}
                >
                  <div class="tool-icon">{t.icon}</div>
                  <h3>{t.name}</h3>
                  <ul>
                    {t.items.map((item) => (
                      <li key={item}>
                        <code>{item}</code>
                        <span>↗</span>
                      </li>
                    ))}
                  </ul>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section id="status" class="section status-section">
          <div class="wrap">
            <div class="section-heading">
              <div>
                <p class="eyebrow">PROJECT PULSE</p>
                <h2>
                  Moving fast.<br />
                  <em>Staying open.</em>
                </h2>
              </div>
              <span class="alpha-badge">
                <i></i> ALPHA / 0.1
              </span>
            </div>
            <div class="status-grid">
              <div class="status-panel">
                <div class="status-row">
                  <span>
                    <i class="check">✓</i> Open source
                  </span>
                  <b>PUBLIC</b>
                </div>
                <div class="status-row">
                  <span>
                    <i class="check">✓</i> Protocol parity
                  </span>
                  <b>BYTE-IDENTICAL</b>
                </div>
                <div class="status-row">
                  <span>
                    <i class="check">✓</i> Deployment
                  </span>
                  <b>DOCKER-READY</b>
                </div>
                <div class="status-row">
                  <span>
                    <i class="check">✓</i> Community
                  </span>
                  <b>WELCOME</b>
                </div>
              </div>
              <div class="status-metrics">
                <div>
                  <strong>.NET 10</strong>
                  <span>runtime</span>
                </div>
                <div>
                  <strong>2013</strong>
                  <span>wire protocol</span>
                </div>
                <div>
                  <strong>Docker</strong>
                  <span>deployment</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section id="credits" class="section community-section">
          <div class="wrap">
            <div class="community-card">
              <div>
                <p class="eyebrow">JOIN THE BUILD</p>
                <h2>
                  Keep the legacy<br />
                  <em>in motion.</em>
                </h2>
                <p>
                  Open source, community-run, and built for people who still
                  remember the lobby sound.
                </p>
              </div>
              <div class="community-actions">
                <a
                  class="button primary"
                  href="https://github.com/Nergy101/OpenS4League"
                  target="_blank"
                  rel="noreferrer"
                >
                  Contribute on GitHub ↗
                </a>
                <a class="button secondary" href="#credits">Read the docs →</a>
              </div>
            </div>
            <div class="credits-copy">
              <p>
                OpenS4L builds on prior work by NetspherePirates, wtfblub,
                BlubLib, Nettention, and the MiniLZO contributors.
              </p>
              <p class="disclaimer">
                S4 League and its resource archives, maps, and animation files
                belong to Nexon and/or its licensors. This project does not
                redistribute game content. Licensed under the MIT License.
              </p>
            </div>
          </div>
        </section>
      </main>

      <footer class="site-footer">
        <div class="wrap footer-inner">
          <img
            src="/logos/os4l-crest-flat-dark.svg"
            width="20"
            height="20"
            alt=""
          />
          <span>OpenS4League — unofficial &amp; non-commercial</span>
          <a href="#top">Back to top ↑</a>
        </div>
      </footer>
    </>
  );
});

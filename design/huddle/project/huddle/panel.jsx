// Huddle — peek panel + nudge cards.

const { useState, useEffect, useRef, useCallback } = React;

// Tiny app tile shown on each card
function AppTile({ app, size = 22 }) {
  const meta = APP_META[app] || { mono: "?", tint: "#8aa0b4" };
  return (
    <span
      className="app-tile"
      style={{
        width: size,
        height: size,
        "--tile-tint": meta.tint,
        fontSize: size * 0.42,
      }}
      title={app}
    >
      {meta.mono}
    </span>
  );
}

function ScenarioTag({ scenario, layout }) {
  const s = SCENARIOS[scenario];
  return (
    <span className={"scn-tag scn-" + scenario}>
      <span className="scn-dot" />
      {layout === "compact" ? s.short : s.label}
    </span>
  );
}

function NudgeCard({ n, featured, layout, reduceMotion, onSave, onDismiss, onCopy }) {
  const [leaving, setLeaving] = useState(false);
  const [copied, setCopied] = useState(false);

  const dismiss = () => {
    if (reduceMotion) return onDismiss(n.id);
    setLeaving(true);
    setTimeout(() => onDismiss(n.id), 260);
  };
  const copy = () => {
    onCopy && onCopy(n);
    setCopied(true);
    setTimeout(() => setCopied(false), 1400);
  };

  const cls = [
    "card",
    featured ? "card--featured" : "card--hist",
    "lay-" + layout,
    leaving ? "card--leaving" : "",
    n.fresh ? "card--fresh" : "",
    n.saved ? "card--saved" : "",
  ].join(" ");

  return (
    <article className={cls} data-scenario={n.scenario}>
      <span className="card-rail" />
      <div className="card-head">
        <ScenarioTag scenario={n.scenario} layout={layout} />
        {featured && n.fresh && (
          <span className="fresh-badge"><Icon.spark /> New</span>
        )}
        <span className="card-time">{relTime(n.ago)}</span>
      </div>

      <p className="card-text">{n.text}</p>

      <div className="card-foot">
        <span className="card-src">
          <AppTile app={n.app} size={featured ? 20 : 17} />
          <span className="src-name">{n.app}</span>
        </span>
        <div className="card-actions">
          {n.scenario === "social" && (
            <button className="cact" onClick={copy} title="Copy idea">
              {copied ? <span className="copied-txt">Copied</span> : <Icon.copy />}
            </button>
          )}
          <button
            className={"cact" + (n.saved ? " cact--on" : "")}
            onClick={() => onSave(n.id)}
            title={n.saved ? "Saved" : "Save"}
          >
            {n.saved ? <Icon.bookmarkFill /> : <Icon.bookmark />}
          </button>
          <button className="cact" onClick={dismiss} title="Dismiss">
            <Icon.close />
          </button>
        </div>
      </div>
    </article>
  );
}

const TICK_SECONDS = 18; // demo cadence (real Huddle = 3 min) — fast enough to feel alive

function HuddlePanel({ t }) {
  const layout = t.layout;
  const reduceMotion = t.reduceMotion;

  const [nudges, setNudges] = useState(() =>
    INITIAL_NUDGES.map((n) => ({ ...n }))
  );
  const [paused, setPaused] = useState(false);
  const [filter, setFilter] = useState("all");
  const [secs, setSecs] = useState(TICK_SECONDS);
  const queueRef = useRef([...INCOMING]);

  // countdown → deliver next queued nudge
  useEffect(() => {
    if (paused) return;
    const id = setInterval(() => {
      setSecs((s) => {
        if (s <= 1) {
          deliver();
          return TICK_SECONDS;
        }
        return s - 1;
      });
    }, 1000);
    return () => clearInterval(id);
  }, [paused]);

  const deliver = useCallback(() => {
    const q = queueRef.current;
    if (!q.length) {
      queueRef.current = [...INCOMING]; // loop the demo
    }
    const next = queueRef.current.shift();
    const item = {
      ...next,
      id: next.id + "_" + Date.now(),
      ago: 0,
      saved: false,
      fresh: true,
    };
    setNudges((prev) => {
      const aged = prev.map((p) => ({
        ...p,
        fresh: false,
        ago: p.ago + Math.max(1, Math.round(TICK_SECONDS / 60) || 1) + 2,
      }));
      return [item, ...aged];
    });
    setTimeout(
      () =>
        setNudges((prev) =>
          prev.map((p) => (p.fresh ? { ...p, fresh: false } : p))
        ),
      4200
    );
  }, []);

  const onSave = (id) =>
    setNudges((prev) =>
      prev.map((p) => (p.id === id ? { ...p, saved: !p.saved } : p))
    );
  const onDismiss = (id) =>
    setNudges((prev) => prev.filter((p) => p.id !== id));
  const onCopy = () => {};

  const counts = {
    all: nudges.length,
    social: nudges.filter((n) => n.scenario === "social").length,
    efficiency: nudges.filter((n) => n.scenario === "efficiency").length,
  };
  const visible = nudges.filter(
    (n) => filter === "all" || n.scenario === filter
  );

  const [latest, ...history] = visible;
  const showFeatured = layout !== "stream"; // stream = uniform feed
  const progress = 1 - secs / TICK_SECONDS;

  return (
    <section
      className="huddle-panel"
      data-layout={layout}
      data-paused={paused ? "true" : "false"}
    >
      {/* next-look progress hairline */}
      <div className="look-bar" aria-hidden="true">
        <span
          className="look-bar-fill"
          style={{
            transform: `scaleX(${paused ? 0 : progress})`,
            transition: reduceMotion ? "none" : undefined,
          }}
        />
      </div>

      <header className="panel-head">
        <div className="brand">
          <HuddleMark />
          <div className="brand-txt">
            <div className="brand-name">Huddle</div>
            <div className={"brand-status" + (paused ? " is-paused" : "")}>
              {paused ? (
                <>Paused · not watching</>
              ) : (
                <>
                  <span className="watch-dot" />
                  Watching · next look in 0:{String(secs).padStart(2, "0")}
                </>
              )}
            </div>
          </div>
        </div>
        <div className="head-btns">
          <button
            className={"hbtn" + (paused ? " hbtn--accent" : "")}
            onClick={() => setPaused((p) => !p)}
            title={paused ? "Resume" : "Pause"}
          >
            {paused ? <Icon.play /> : <Icon.pause />}
          </button>
          <button className="hbtn" title="Scenarios & settings">
            <Icon.settings />
          </button>
        </div>
      </header>

      <div className="filters">
        {[
          ["all", "All"],
          ["social", SCENARIOS.social.label],
          ["efficiency", SCENARIOS.efficiency.label],
        ].map(([key, label]) => (
          <button
            key={key}
            className={
              "chip" +
              (filter === key ? " chip--on" : "") +
              (key !== "all" ? " chip-scn chip-" + key : "")
            }
            onClick={() => setFilter(key)}
          >
            {key !== "all" && <span className="chip-dot" />}
            {label}
            <span className="chip-count">{counts[key]}</span>
          </button>
        ))}
      </div>

      <div className="stream">
        {visible.length === 0 && (
          <div className="empty">
            <Icon.spark />
            <p>No {filter === "all" ? "" : SCENARIOS[filter].label.toLowerCase()} nudges right now.</p>
            <span>Huddle is watching — something useful will surface soon.</span>
          </div>
        )}

        {showFeatured && latest && (
          <NudgeCard
            key={latest.id}
            n={latest}
            featured
            layout={layout}
            reduceMotion={reduceMotion}
            onSave={onSave}
            onDismiss={onDismiss}
            onCopy={onCopy}
          />
        )}

        {showFeatured && history.length > 0 && (
          <div className="earlier-div">
            <span>Earlier</span>
          </div>
        )}

        <div className="hist-list">
          {(showFeatured ? history : visible).map((n) => (
            <NudgeCard
              key={n.id}
              n={n}
              featured={!showFeatured && n === visible[0]}
              layout={layout}
              reduceMotion={reduceMotion}
              onSave={onSave}
              onDismiss={onDismiss}
              onCopy={onCopy}
            />
          ))}
        </div>
      </div>
    </section>
  );
}

function HuddleMark() {
  // Three overlapping soft discs — a small group "huddling"
  return (
    <span className="huddle-mark" aria-hidden="true">
      <svg viewBox="0 0 28 28" width="28" height="28">
        <circle cx="10" cy="11" r="6.4" className="hm a" />
        <circle cx="18" cy="11" r="6.4" className="hm b" />
        <circle cx="14" cy="18.5" r="6.4" className="hm c" />
      </svg>
    </span>
  );
}

Object.assign(window, { HuddlePanel, NudgeCard });

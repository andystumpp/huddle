// Huddle — data + icons. Exports to window for cross-script sharing.

// Scenario metadata
const SCENARIOS = {
  social: {
    id: "social",
    label: "Social ideas",
    short: "Social",
    cssVar: "--social",
  },
  efficiency: {
    id: "efficiency",
    label: "Efficiency",
    short: "Efficiency",
    cssVar: "--efficiency",
  },
};

// The featured (latest) nudge + scrollable history, newest first.
// Voice: a dry, observant kibitzer. Specific, second-person, never generic.
const INITIAL_NUDGES = [
  {
    id: "n1",
    scenario: "efficiency",
    text: "You've alt-tabbed between VS Code and Chrome 14 times in six minutes. It's one task — snap them side by side and stop paying the switch tax.",
    app: "Code.exe",
    ago: 0,
    saved: false,
  },
  {
    id: "n2",
    scenario: "social",
    text: "You rewrote the North Star sentence three times. That struggle is the post — \u201cI rewrote one sentence 40 times, here's what finally clicked\u201d is a thread people save.",
    app: "Code.exe",
    ago: 4,
    saved: false,
  },
  {
    id: "n3",
    scenario: "efficiency",
    text: "Third time pasting that same import block. Make it a snippet once and never type it again.",
    app: "Code.exe",
    ago: 11,
    saved: false,
  },
  {
    id: "n4",
    scenario: "social",
    text: "Your WinUI-vs-Electron comparison in the notes is already a tweet. Devs love a decisive stack take \u2014 post the table.",
    app: "Notepad",
    ago: 18,
    saved: true,
  },
  {
    id: "n5",
    scenario: "efficiency",
    text: "Nine tabs, all the same MDN page, open for 20 minutes. Bookmark it and close the other eight?",
    app: "Chrome",
    ago: 26,
    saved: false,
  },
  {
    id: "n6",
    scenario: "social",
    text: "You keep reaching for the word \u201cambient.\u201d That's your hook \u2014 \u201csoftware that's there without being in the way\u201d is a strong opening line.",
    app: "Code.exe",
    ago: 35,
    saved: false,
  },
  {
    id: "n7",
    scenario: "efficiency",
    text: "You've scrolled this 600-line file hunting for one function four times. Go-to-Symbol (Ctrl+Shift+O) lands you there in one keystroke.",
    app: "Code.exe",
    ago: 47,
    saved: false,
  },
  {
    id: "n8",
    scenario: "social",
    text: "The kibitzer metaphor in your outline is gold. \u201cI built an AI that kibitzes your workday\u201d is the whole launch tweet.",
    app: "Code.exe",
    ago: 58,
    saved: false,
  },
  {
    id: "n9",
    scenario: "efficiency",
    text: "You drafted nearly the same reply in Slack and in email. Write it once, send the link to the rest.",
    app: "Slack",
    ago: 72,
    saved: false,
  },
];

// Queue delivered as the "next look" countdown fires.
const INCOMING = [
  {
    id: "q1",
    scenario: "efficiency",
    text: "That command's been half-typed in the terminal for two minutes. It's `git rebase -i HEAD~3` \u2014 want it dropped into your shell history?",
    app: "Windows Terminal",
  },
  {
    id: "q2",
    scenario: "social",
    text: "You just laughed at your own commit message. \u201cfix: stop the thing from doing the bad thing\u201d \u2014 screenshot it. Funny commits do numbers.",
    app: "Code.exe",
  },
  {
    id: "q3",
    scenario: "efficiency",
    text: "You've checked the clock four times this paragraph. You're not stuck on the words, you're tired \u2014 a five-minute walk beats a fifth rewrite.",
    app: "Code.exe",
  },
];

function relTime(ago) {
  if (ago <= 0) return "just now";
  if (ago < 60) return ago + "m ago";
  const h = Math.floor(ago / 60);
  const m = ago % 60;
  return m ? `${h}h ${m}m ago` : `${h}h ago`;
}

// --- Icons (currentColor, 1.5 stroke, 16px grid) ---
const Icon = {
  pause: (p) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="currentColor" {...p}>
      <rect x="4" y="3" width="2.6" height="10" rx="1" />
      <rect x="9.4" y="3" width="2.6" height="10" rx="1" />
    </svg>
  ),
  play: (p) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="currentColor" {...p}>
      <path d="M5 3.4c0-.8.86-1.3 1.55-.9l6.1 3.6c.66.4.66 1.4 0 1.8l-6.1 3.6c-.69.4-1.55-.1-1.55-.9z" />
    </svg>
  ),
  settings: (p) => (
    <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <circle cx="8" cy="8" r="2.1" />
      <path d="M8 1.4v1.6M8 13v1.6M14.6 8H13M3 8H1.4M12.7 3.3l-1.1 1.1M4.4 11.6l-1.1 1.1M12.7 12.7l-1.1-1.1M4.4 4.4 3.3 3.3" strokeLinecap="round" />
    </svg>
  ),
  bookmark: (p) => (
    <svg viewBox="0 0 16 16" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <path d="M4 3.2c0-.66.54-1.2 1.2-1.2h5.6c.66 0 1.2.54 1.2 1.2v10.3c0 .5-.57.78-.96.47L8 11.7l-4.04 2.27c-.4.31-.96.03-.96-.47z" strokeLinejoin="round" />
    </svg>
  ),
  bookmarkFill: (p) => (
    <svg viewBox="0 0 16 16" width="15" height="15" fill="currentColor" {...p}>
      <path d="M4 3.2c0-.66.54-1.2 1.2-1.2h5.6c.66 0 1.2.54 1.2 1.2v10.3c0 .5-.57.78-.96.47L8 11.7l-4.04 2.27c-.4.31-.96.03-.96-.47z" />
    </svg>
  ),
  close: (p) => (
    <svg viewBox="0 0 16 16" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.5" {...p}>
      <path d="M4 4l8 8M12 4l-8 8" strokeLinecap="round" />
    </svg>
  ),
  copy: (p) => (
    <svg viewBox="0 0 16 16" width="15" height="15" fill="none" stroke="currentColor" strokeWidth="1.4" {...p}>
      <rect x="5.2" y="5.2" width="8" height="8" rx="1.4" />
      <path d="M10.8 5.2V3.6c0-.66-.54-1.2-1.2-1.2H3.6c-.66 0-1.2.54-1.2 1.2v6c0 .66.54 1.2 1.2 1.2h1.6" />
    </svg>
  ),
  spark: (p) => (
    <svg viewBox="0 0 16 16" width="14" height="14" fill="currentColor" {...p}>
      <path d="M8 1.2l1.5 4.1c.16.43.5.77.93.93L14.8 8l-4.37 1.55c-.43.16-.77.5-.93.93L8 14.8l-1.5-4.32a1.5 1.5 0 0 0-.93-.93L1.2 8l4.37-1.77c.43-.16.77-.5.93-.93z" />
    </svg>
  ),
  pin: (p) => (
    <svg viewBox="0 0 16 16" width="13" height="13" fill="currentColor" {...p}>
      <path d="M6 1.6h4c.5 0 .8.6.45 1L9.3 4.2v3l1.6 1.4c.5.45.18 1.3-.5 1.3H8.7v3.9c0 .4-.6.6-.85.2L7 12.5l-.85 1.5c-.25.4-.85.2-.85-.2V9.9H3.6c-.68 0-1-.85-.5-1.3L4.7 7.2v-3L3.55 2.6c-.35-.4-.05-1 .45-1z" />
    </svg>
  ),
};

// App glyph shown on each card (tiny monogram tile)
const APP_META = {
  "Code.exe": { mono: "VS", tint: "#3C9DF0" },
  "Chrome": { mono: "Cr", tint: "#E8534B" },
  "Notepad": { mono: "Nt", tint: "#8AA0B4" },
  "Slack": { mono: "Sl", tint: "#C4A1E8" },
  "Windows Terminal": { mono: ">_", tint: "#4ED6A8" },
};

Object.assign(window, {
  SCENARIOS,
  INITIAL_NUDGES,
  INCOMING,
  relTime,
  Icon,
  APP_META,
});

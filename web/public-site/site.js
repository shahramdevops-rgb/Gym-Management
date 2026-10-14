// The public site's small amount of behaviour. Loaded in <head> without defer so the "js" class
// is on <html> before the first paint (the reveal styles hang on it); everything else waits for
// the DOM. The page reads fine without this file: every effect here only adds to it.
// A file, not an inline script: the public site's Content-Security-Policy allows 'self' only.

document.documentElement.classList.add("js");

const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const persianDigits = "۰۱۲۳۴۵۶۷۸۹";

document.addEventListener("DOMContentLoaded", () => {
  openNow();
  torchlight();
  reveal();
  manifesto();
  slots();
  galleryCounter();
  topbar();
});

// ---------- Open now ----------
// The gym is open every day of the year, 7:30 to 23:00 (docs/BUSINESS_RULES.md), in Tehran's
// time whatever the visitor's own clock says.

const OPENS = 7 * 60 + 30;
const CLOSES = 23 * 60;

function minutesInTehran() {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "Asia/Tehran",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(new Date());
  const value = (type) => Number(parts.find((p) => p.type === type).value);
  return value("hour") * 60 + value("minute");
}

function openNow() {
  const statuses = document.querySelectorAll("[data-status]");
  const now = document.querySelector("[data-now]");

  function update() {
    let minutes;
    try {
      minutes = minutesInTehran();
    } catch {
      return; // A browser without time zones: the page simply shows the hours, no status.
    }

    const open = minutes >= OPENS && minutes < CLOSES;
    const text = open ? "الان باز است · تا ۱۱ شب" : "الان بسته است · از ۷:۳۰ صبح باز می‌شود";

    for (const status of statuses) {
      status.classList.toggle("is-closed", !open);
      status.querySelector("[data-status-text]").textContent = text;
      status.hidden = false;
    }

    if (now) {
      now.style.setProperty("--now", String(minutes / (24 * 60)));
      now.hidden = false;
    }
  }

  update();
  setInterval(update, 60_000);
}

// ---------- Torchlight on the carved name ----------
// The light follows the finger or the pointer; left alone for a moment, it drifts by itself.
// It only runs while the name is on screen and the tab is visible.

function torchlight() {
  const name = document.querySelector("[data-torch]");
  if (!name || reduceMotion) return;

  let target = { x: 50, y: 40 };
  const light = { x: 50, y: 40 };
  let lastTouched = -Infinity;
  let onScreen = true;
  let frame = 0;

  function aim(clientX, clientY) {
    const box = name.getBoundingClientRect();
    target = {
      x: ((clientX - box.left) / box.width) * 100,
      y: ((clientY - box.top) / box.height) * 100,
    };
    lastTouched = performance.now();
  }

  window.addEventListener("pointermove", (e) => aim(e.clientX, e.clientY), { passive: true });
  window.addEventListener("touchmove", (e) => aim(e.touches[0].clientX, e.touches[0].clientY), {
    passive: true,
  });

  function tick(time) {
    if (time - lastTouched > 1800) {
      target = {
        x: 50 + 48 * Math.sin(time / 2600),
        y: 45 + 30 * Math.sin(time / 1700),
      };
    }

    light.x += (target.x - light.x) * 0.07;
    light.y += (target.y - light.y) * 0.07;
    name.style.setProperty("--lx", `${light.x.toFixed(2)}%`);
    name.style.setProperty("--ly", `${light.y.toFixed(2)}%`);

    frame = requestAnimationFrame(tick);
  }

  function run() {
    cancelAnimationFrame(frame);
    if (onScreen && !document.hidden) frame = requestAnimationFrame(tick);
  }

  new IntersectionObserver(([entry]) => {
    onScreen = entry.isIntersecting;
    run();
  }).observe(name);
  document.addEventListener("visibilitychange", run);
}

// ---------- Reveal on scroll ----------

function reveal() {
  const items = document.querySelectorAll("[data-reveal]");
  if (!("IntersectionObserver" in window)) {
    items.forEach((item) => item.classList.add("is-in"));
    return;
  }

  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        entry.target.classList.add("is-in");
        observer.unobserve(entry.target);
      }
    },
    { threshold: 0.15 },
  );
  items.forEach((item) => observer.observe(item));
}

// ---------- The manifesto lights up word by word as it scrolls past ----------

function manifesto() {
  const block = document.querySelector("[data-manifesto]");
  if (!block) return;

  const words = block.textContent.trim().split(/\s+/);
  block.textContent = "";
  const spans = words.map((word) => {
    const span = document.createElement("span");
    span.className = "w";
    span.textContent = word;
    block.append(span, " ");
    return span;
  });

  if (reduceMotion) return;

  let queued = false;
  function update() {
    queued = false;
    const box = block.getBoundingClientRect();
    const height = window.innerHeight;
    const progress = (height * 0.85 - box.top) / (box.height + height * 0.3);
    const lit = Math.round(Math.min(1, Math.max(0, progress)) * spans.length);
    spans.forEach((span, i) => span.classList.toggle("is-lit", i < lit));
  }

  window.addEventListener(
    "scroll",
    () => {
      if (!queued) {
        queued = true;
        requestAnimationFrame(update);
      }
    },
    { passive: true },
  );
  update();
}

// ---------- Numbers that land like the reels of a lock ----------
// Each Persian digit becomes a reel of ۰..۹ twice over; on screen, it spins once and stops on
// its digit. Screen readers get the plain number.

function slots() {
  const numbers = document.querySelectorAll("[data-slot]");
  if (!numbers.length) return;

  const reels = [];
  for (const number of numbers) {
    const text = number.textContent;
    number.textContent = "";

    const spoken = document.createElement("span");
    spoken.className = "sr-only";
    spoken.textContent = text;

    const shown = document.createElement("span");
    shown.setAttribute("aria-hidden", "true");

    let index = 0;
    for (const char of text) {
      const digit = persianDigits.indexOf(char);
      if (digit === -1) {
        shown.append(char);
        continue;
      }

      const reel = document.createElement("span");
      reel.className = "reel";
      const strip = document.createElement("span");
      strip.className = "reel__strip";
      for (let i = 0; i < 20; i++) {
        const face = document.createElement("span");
        face.textContent = persianDigits[i % 10];
        strip.append(face);
      }
      strip.style.transitionDelay = `${index * 140}ms`;
      reel.append(strip);
      shown.append(reel);

      reels.push({ strip, stop: digit + 10, number });
      index++;
    }

    number.append(spoken, shown);
  }

  function land(number) {
    for (const reel of reels) {
      if (reel.number === number) reel.strip.style.transform = `translateY(${-1.1 * reel.stop}em)`;
    }
  }

  if (reduceMotion || !("IntersectionObserver" in window)) {
    numbers.forEach(land);
    return;
  }

  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        land(entry.target);
        observer.unobserve(entry.target);
      }
    },
    { threshold: 0.6 },
  );
  numbers.forEach((number) => observer.observe(number));
}

// ---------- Photo counter on the phone's swipeable gallery ----------

function galleryCounter() {
  const gallery = document.querySelector("[data-gallery]");
  const counter = document.querySelector("[data-gallery-index]");
  if (!gallery || !counter || !("IntersectionObserver" in window)) return;

  const items = [...gallery.children];
  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        const n = items.indexOf(entry.target) + 1;
        counter.textContent = String(n)
          .padStart(2, "0")
          .replace(/\d/g, (d) => persianDigits[d]);
      }
    },
    { root: gallery, threshold: 0.6 },
  );
  items.forEach((item) => observer.observe(item));
}

// ---------- The top bar turns solid once the page moves ----------

function topbar() {
  const bar = document.querySelector(".topbar");
  if (!bar) return;
  const update = () => bar.classList.toggle("is-scrolled", window.scrollY > 40);
  window.addEventListener("scroll", update, { passive: true });
  update();
}

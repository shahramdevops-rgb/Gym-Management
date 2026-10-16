// The public site's behaviour. Loaded in <head> without defer so the "js" class is on <html>
// before the first paint (the reveal styles hang on it); everything else waits for the DOM.
// The page reads fine without this file: every effect here only adds to it.
// A file, not an inline script: the public site's Content-Security-Policy allows 'self' only.

document.documentElement.classList.add("js");

const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const persianDigits = "۰۱۲۳۴۵۶۷۸۹";
const toPersian = (n) => String(n).replace(/\d/g, (d) => persianDigits[d]);
const twoDigits = (n) => toPersian(String(n).padStart(2, "0"));

document.addEventListener("DOMContentLoaded", () => {
  openNow();
  heroLight();
  smoke(document.querySelector('[data-smoke="back"]'), "back");
  smoke(document.querySelector('[data-smoke="front"]'), "front");
  slider();
  reveal();
  manifesto();
  features();
  slots();
  tour();
  coaches();
  galleryCounter();
  topbar();
  dock();
});

// One scroll listener for everything that follows the scroll, run at most once per frame.
const scrollJobs = [];
let scrollQueued = false;
function onScroll(job) {
  scrollJobs.push(job);
  job();
}
window.addEventListener(
  "scroll",
  () => {
    if (scrollQueued) return;
    scrollQueued = true;
    requestAnimationFrame(() => {
      scrollQueued = false;
      scrollJobs.forEach((job) => job());
    });
  },
  { passive: true },
);

// Runs `start`/`stop` as an element comes on and off screen, or the tab is hidden.
function whileVisible(element, start, stop) {
  let onScreen = false;
  const update = () => (onScreen && !document.hidden ? start() : stop());
  new IntersectionObserver(([entry]) => {
    onScreen = entry.isIntersecting;
    update();
  }).observe(element);
  document.addEventListener("visibilitychange", update);
}

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

// ---------- The hero's light and depth ----------
// The pointer (or finger) moves a bronze light across the background, the ghost word and the
// carved name, and shifts every layer by its own depth. Left alone, the light drifts by itself.

function heroLight() {
  const hero = document.querySelector("[data-hero]");
  if (!hero || reduceMotion) return;
  const lit = [...hero.querySelectorAll("[data-torch], .hero__ghost")];

  let target = { x: 0.6, y: 0.4 };
  const light = { x: 0.6, y: 0.4 };
  let lastTouched = -Infinity;
  let frame = 0;

  function aim(clientX, clientY) {
    const box = hero.getBoundingClientRect();
    target = { x: (clientX - box.left) / box.width, y: (clientY - box.top) / box.height };
    lastTouched = performance.now();
  }

  hero.addEventListener("pointermove", (e) => aim(e.clientX, e.clientY), { passive: true });
  hero.addEventListener("touchmove", (e) => aim(e.touches[0].clientX, e.touches[0].clientY), {
    passive: true,
  });

  function tick(time) {
    if (time - lastTouched > 1800) {
      target = {
        x: 0.5 + 0.42 * Math.sin(time / 3100),
        y: 0.45 + 0.25 * Math.sin(time / 2100),
      };
    }

    light.x += (target.x - light.x) * 0.06;
    light.y += (target.y - light.y) * 0.06;

    const heroBox = hero.getBoundingClientRect();
    hero.style.setProperty("--hx", `${(light.x * 100).toFixed(2)}%`);
    hero.style.setProperty("--hy", `${(light.y * 100).toFixed(2)}%`);
    hero.style.setProperty("--px", ((light.x - 0.5) * 2).toFixed(3));
    hero.style.setProperty("--py", ((light.y - 0.5) * 2).toFixed(3));

    // Each lit element gets the same light in its own coordinates.
    const clientX = heroBox.left + light.x * heroBox.width;
    const clientY = heroBox.top + light.y * heroBox.height;
    for (const element of lit) {
      const box = element.getBoundingClientRect();
      element.style.setProperty(
        "--lx",
        `${(((clientX - box.left) / box.width) * 100).toFixed(2)}%`,
      );
      element.style.setProperty(
        "--ly",
        `${(((clientY - box.top) / box.height) * 100).toFixed(2)}%`,
      );
    }

    frame = requestAnimationFrame(tick);
  }

  whileVisible(
    hero,
    () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(tick);
    },
    () => cancelAnimationFrame(frame),
  );
}

// ---------- Smoke ----------
// Drawn, not a picture: a few soft puffs, each made once from overlapping bronze-grey
// gradients, drift upwards, turn slowly, fade in and out, and lean with the pointer. The back
// layer sits behind the athlete, the front layer over his legs.

function puffSprite(seed) {
  const size = 256;
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = size;
  const ctx = canvas.getContext("2d");
  let s = seed;
  const random = () => ((s = (s * 16807) % 2147483647) - 1) / 2147483646;

  // Many small clumps, denser at the centre, give the puff a texture instead of a smooth blob.
  for (let i = 0; i < 110; i++) {
    const angle = random() * Math.PI * 2;
    const distance = (random() + random() + random()) * 30;
    const x = size / 2 + Math.cos(angle) * distance;
    const y = size / 2 + Math.sin(angle) * distance * 0.7;
    const r = 8 + random() * 30;
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, "rgba(222, 198, 162, 0.10)");
    g.addColorStop(0.5, "rgba(190, 162, 124, 0.04)");
    g.addColorStop(1, "rgba(160, 132, 98, 0)");
    ctx.fillStyle = g;
    ctx.fillRect(x - r, y - r, r * 2, r * 2);
  }
  return canvas;
}

function smoke(canvas, layer) {
  if (!canvas) return;
  const hero = canvas.closest("[data-hero]");
  const ctx = canvas.getContext("2d");
  const sprites = [puffSprite(7), puffSprite(42), puffSprite(1405)];
  const front = layer === "front";
  let width = 0;
  let height = 0;
  let puffs = [];
  let frame = 0;
  let last = 0;

  function spawn(fresh) {
    const ttl = 14000 + Math.random() * 12000;
    const base = Math.max(width, height) * (front ? 0.36 : 0.3);
    return {
      x: Math.random() * width,
      y: front ? height * (0.55 + Math.random() * 0.6) : height * (0.4 + Math.random() * 0.75),
      size: base * (0.6 + Math.random() * 0.7),
      vx: (Math.random() - 0.5) * 18,
      vy: -(5 + Math.random() * (front ? 10 : 16)),
      angle: Math.random() * Math.PI * 2,
      spin: (Math.random() - 0.5) * 0.12,
      age: fresh ? 0 : Math.random() * ttl,
      ttl,
      alpha: (front ? 0.85 : 0.7) * (0.5 + Math.random() * 0.5),
      sprite: sprites[Math.floor(Math.random() * sprites.length)],
    };
  }

  function resize() {
    const ratio = Math.min(window.devicePixelRatio || 1, 1.5);
    const box = canvas.getBoundingClientRect();
    width = box.width;
    height = box.height;
    canvas.width = Math.round(width * ratio);
    canvas.height = Math.round(height * ratio);
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    const count = width < 700 ? (front ? 9 : 10) : front ? 15 : 18;
    puffs = Array.from({ length: count }, () => spawn(false));
  }

  function draw(dt) {
    ctx.clearRect(0, 0, width, height);
    const wind = Number(hero.style.getPropertyValue("--px")) || 0;
    for (let i = 0; i < puffs.length; i++) {
      const p = puffs[i];
      p.age += dt;
      if (p.age > p.ttl) {
        puffs[i] = spawn(true);
        continue;
      }
      const seconds = dt / 1000;
      p.x += (p.vx + wind * 14) * seconds;
      p.y += p.vy * seconds;
      p.angle += p.spin * seconds;

      const life = p.age / p.ttl;
      ctx.globalAlpha = p.alpha * Math.sin(Math.PI * life);
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(p.angle);
      ctx.drawImage(p.sprite, -p.size / 2, -p.size / 2, p.size, p.size);
      ctx.restore();
    }
    ctx.globalAlpha = 1;
  }

  function tick(time) {
    const dt = last ? Math.min(time - last, 50) : 16;
    last = time;
    draw(dt);
    frame = requestAnimationFrame(tick);
  }

  resize();
  new ResizeObserver(resize).observe(canvas);

  if (reduceMotion) {
    draw(0);
    return;
  }

  whileVisible(
    hero,
    () => {
      cancelAnimationFrame(frame);
      last = 0;
      frame = requestAnimationFrame(tick);
    },
    () => cancelAnimationFrame(frame),
  );
}

// ---------- The hero's slides ----------
// A new athlete every four seconds, with arrows and swiping. In a right-to-left page "next"
// points left and comes from the left, so a swipe to the right goes forward.

function slider() {
  const hero = document.querySelector("[data-hero]");
  if (!hero) return;
  const slides = [...hero.querySelectorAll("[data-slide]")];
  const lines = [...hero.querySelectorAll("[data-line]")];
  const counter = hero.querySelector("[data-slide-index]");
  const progress = hero.querySelector("[data-progress]");
  const interval = 4000;
  let current = 0;
  let timer = 0;
  let playing = false;

  function show(index) {
    current = (index + slides.length) % slides.length;
    slides.forEach((slide, i) => slide.classList.toggle("is-active", i === current));
    lines.forEach((line, i) => line.classList.toggle("is-active", i === current));
    counter.textContent = twoDigits(current + 1);
    schedule();
  }

  function schedule() {
    clearTimeout(timer);
    progress.classList.remove("is-running");
    if (!playing || reduceMotion) return;
    void progress.offsetWidth; // restart the bar's animation
    progress.classList.add("is-running");
    timer = setTimeout(() => show(current + 1), interval);
  }

  hero.querySelector("[data-next]").addEventListener("click", () => show(current + 1));
  hero.querySelector("[data-prev]").addEventListener("click", () => show(current - 1));

  let touchX = null;
  hero.addEventListener("touchstart", (e) => (touchX = e.touches[0].clientX), { passive: true });
  hero.addEventListener(
    "touchend",
    (e) => {
      if (touchX === null) return;
      const dx = e.changedTouches[0].clientX - touchX;
      touchX = null;
      if (Math.abs(dx) > 50) show(current + (dx > 0 ? 1 : -1));
    },
    { passive: true },
  );

  whileVisible(
    hero,
    () => {
      playing = true;
      schedule();
    },
    () => {
      playing = false;
      schedule();
    },
  );
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

// ---------- The line lights up word by word as it scrolls past ----------

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

  onScroll(() => {
    const box = block.getBoundingClientRect();
    const height = window.innerHeight;
    const progress = (height * 0.85 - box.top) / (box.height + height * 0.3);
    const lit = Math.round(Math.min(1, Math.max(0, progress)) * spans.length);
    spans.forEach((span, i) => span.classList.toggle("is-lit", i < lit));
  });
}

// ---------- The four points' signs move only while they are on screen ----------

function features() {
  const row = document.querySelector("[data-features]");
  if (!row) return;
  whileVisible(
    row,
    () => row.classList.add("is-playing"),
    () => row.classList.remove("is-playing"),
  );
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

// ---------- The hall as a walk, driven by the scroll ----------
// The walk from the door, as straight legs with rounded corners; it never crosses itself.
// A third number marks a stop: the index of its name in .tour__names (and of its photo, its
// tick, the plan's data-steps and the spot's data-step). The other points are bends.

const walk = [
  [24, 195, 0], // the door
  [100, 195],
  [100, 238, 1], // massage
  [215, 238],
  [215, 140, 2], // barber
  [240, 140],
  [240, 183],
  [330, 183, 3], // cafe, past the inner door
  [330, 160, 4], // shop
  [388, 160],
  [388, 64, 5], // body analysis
  [388, 30, 6], // lockers
  [404, 30],
  [404, 165],
  [470, 165, 7], // cardio
  [470, 316, 8], // bodybuilding, its four spots
  [655, 316, 9],
  [655, 100, 10],
  [890, 100, 11],
  [995, 100],
  [995, 180, 12], // the figure stage, on the wall
  [890, 180],
  [890, 269, 13], // calisthenics, the upper half of the old CrossFit room
  [890, 355, 14], // crossfit, the lower half
];

const lerp = (a, b, t) => a + (b - a) * t;
const clamp01 = (t) => Math.min(Math.max(t, 0), 1);
const smoothstep = (t) => t * t * (3 - 2 * t);

// Straight legs through the points, each corner rounded with a radius of up to `radius`.
function roundedPath(points, radius = 14) {
  let d = `M${points[0][0]} ${points[0][1]}`;
  for (let i = 1; i < points.length - 1; i++) {
    const [a, p, b] = [points[i - 1], points[i], points[i + 1]];
    const legIn = Math.hypot(p[0] - a[0], p[1] - a[1]);
    const legOut = Math.hypot(b[0] - p[0], b[1] - p[1]);
    const r = Math.min(radius, legIn / 2, legOut / 2);
    const from = [p[0] - ((p[0] - a[0]) / legIn) * r, p[1] - ((p[1] - a[1]) / legIn) * r];
    const to = [p[0] + ((b[0] - p[0]) / legOut) * r, p[1] + ((b[1] - p[1]) / legOut) * r];
    d += ` L${from[0]} ${from[1]} Q${p[0]} ${p[1]} ${to[0]} ${to[1]}`;
  }
  const end = points[points.length - 1];
  return `${d} L${end[0]} ${end[1]}`;
}

function tour() {
  const tour = document.querySelector("[data-tour]");
  if (!tour) return;
  const stage = tour.querySelector(".tour__stage");
  const route = tour.querySelector("[data-route]");
  const walker = tour.querySelector("[data-walker]");
  const count = tour.querySelector("[data-tour-count]");
  const photos = [...tour.querySelectorAll(".tour__photo")];
  const names = [...tour.querySelectorAll(".tour__names > li")];
  const ticks = [...tour.querySelectorAll(".tour__ticks button")];
  const zones = [...tour.querySelectorAll("[data-steps]")];
  const stepsOf = (zone) => zone.dataset.steps.split(" ").map(Number);
  const spots = [...tour.querySelectorAll("[data-step]")];
  const plan = tour.querySelector(".tour__map");
  const last = walk.filter((point) => point[2] !== undefined).length - 1;

  // The line, and how far along it each stop lies: the length of the line up to that stop.
  const along = [0];
  walk.forEach((point, k) => {
    if (k === 0 || point[2] === undefined) return;
    route.setAttribute("d", roundedPath(walk.slice(0, k + 1)));
    along.push(route.getTotalLength());
  });
  const d = roundedPath(walk);
  route.setAttribute("d", d);
  tour.querySelector("[data-route-track]").setAttribute("d", d);
  const length = route.getTotalLength();
  route.style.strokeDasharray = `${length} ${length}`;

  let shown = -1;
  function show(index) {
    if (index === shown) return;
    shown = index;
    const mark = (el, i) => {
      el.classList.toggle("is-active", i === index);
      el.classList.toggle("is-past", i < index);
    };
    photos.forEach(mark);
    names.forEach(mark);
    ticks.forEach((tick, i) => {
      mark(tick, i);
      if (i === index) tick.setAttribute("aria-current", "step");
      else tick.removeAttribute("aria-current");
    });
    zones.forEach((zone) => {
      const steps = stepsOf(zone);
      zone.classList.toggle("is-active", steps.includes(index));
      zone.classList.toggle("is-past", Math.max(...steps) < index);
    });
    spots.forEach((spot) => mark(spot, Number(spot.dataset.step)));
    count.textContent = twoDigits(index);
  }

  const travel = () => tour.offsetHeight - stage.offsetHeight;

  function update() {
    const progress = clamp01(-tour.getBoundingClientRect().top / travel()) * last;
    const i = Math.min(Math.floor(progress), last - 1);
    const f = progress - i;
    // The walker rests at each stop for the first and last quarter of the way between two.
    const t = smoothstep(clamp01((f - 0.25) / 0.5));

    const distance = lerp(along[i], along[i + 1], t);
    route.style.strokeDashoffset = length - distance;
    const p = route.getPointAtLength(distance);
    walker.setAttribute("transform", `translate(${p.x} ${p.y}) scale(1.6)`);

    stage.classList.toggle("is-walking", progress > 0.05);
    show(f < 0.5 ? i : i + 1);
  }

  // A room on the plan or a tick under it scrolls the page to that stop.
  function go(index) {
    const top = window.scrollY + tour.getBoundingClientRect().top + (index / last) * travel();
    window.scrollTo({ top, behavior: reduceMotion ? "auto" : "smooth" });
  }
  ticks.forEach((tick, i) => tick.addEventListener("click", () => go(i)));

  // A room with several stops goes to the spot nearest the click; from the keyboard, each
  // Enter goes to the room's next spot.
  function nearestStep(steps, e) {
    const at = new DOMPoint(e.clientX, e.clientY).matrixTransform(plan.getScreenCTM().inverse());
    const distance = (step) => {
      const spot = spots.find((s) => Number(s.dataset.step) === step);
      return Math.hypot(spot.cx.baseVal.value - at.x, spot.cy.baseVal.value - at.y);
    };
    return steps.reduce((best, step) => (distance(step) < distance(best) ? step : best));
  }
  zones.forEach((zone) => {
    const steps = stepsOf(zone);
    zone.addEventListener("click", (e) => go(steps.length > 1 ? nearestStep(steps, e) : steps[0]));
    zone.addEventListener("keydown", (e) => {
      if (e.key !== "Enter" && e.key !== " ") return;
      e.preventDefault();
      const here = steps.indexOf(shown);
      go(steps[(here + 1) % steps.length]);
    });
  });

  tour.classList.add("is-live");
  onScroll(update);
  window.addEventListener("resize", update);
}

// ---------- The trainers: a deck of cards on a stage ----------
// The card in front is the chosen one; a tap on it turns it over, a tap on a card beside it
// brings that one to the front. Arrows, the keyboard and a swipe go round the deck.

function coaches() {
  const section = document.querySelector("[data-coaches]");
  if (!section) return;
  const deck = section.querySelector(".coaches__deck");
  const ghost = section.querySelector("[data-coach-ghost]");
  const cards = [...section.querySelectorAll("[data-coach]")];
  const nameOf = (card) => card.querySelector(".coach__name").textContent;
  let current = 0;

  function place() {
    const n = cards.length;
    cards.forEach((card, i) => {
      let offset = (i - current + n) % n;
      if (offset > n / 2) offset -= n; // the short way round: one side or the other
      card.style.setProperty("--o", offset);
      card.style.setProperty("--a", Math.abs(offset));
      card.classList.toggle("is-active", offset === 0);
      if (offset !== 0) card.classList.remove("is-flipped");
    });
  }

  function go(index) {
    current = (index + cards.length) % cards.length;
    place();
    ghost.classList.add("is-changing");
    setTimeout(() => {
      ghost.textContent = nameOf(cards[current]);
      ghost.classList.remove("is-changing");
    }, 350);
  }

  let swiped = false;
  cards.forEach((card, i) => {
    card.addEventListener("click", () => {
      if (swiped) return;
      if (i === current) card.classList.toggle("is-flipped");
      else go(i);
    });
    card.addEventListener("keydown", (e) => {
      if (e.key !== "Enter" && e.key !== " ") return;
      e.preventDefault();
      card.click();
    });
  });

  // Right to left: the next trainer is on the left.
  section.querySelector("[data-coach-next]").addEventListener("click", () => go(current + 1));
  section.querySelector("[data-coach-prev]").addEventListener("click", () => go(current - 1));
  section.addEventListener("keydown", (e) => {
    if (e.key === "ArrowLeft") go(current + 1);
    if (e.key === "ArrowRight") go(current - 1);
  });

  let startX = null;
  deck.addEventListener("pointerdown", (e) => {
    startX = e.clientX;
    swiped = false;
  });
  deck.addEventListener("pointerup", (e) => {
    if (startX === null) return;
    const dx = e.clientX - startX;
    startX = null;
    if (Math.abs(dx) < 40) return;
    swiped = true; // the click that follows this swipe is not a tap
    go(current + (dx > 0 ? 1 : -1));
  });

  // The card in front tilts toward the pointer and the light slides over its metal.
  if (!reduceMotion && window.matchMedia("(hover: hover)").matches) {
    cards.forEach((card) => {
      card.addEventListener("pointermove", (e) => {
        if (!card.classList.contains("is-active")) return;
        const box = card.getBoundingClientRect();
        const x = (e.clientX - box.left) / box.width;
        const y = (e.clientY - box.top) / box.height;
        card.style.setProperty("--ty", `${(x - 0.5) * 16}deg`);
        card.style.setProperty("--tx", `${(0.5 - y) * 12}deg`);
        card.style.setProperty("--sx", `${x * 100}%`);
        card.style.setProperty("--sy", `${y * 100}%`);
        card.classList.add("is-tilting");
      });
      card.addEventListener("pointerleave", () => {
        card.style.setProperty("--ty", "0deg");
        card.style.setProperty("--tx", "0deg");
        card.classList.remove("is-tilting");
      });
    });
  }

  place();
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
        if (entry.isIntersecting) counter.textContent = twoDigits(items.indexOf(entry.target) + 1);
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
  onScroll(() => bar.classList.toggle("is-scrolled", window.scrollY > 40));
  menu(bar);
}

// ---------- The phone's menu: the three-line button opens and closes it ----------
// A link, the button again, a tap beside the drawer or Escape closes it. While it is open the
// page under it stays put.

function menu(bar) {
  const button = bar.querySelector("[data-menu-button]");
  const nav = bar.querySelector(".topnav");
  if (!button || !nav) return;

  function toggle(open) {
    bar.classList.toggle("is-open", open);
    document.documentElement.classList.toggle("menu-open", open);
    button.setAttribute("aria-expanded", String(open));
    button.setAttribute("aria-label", open ? "بستن منو" : "منو");
  }

  button.addEventListener("click", () => toggle(!bar.classList.contains("is-open")));
  nav.addEventListener("click", (e) => {
    if (e.target.closest("a")) toggle(false);
  });
  bar.querySelector("[data-menu-shade]")?.addEventListener("click", () => toggle(false));
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && bar.classList.contains("is-open")) {
      toggle(false);
      button.focus();
    }
  });
  // Turned to a computer's width while open: nothing is left to close it
  window.matchMedia("(min-width: 900px)").addEventListener("change", () => toggle(false));
}

// ---------- The phone's bottom bar appears once the hero is behind ----------

function dock() {
  const bar = document.querySelector("[data-dock]");
  const hero = document.querySelector("[data-hero]");
  if (!bar || !hero) return;
  new IntersectionObserver(([entry]) => bar.classList.toggle("is-on", !entry.isIntersecting), {
    threshold: 0.1,
  }).observe(hero);
}

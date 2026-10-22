import * as THREE from "../vendor/three.module.min.js";

const ACCENT = 0x6fc3e8;
const DEEP = 0x1a3a4d;

let renderer = null;
let canvas = null;
let raf = 0;
let views = [];
let container = null;

const box = (w, h, d) => new THREE.BoxGeometry(w, h, d);
const rod = (r, h, seg = 12) => new THREE.CylinderGeometry(r, r, h, seg);
const ring = (r, tube, arc) => new THREE.TorusGeometry(r, tube, 8, 24, arc);
const ball = (r) => new THREE.SphereGeometry(r, 16, 12);
const part = (geometry, position, rotation) => ({ geometry, position, rotation });

/* Фигура на карточке показывает, что делает инструмент: стопка полок для служб,
   щит для защитника, лупа для поиска. Собрано из примитивов, потому что готовую
   модель нельзя скачать, а значит она не встретится больше нигде. */
function partsFor(tool) {
  switch (tool) {
    case "services":
      return [
        part(box(1.5, 0.28, 0.6), [0, 0.6, 0]),
        part(box(1.5, 0.28, 0.6), [0, 0, 0]),
        part(box(1.5, 0.28, 0.6), [0, -0.6, 0])
      ];
    case "defender":
      return [
        part(box(1.05, 0.95, 0.22), [0, 0.3, 0]),
        part(new THREE.ConeGeometry(0.74, 0.8, 4), [0, -0.57, 0], [Math.PI, Math.PI / 4, 0])
      ];
    case "updates":
      return [
        part(ring(0.78, 0.1, Math.PI * 1.45), [0, 0, 0]),
        part(new THREE.ConeGeometry(0.22, 0.44, 12), [0.1, -0.78, 0], [0, 0, -Math.PI / 2])
      ];
    case "store":
      return [
        part(box(1.25, 0.9, 0.72), [0, -0.2, 0]),
        part(ring(0.33, 0.08, Math.PI), [0, 0.25, 0])
      ];
    case "search":
      return [
        part(ring(0.52, 0.1), [0.16, 0.26, 0]),
        part(rod(0.09, 0.8), [-0.32, -0.56, 0], [0, 0, Math.PI / 4])
      ];
    case "network":
      return [
        part(ball(0.3), [0, 0, 0]),
        part(ball(0.19), [0, 0.95, 0]),
        part(ball(0.19), [0.82, -0.48, 0]),
        part(ball(0.19), [-0.82, -0.48, 0]),
        part(rod(0.04, 0.68, 6), [0, 0.5, 0]),
        part(rod(0.04, 0.68, 6), [0.43, -0.25, 0], [0, 0, -Math.PI / 3]),
        part(rod(0.04, 0.68, 6), [-0.43, -0.25, 0], [0, 0, Math.PI / 3])
      ];
    case "memory":
      return [
        part(box(1.7, 0.6, 0.14), [0, 0, 0]),
        part(box(0.3, 0.3, 0.2), [-0.52, 0.06, 0]),
        part(box(0.3, 0.3, 0.2), [-0.02, 0.06, 0]),
        part(box(0.3, 0.3, 0.2), [0.48, 0.06, 0]),
        part(box(1.7, 0.16, 0.14), [0, -0.38, 0])
      ];
    case "dns":
      return [
        part(rod(0.07, 1.8, 8), [0, -0.1, 0]),
        part(box(1.0, 0.26, 0.1), [0.38, 0.52, 0]),
        part(box(0.86, 0.26, 0.1), [-0.31, 0.05, 0])
      ];
    case "godmode":
      return [
        part(ring(0.3, 0.09), [0, 0.62, 0]),
        part(box(0.14, 1.0, 0.14), [0, -0.08, 0]),
        part(box(0.36, 0.13, 0.13), [0.18, -0.36, 0]),
        part(box(0.3, 0.13, 0.13), [0.15, -0.62, 0])
      ];
    case "ctxti":
      return [
        part(box(1.5, 1.0, 0.14), [0, 0, 0]),
        part(box(0.3, 0.09, 0.08), [-0.42, -0.16, 0.08]),
        part(box(0.09, 0.3, 0.08), [-0.6, 0.18, 0.08], [0, 0, Math.PI / 4])
      ];
    case "ctxown":
      return [
        part(box(1.0, 0.75, 0.52), [0, -0.3, 0]),
        part(ring(0.33, 0.09, Math.PI), [0, 0.12, 0]),
        part(rod(0.09, 0.26, 8), [0, -0.28, 0.27], [Math.PI / 2, 0, 0])
      ];
    case "explorer":
      return [
        part(box(1.4, 0.9, 0.5), [0, -0.12, 0]),
        part(box(0.6, 0.2, 0.5), [-0.4, 0.44, 0])
      ];
    default:
      return [part(box(1.1, 1.1, 1.1), [0, 0, 0])];
  }
}

function makeView(element, shape, index) {
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(42, 1, 0.1, 20);
  camera.position.set(0, 0, 4.1);

  scene.add(new THREE.AmbientLight(0x51768d, 1.3));
  const key = new THREE.DirectionalLight(0xbfe4f7, 2.4);
  key.position.set(-2, 3, 4);
  scene.add(key);
  const rim = new THREE.PointLight(ACCENT, 9, 14);
  rim.position.set(2.4, -1.6, 2.4);
  scene.add(rim);

  const group = new THREE.Group();
  const solid = new THREE.MeshStandardMaterial({
    color: DEEP,
    metalness: 0.8,
    roughness: 0.28,
    transparent: true,
    opacity: 0.72,
    flatShading: true
  });
  const wire = new THREE.LineBasicMaterial({ color: ACCENT, transparent: true, opacity: 0.7 });
  const geometries = [];

  for (const piece of partsFor(shape)) {
    const edges = new THREE.EdgesGeometry(piece.geometry, 16);
    geometries.push(piece.geometry, edges);

    const mesh = new THREE.Mesh(piece.geometry, solid);
    const lines = new THREE.LineSegments(edges, wire);
    for (const item of [mesh, lines]) {
      if (piece.position) item.position.set(...piece.position);
      if (piece.rotation) item.rotation.set(...piece.rotation);
      group.add(item);
    }
  }

  group.rotation.set(0.3, index * 0.7, 0);
  scene.add(group);

  return { element, scene, camera, group, wire, solid, geometries, hover: 0, spin: 0.25 + (index % 4) * 0.08 };
}

export function mountToolCards(root) {
  unmountToolCards();
  container = root;

  const targets = [...root.querySelectorAll("[data-shape]")];
  if (!targets.length) return false;

  canvas = document.createElement("canvas");
  canvas.className = "toolcanvas";
  root.appendChild(canvas);

  try {
    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: "low-power" });
  } catch {
    canvas.remove();
    canvas = null;
    return false;
  }
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.setScissorTest(true);

  views = targets.map((el, i) => makeView(el, el.dataset.shape, i));

  targets.forEach((el, i) => {
    const card = el.closest(".toolcard") || el;
    card.addEventListener("mouseenter", () => (views[i].hover = 1));
    card.addEventListener("mouseleave", () => (views[i].hover = 0));
  });

  raf = requestAnimationFrame(frame);
  return true;
}

function frame(now) {
  raf = requestAnimationFrame(frame);
  if (!renderer || !container) return;

  const bounds = container.getBoundingClientRect();
  const width = Math.max(1, Math.floor(bounds.width));
  const height = Math.max(1, Math.floor(bounds.height));

  if (canvas.width !== width || canvas.height !== height) {
    renderer.setSize(width, height, false);
  }

  const time = now * 0.001;

  for (const view of views) {
    const rect = view.element.getBoundingClientRect();
    if (rect.bottom < bounds.top || rect.top > bounds.bottom || rect.width < 2) continue;

    const left = rect.left - bounds.left;
    const bottom = bounds.bottom - rect.bottom;
    const w = Math.floor(rect.width);
    const h = Math.floor(rect.height);

    renderer.setViewport(left, bottom, w, h);
    renderer.setScissor(left, bottom, w, h);

    view.camera.aspect = w / h;
    view.camera.updateProjectionMatrix();

    /* Фигура качается вокруг фронта, а не крутится: предмет, повёрнутый ребром,
       перестаёт читаться, а он здесь для того, чтобы его узнали. */
    const swing = 0.5 + view.hover * 0.35;
    view.group.rotation.y = Math.sin(time * 0.35 + view.spin * 6) * swing;
    view.group.rotation.x = 0.12 + Math.sin(time * 0.25 + view.spin) * 0.08;
    view.group.scale.setScalar(1 + view.hover * 0.08);
    view.wire.opacity = 0.62 + view.hover * 0.3;
    view.solid.opacity = 0.7 + view.hover * 0.18;

    renderer.render(view.scene, view.camera);
  }
}

export function unmountToolCards() {
  cancelAnimationFrame(raf);
  raf = 0;
  for (const view of views) {
    for (const geometry of view.geometries) geometry.dispose();
    view.solid.dispose();
    view.wire.dispose();
  }
  views = [];
  if (renderer) {
    renderer.dispose();
    renderer = null;
  }
  if (canvas) {
    canvas.remove();
    canvas = null;
  }
  container = null;
}

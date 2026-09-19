import * as THREE from "../vendor/three.module.min.js";

const ACCENT = 0x6fc3e8;
const DEEP = 0x1a3a4d;

let renderer = null;
let canvas = null;
let raf = 0;
let views = [];
let container = null;

function geometryFor(shape) {
  switch (shape) {
    case "gear":
      return new THREE.TorusGeometry(0.72, 0.3, 6, 14);
    case "shield":
      return new THREE.OctahedronGeometry(1.05, 0);
    case "refresh":
      return new THREE.TorusGeometry(0.85, 0.2, 10, 30);
    case "box":
      return new THREE.BoxGeometry(1.25, 1.25, 1.25);
    case "search":
      return new THREE.IcosahedronGeometry(1.0, 1);
    case "globe":
      return new THREE.SphereGeometry(1.05, 14, 10);
    case "god":
      return new THREE.DodecahedronGeometry(1.05, 0);
    case "knot":
      return new THREE.TorusKnotGeometry(0.66, 0.24, 90, 10);
    case "memory":
      return new THREE.CapsuleGeometry(0.5, 0.95, 4, 12);
    case "spark":
      return new THREE.TetrahedronGeometry(1.15, 0);
    case "cone":
      return new THREE.ConeGeometry(0.85, 1.6, 7, 1);
    default:
      return new THREE.IcosahedronGeometry(1.0, 0);
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

  const geometry = geometryFor(shape);
  const group = new THREE.Group();

  const solid = new THREE.Mesh(
    geometry,
    new THREE.MeshStandardMaterial({
      color: DEEP,
      metalness: 0.8,
      roughness: 0.28,
      transparent: true,
      opacity: 0.72,
      flatShading: true
    })
  );
  const wire = new THREE.LineSegments(
    new THREE.EdgesGeometry(geometry, 16),
    new THREE.LineBasicMaterial({ color: ACCENT, transparent: true, opacity: 0.7 })
  );

  group.add(solid);
  group.add(wire);
  group.rotation.set(0.5, index * 0.7, 0);
  scene.add(group);

  return { element, scene, camera, group, wire, solid, hover: 0, spin: 0.25 + (index % 4) * 0.08 };
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

    const boost = 1 + view.hover * 2.2;
    view.group.rotation.y += 0.004 * view.spin * boost;
    view.group.rotation.x = 0.42 + Math.sin(time * 0.4 + view.spin) * 0.12;
    view.group.scale.setScalar(1 + view.hover * 0.08);
    view.wire.material.opacity = 0.62 + view.hover * 0.3;
    view.solid.material.opacity = 0.7 + view.hover * 0.18;

    renderer.render(view.scene, view.camera);
  }
}

export function unmountToolCards() {
  cancelAnimationFrame(raf);
  raf = 0;
  for (const view of views) {
    view.solid.geometry.dispose();
    view.solid.material.dispose();
    view.wire.geometry.dispose();
    view.wire.material.dispose();
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

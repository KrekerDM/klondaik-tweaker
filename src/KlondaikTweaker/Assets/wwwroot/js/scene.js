import * as THREE from "../vendor/three.module.min.js";

let renderer = null;
let scene = null;
let camera = null;
let shapes = [];
let dust = null;
let raf = 0;
let running = false;
let energy = 0;
let target = 0;
const pointer = { x: 0, y: 0, tx: 0, ty: 0 };

const ACCENT = 0x6fc3e8;
const DEEP = 0x1a3a4d;
const PALE = 0x2b5670;

function geometryFor(kind) {
  switch (kind) {
    case "knot":
      return new THREE.TorusKnotGeometry(1.05, 0.32, 110, 12);
    case "knot2":
      return new THREE.TorusKnotGeometry(0.95, 0.26, 120, 10, 3, 4);
    case "ico":
      return new THREE.IcosahedronGeometry(1.35, 0);
    case "ico2":
      return new THREE.IcosahedronGeometry(1.15, 1);
    case "octa":
      return new THREE.OctahedronGeometry(1.2, 0);
    case "dodeca":
      return new THREE.DodecahedronGeometry(1.05, 0);
    case "tetra":
      return new THREE.TetrahedronGeometry(1.25, 0);
    case "torus":
      return new THREE.TorusGeometry(1.15, 0.17, 12, 44);
    case "torus2":
      return new THREE.TorusGeometry(0.9, 0.3, 8, 28);
    case "cone":
      return new THREE.ConeGeometry(0.95, 1.9, 6, 1);
    case "cylinder":
      return new THREE.CylinderGeometry(0.7, 0.7, 1.7, 8, 1);
    case "box":
      return new THREE.BoxGeometry(1.5, 1.5, 1.5);
    case "capsule":
      return new THREE.CapsuleGeometry(0.6, 1.1, 4, 10);
    default:
      return new THREE.IcosahedronGeometry(1.2, 0);
  }
}

const LAYOUT = [
  { kind: "knot", pos: [-7.6, 2.6, -1.5], scale: 1.15, speed: 0.16 },
  { kind: "ico", pos: [7.9, -2.2, -0.5], scale: 1.2, speed: -0.2 },
  { kind: "octa", pos: [5.4, 3.9, -4.5], scale: 0.95, speed: 0.24 },
  { kind: "torus", pos: [-5.8, -3.8, -3.5], scale: 1.05, speed: -0.14 },
  { kind: "dodeca", pos: [0.6, 5.1, -7], scale: 0.85, speed: 0.3 },
  { kind: "tetra", pos: [-9.6, -1.2, -5.5], scale: 0.9, speed: 0.21 },
  { kind: "knot2", pos: [9.8, 3.4, -6.5], scale: 0.8, speed: -0.26 },
  { kind: "cone", pos: [-3.4, 5.6, -9], scale: 0.75, speed: 0.18 },
  { kind: "ico2", pos: [3.2, -5.4, -6], scale: 0.8, speed: -0.17 },
  { kind: "cylinder", pos: [-11.2, 3.8, -9.5], scale: 0.85, speed: 0.13 },
  { kind: "box", pos: [11.4, -4.6, -8.5], scale: 0.7, speed: -0.22 },
  { kind: "torus2", pos: [-1.8, -6.2, -10], scale: 0.9, speed: 0.27 },
  { kind: "capsule", pos: [6.6, 6.2, -11], scale: 0.8, speed: -0.15 },
  { kind: "octa", pos: [-7.2, 6.6, -12], scale: 0.65, speed: 0.19 },
  { kind: "ico", pos: [12.6, 1.2, -12.5], scale: 0.6, speed: -0.24 },
  { kind: "tetra", pos: [-12.4, -5.2, -13], scale: 0.7, speed: 0.16 },
  { kind: "dodeca", pos: [2.4, 7.4, -14], scale: 0.55, speed: -0.2 },
  { kind: "knot", pos: [-4.2, -7.8, -15], scale: 0.6, speed: 0.12 }
];

function makeShape(spec) {
  const geometry = geometryFor(spec.kind);
  const depth = Math.min(1, Math.max(0, -spec.pos[2] / 16));
  const group = new THREE.Group();

  const solid = new THREE.Mesh(
    geometry,
    new THREE.MeshStandardMaterial({
      color: depth > 0.55 ? PALE : DEEP,
      metalness: 0.72,
      roughness: 0.34,
      transparent: true,
      opacity: 0.55 - depth * 0.22,
      flatShading: true
    })
  );

  const wire = new THREE.LineSegments(
    new THREE.EdgesGeometry(geometry, 18),
    new THREE.LineBasicMaterial({
      color: ACCENT,
      transparent: true,
      opacity: 0.38 - depth * 0.18
    })
  );

  group.add(solid);
  group.add(wire);
  group.position.set(spec.pos[0], spec.pos[1], spec.pos[2]);
  group.scale.setScalar(spec.scale);
  group.rotation.set(Math.random() * Math.PI, Math.random() * Math.PI, Math.random() * Math.PI);
  group.userData = {
    speed: spec.speed,
    base: spec.pos.slice(),
    wire,
    solid,
    depth,
    drift: 0.2 + Math.random() * 0.3,
    phase: Math.random() * Math.PI * 2
  };
  return group;
}

export function initScene(canvas) {
  if (renderer) return true;
  try {
    renderer = new THREE.WebGLRenderer({ canvas, antialias: false, alpha: true, powerPreference: "low-power" });
  } catch {
    return false;
  }
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
  renderer.setSize(window.innerWidth, window.innerHeight, false);

  scene = new THREE.Scene();
  scene.fog = new THREE.FogExp2(0x0b0f14, 0.042);

  camera = new THREE.PerspectiveCamera(48, window.innerWidth / window.innerHeight, 0.1, 120);
  camera.position.set(0, 0, 14);

  scene.add(new THREE.AmbientLight(0x4a6d84, 1.15));

  const key = new THREE.DirectionalLight(0x9fd8f2, 2.1);
  key.position.set(-6, 7, 9);
  scene.add(key);

  const rim = new THREE.PointLight(ACCENT, 30, 46);
  rim.position.set(7, -4, 6);
  scene.add(rim);

  const fill = new THREE.PointLight(0x8fb7cf, 14, 40);
  fill.position.set(-9, 5, 2);
  scene.add(fill);

  shapes = LAYOUT.map(makeShape);
  shapes.forEach((s) => scene.add(s));

  const count = 320;
  const positions = new Float32Array(count * 3);
  for (let i = 0; i < count; i++) {
    positions[i * 3] = (Math.random() - 0.5) * 34;
    positions[i * 3 + 1] = (Math.random() - 0.5) * 22;
    positions[i * 3 + 2] = (Math.random() - 0.5) * 20 - 5;
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
  dust = new THREE.Points(
    geometry,
    new THREE.PointsMaterial({ color: ACCENT, size: 0.035, transparent: true, opacity: 0.45, sizeAttenuation: true })
  );
  scene.add(dust);

  window.addEventListener("resize", onResize);
  window.addEventListener("pointermove", onPointer);
  start();
  return true;
}

function onResize() {
  if (!renderer) return;
  renderer.setSize(window.innerWidth, window.innerHeight, false);
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
}

function onPointer(e) {
  pointer.tx = (e.clientX / window.innerWidth - 0.5) * 2;
  pointer.ty = (e.clientY / window.innerHeight - 0.5) * 2;
}

function frame(now) {
  raf = requestAnimationFrame(frame);
  if (!renderer) return;
  const time = now * 0.001;

  energy += (target - energy) * 0.05;
  pointer.x += (pointer.tx - pointer.x) * 0.045;
  pointer.y += (pointer.ty - pointer.y) * 0.045;

  camera.position.x = pointer.x * 1.1;
  camera.position.y = -pointer.y * 0.7;
  camera.lookAt(0, 0, 0);

  for (const s of shapes) {
    const d = s.userData;
    const speed = d.speed * (1 + energy * 2.6);
    s.rotation.x += speed * 0.004;
    s.rotation.y += speed * 0.0062;
    s.position.y = d.base[1] + Math.sin(time * 0.4 + d.phase) * d.drift;
    s.position.x = d.base[0] + Math.cos(time * 0.26 + d.phase) * d.drift * 0.6;
    d.wire.material.opacity = (0.38 - d.depth * 0.18) + energy * 0.4 + Math.sin(time * 0.8 + d.phase) * 0.04;
    d.solid.material.opacity = (0.55 - d.depth * 0.22) + energy * 0.18;
  }

  if (dust) dust.rotation.y = time * 0.011;

  renderer.render(scene, camera);
}

export function start() {
  if (running || !renderer) return;
  running = true;
  raf = requestAnimationFrame(frame);
}

export function stop() {
  running = false;
  cancelAnimationFrame(raf);
}

export function setEnergy(value) {
  target = Math.max(0, Math.min(1, value));
}

export function pulse(duration = 2600) {
  setEnergy(1);
  setTimeout(() => setEnergy(0), duration);
}

export function setEnabled(enabled) {
  const canvas = document.getElementById("bg");
  if (!canvas) return;
  canvas.style.display = enabled ? "" : "none";
  if (enabled) start();
  else stop();
}

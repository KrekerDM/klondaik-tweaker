import * as THREE from "../vendor/three.module.min.js";

let renderer = null;
let scene = null;
let camera = null;
let shapes = [];
let raf = 0;
let running = false;
let energy = 0;
let target = 0;
const pointer = { x: 0, y: 0, tx: 0, ty: 0 };

const ACCENT = 0x6fc3e8;
const DEEP = 0x1a3a4d;

function makeShape(geometry, position, scale, speed) {
  const group = new THREE.Group();
  const solid = new THREE.Mesh(
    geometry,
    new THREE.MeshStandardMaterial({
      color: DEEP,
      metalness: 0.75,
      roughness: 0.32,
      transparent: true,
      opacity: 0.55,
      flatShading: true
    })
  );
  const wire = new THREE.LineSegments(
    new THREE.EdgesGeometry(geometry, 18),
    new THREE.LineBasicMaterial({ color: ACCENT, transparent: true, opacity: 0.35 })
  );
  group.add(solid);
  group.add(wire);
  group.position.set(position[0], position[1], position[2]);
  group.scale.setScalar(scale);
  group.userData = { speed, base: position.slice(), wire, solid, phase: Math.random() * Math.PI * 2 };
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
  scene.fog = new THREE.FogExp2(0x0b0f14, 0.055);

  camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 0.1, 100);
  camera.position.set(0, 0, 14);

  scene.add(new THREE.AmbientLight(0x4a6d84, 1.1));

  const key = new THREE.DirectionalLight(0x9fd8f2, 2.2);
  key.position.set(-6, 7, 9);
  scene.add(key);

  const rim = new THREE.PointLight(ACCENT, 26, 40);
  rim.position.set(7, -4, 6);
  scene.add(rim);

  shapes = [
    makeShape(new THREE.TorusKnotGeometry(1.1, 0.34, 120, 12), [-6.4, 2.1, -2], 1.25, 0.16),
    makeShape(new THREE.IcosahedronGeometry(1.4, 0), [6.8, -2.4, -1], 1.35, -0.2),
    makeShape(new THREE.OctahedronGeometry(1.1, 0), [4.6, 3.4, -5], 1.0, 0.24),
    makeShape(new THREE.TorusGeometry(1.2, 0.18, 12, 48), [-5.2, -3.4, -4], 1.1, -0.14),
    makeShape(new THREE.DodecahedronGeometry(0.8, 0), [0.4, 4.4, -7], 0.9, 0.3)
  ];
  shapes.forEach((s) => scene.add(s));

  const dust = new THREE.BufferGeometry();
  const count = 220;
  const positions = new Float32Array(count * 3);
  for (let i = 0; i < count; i++) {
    positions[i * 3] = (Math.random() - 0.5) * 28;
    positions[i * 3 + 1] = (Math.random() - 0.5) * 18;
    positions[i * 3 + 2] = (Math.random() - 0.5) * 16 - 4;
  }
  dust.setAttribute("position", new THREE.BufferAttribute(positions, 3));
  const points = new THREE.Points(
    dust,
    new THREE.PointsMaterial({ color: ACCENT, size: 0.035, transparent: true, opacity: 0.5, sizeAttenuation: true })
  );
  scene.add(points);
  shapes.push(points);

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

  camera.position.x = pointer.x * 0.9;
  camera.position.y = -pointer.y * 0.6;
  camera.lookAt(0, 0, 0);

  for (const s of shapes) {
    if (!s.userData || !s.userData.speed) {
      s.rotation.y = time * 0.012;
      continue;
    }
    const d = s.userData;
    const speed = d.speed * (1 + energy * 2.4);
    s.rotation.x += speed * 0.004;
    s.rotation.y += speed * 0.0062;
    s.position.y = d.base[1] + Math.sin(time * 0.45 + d.phase) * 0.28;
    d.wire.material.opacity = 0.3 + energy * 0.45 + Math.sin(time * 0.8 + d.phase) * 0.05;
    d.solid.material.opacity = 0.5 + energy * 0.2;
  }

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

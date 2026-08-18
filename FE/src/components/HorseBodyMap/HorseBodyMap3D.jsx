import { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { CSS2DRenderer, CSS2DObject } from 'three/examples/jsm/renderers/CSS2DRenderer.js';
import './HorseBodyMap.css';
import './HorseBodyMap3D.css';

const SEVERITY_CONFIG = {
    0: { color: '#22c55e', label: 'Healthy' },
    1: { color: '#eab308', label: 'Minor' },
    2: { color: '#f97316', label: 'Moderate' },
    3: { color: '#ef4444', label: 'Severe' },
    4: { color: '#991b1b', label: 'Critical' },
};

// ten mesh trong file GLB -> ten part trong he thong
const MESH_TO_PART = {
    Head: 'Head',
    Neck: 'Neck',
    Shoulder: 'Shoulder',
    Back: 'Back',
    Hip: 'Hip',
    Tail: 'Tail',
    FrontL: 'FrontLeg-Left',
    FrontR: 'FrontLeg-Right',
    HindL: 'HindLeg-Left',
    HindR: 'HindLeg-Right',
    FetlockL: 'Fetlock-Left',
    HindFetlockL: 'Fetlock-Left',
    FetlockR: 'Fetlock-Right',
    HindFetlockR: 'Fetlock-Right',
};

const DISPLAY_LABEL = {
    Head: 'Head', Neck: 'Neck', Shoulder: 'Shoulder', Back: 'Back', Hip: 'Hip', Tail: 'Tail',
    'FrontLeg-Left': 'Front L', 'FrontLeg-Right': 'Front R',
    'HindLeg-Left': 'Hind L', 'HindLeg-Right': 'Hind R',
    'Fetlock-Left': 'Fetlock L', 'Fetlock-Right': 'Fetlock R',
};

const VIEWS = {
    Left: [3.2, 1.3, 0], Right: [-3.2, 1.3, 0],
    Front: [0, 1.3, 3.2], Rear: [0, 1.3, -3.2],
    Top: [0.01, 4.2, 0], '3/4': [2.4, 1.8, 2.0],
};

// canh chi thuoc 1 tam giac = bien cua part -> ve vien nhu SVG
function boundaryLines(geo) {
    const idx = geo.index.array, pos = geo.attributes.position;
    const seen = new Map();
    for (let i = 0; i < idx.length; i += 3)
        for (let k = 0; k < 3; k++) {
            const a = idx[i + k], b = idx[i + ((k + 1) % 3)];
            const key = a < b ? `${a}_${b}` : `${b}_${a}`;
            seen.set(key, (seen.get(key) || 0) + 1);
        }
    const pts = [];
    for (const [key, n] of seen) {
        if (n !== 1) continue;
        const [a, b] = key.split('_').map(Number);
        pts.push(pos.getX(a), pos.getY(a), pos.getZ(a), pos.getX(b), pos.getY(b), pos.getZ(b));
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
    return new THREE.LineSegments(
        g, new THREE.LineBasicMaterial({ color: 0x1f2937, transparent: true, opacity: 0.55 })
    );
}

function HorseBodyMap3D({
    injuredParts = {},
    onPartClick = () => { },
    selectedPart = null,
    modelUrl = '/models/horse_diagram.glb',
    showLabels = true,
}) {
    const mount = useRef(null);
    const api = useRef({ groups: {} });
    const [hover, setHover] = useState(null);
    const [ready, setReady] = useState(false);
    const [view, setView] = useState('Left');

    useEffect(() => {
        const el = mount.current;
        const scene = new THREE.Scene();
        scene.background = new THREE.Color('#fafafa');

        const cam = new THREE.PerspectiveCamera(38, el.clientWidth / el.clientHeight, 0.1, 100);
        cam.position.set(...VIEWS.Left);

        const renderer = new THREE.WebGLRenderer({ antialias: true });
        renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        renderer.setSize(el.clientWidth, el.clientHeight);
        el.appendChild(renderer.domElement);

        const labelR = new CSS2DRenderer();
        labelR.setSize(el.clientWidth, el.clientHeight);
        Object.assign(labelR.domElement.style, {
            position: 'absolute', top: '0', left: '0', pointerEvents: 'none',
        });
        el.appendChild(labelR.domElement);

        scene.add(new THREE.HemisphereLight(0xffffff, 0xc8c8c8, 1.6));
        const key = new THREE.DirectionalLight(0xffffff, 0.8);
        key.position.set(4, 6, 4);
        scene.add(key);

        const ctrl = new OrbitControls(cam, renderer.domElement);
        ctrl.target.set(0, 0.85, 0);
        ctrl.enableDamping = true;
        ctrl.enablePan = false;
        ctrl.minDistance = 1.8;
        ctrl.maxDistance = 6;
        ctrl.maxPolarAngle = Math.PI / 2 + 0.2;

        const groups = {};
        const meshes = [];

        new GLTFLoader().load(modelUrl, (gltf) => {
            gltf.scene.traverse((o) => {
                if (!o.isMesh) return;
                const part = MESH_TO_PART[o.name];
                if (!part) return;
                o.userData.part = part;
                o.material = new THREE.MeshStandardMaterial({
                    color: SEVERITY_CONFIG[0].color, roughness: 0.92, metalness: 0,
                });
                o.add(boundaryLines(o.geometry));
                (groups[part] = groups[part] || []).push(o);
                meshes.push(o);
            });

            if (showLabels) {
                Object.entries(groups).forEach(([part, list]) => {
                    const box = new THREE.Box3();
                    list.forEach((m) => box.expandByObject(m));
                    const div = document.createElement('div');
                    div.className = 'horse3d-label';
                    div.textContent = DISPLAY_LABEL[part] || part;
                    const tag = new CSS2DObject(div);
                    tag.position.copy(list[0].worldToLocal(box.getCenter(new THREE.Vector3())));
                    list[0].add(tag);
                });
            }

            scene.add(gltf.scene);
            api.current.groups = groups;
            setReady(true);
        });

        const ray = new THREE.Raycaster(), ndc = new THREE.Vector2();
        const pick = (e) => {
            const r = renderer.domElement.getBoundingClientRect();
            ndc.set(
                ((e.clientX - r.left) / r.width) * 2 - 1,
                -((e.clientY - r.top) / r.height) * 2 + 1
            );
            ray.setFromCamera(ndc, cam);
            const hit = ray.intersectObjects(meshes, false)[0];
            return hit ? hit.object.userData.part : null;
        };

        let downAt = null;
        const onMove = (e) => {
            const p = pick(e);
            setHover(p);
            renderer.domElement.style.cursor = p ? 'pointer' : 'grab';
            if (downAt && Math.hypot(e.clientX - downAt[0], e.clientY - downAt[1]) > 5)
                api.current.dragged = true;
        };
        const onDown = (e) => { downAt = [e.clientX, e.clientY]; api.current.dragged = false; };
        const onUp = (e) => {
            downAt = null;
            if (api.current.dragged) return;   // dang xoay thi khong tinh la click
            const p = pick(e);
            if (p) onPartClick(p);
        };

        const cv = renderer.domElement;
        cv.addEventListener('pointermove', onMove);
        cv.addEventListener('pointerdown', onDown);
        cv.addEventListener('pointerup', onUp);

        const onResize = () => {
            cam.aspect = el.clientWidth / el.clientHeight;
            cam.updateProjectionMatrix();
            renderer.setSize(el.clientWidth, el.clientHeight);
            labelR.setSize(el.clientWidth, el.clientHeight);
        };
        window.addEventListener('resize', onResize);

        let raf;
        const loop = () => {
            raf = requestAnimationFrame(loop);
            ctrl.update();
            renderer.render(scene, cam);
            labelR.render(scene, cam);
        };
        loop();

        api.current.flyTo = (name) => {
            cam.position.set(...VIEWS[name]);
            ctrl.update();
        };

        return () => {
            cancelAnimationFrame(raf);
            window.removeEventListener('resize', onResize);
            cv.removeEventListener('pointermove', onMove);
            cv.removeEventListener('pointerdown', onDown);
            cv.removeEventListener('pointerup', onUp);
            renderer.dispose();
            el.innerHTML = '';
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [modelUrl, showLabels]);

    // dong bo mau theo du lieu chan doan
    useEffect(() => {
        if (!ready) return;
        Object.entries(api.current.groups).forEach(([part, list]) => {
            const sev = injuredParts[part] ?? 0;
            const color = (SEVERITY_CONFIG[sev] || SEVERITY_CONFIG[0]).color;
            const active = part === hover || part === selectedPart;
            list.forEach((m) => {
                m.material.color.set(color);
                m.material.emissive.set(active ? color : 0x000000);
                m.material.emissiveIntensity = part === selectedPart ? 0.6 : active ? 0.35 : 0;
            });
        });
    }, [injuredParts, hover, selectedPart, ready]);

    const hoverSev = hover ? (injuredParts[hover] ?? 0) : null;

    return (
        <div className="horse-body-map-container">
            <div className="horse-body-map-header">
                <h3 className="horse-body-map-title">Horse Body Map</h3>
                <p className="horse-body-map-subtitle">Click a body part to log or review an injury</p>
            </div>

            <div className="horse3d-stage">
                <div ref={mount} className="horse3d-canvas" />
                {hover && (
                    <div className="horse3d-tooltip">
                        {DISPLAY_LABEL[hover]} - {SEVERITY_CONFIG[hoverSev].label}
                    </div>
                )}
                {!ready && <div className="horse3d-loading">Loading model...</div>}
            </div>

            <div className="horse3d-a11y">
                {Object.keys(DISPLAY_LABEL).map((part) => (
                    <button
                        key={part}
                        type="button"
                        className={`horse3d-chip${selectedPart === part ? ' selected' : ''}`}
                        style={{ borderColor: SEVERITY_CONFIG[injuredParts[part] ?? 0].color }}
                        onMouseEnter={() => setHover(part)}
                        onMouseLeave={() => setHover(null)}
                        onClick={() => onPartClick(part)}
                        aria-label={`${DISPLAY_LABEL[part]} - ${SEVERITY_CONFIG[injuredParts[part] ?? 0].label}`}
                    >
                        {DISPLAY_LABEL[part]}
                    </button>
                ))}
            </div>

            <div className="horse-body-legend">
                <span className="legend-title">Severity:</span>
                {Object.entries(SEVERITY_CONFIG).map(([sev, config]) => (
                    <div key={sev} className="legend-item">
                        <span className="legend-swatch" style={{ backgroundColor: config.color }} />
                        <span className="legend-label">{config.label}</span>
                    </div>
                ))}
            </div>
        </div>
    );
}

export default HorseBodyMap3D;
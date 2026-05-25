// Digital Twin 3D Visualization using Babylon.js
console.log('babylon-scene.js loaded');

let engine = null;
let scene = null;
let camera = null;
let equipmentMeshes = {};
let selectedMeshId = null;

// Color palette for equipment states
const stateColors = {
    'Running': new BABYLON.Color3(0.2, 0.8, 0.2),      // Green
    'Stopped': new BABYLON.Color3(0.5, 0.5, 0.5),      // Gray
    'Maintenance': new BABYLON.Color3(1.0, 0.8, 0.0),  // Yellow
    'Fault': new BABYLON.Color3(0.9, 0.2, 0.2),        // Red
    'Starting': new BABYLON.Color3(0.2, 0.6, 0.9),     // Blue
    'Stopping': new BABYLON.Color3(0.6, 0.4, 0.8)      // Purple
};

// Initialize the Babylon.js scene
window.initBabylonScene = function() {
    const canvas = document.getElementById('renderCanvas');
    if (!canvas) {
        console.error('Canvas not found');
        return;
    }

    // Ensure canvas has proper dimensions
    const parent = canvas.parentElement;
    canvas.width = parent.clientWidth || 800;
    canvas.height = 450;

    engine = new BABYLON.Engine(canvas, true, { preserveDrawingBuffer: true, stencil: true });
    scene = new BABYLON.Scene(engine);
    scene.clearColor = new BABYLON.Color3(0.1, 0.1, 0.15);

    // Camera - arc rotate for 3D navigation
    camera = new BABYLON.ArcRotateCamera(
        'camera',
        Math.PI / 4,
        Math.PI / 3,
        35,
        new BABYLON.Vector3(0, 0, 0),
        scene
    );
    camera.attachControl(canvas, true);
    camera.lowerRadiusLimit = 10;
    camera.upperRadiusLimit = 60;

    // Lighting
    const light1 = new BABYLON.HemisphericLight('light1', new BABYLON.Vector3(0, 1, 0), scene);
    light1.intensity = 0.7;

    const light2 = new BABYLON.DirectionalLight('light2', new BABYLON.Vector3(-1, -2, -1), scene);
    light2.intensity = 0.5;

    // Factory floor
    const floor = BABYLON.MeshBuilder.CreateGround('floor', { width: 30, height: 20 }, scene);
    const floorMat = new BABYLON.StandardMaterial('floorMat', scene);
    floorMat.diffuseColor = new BABYLON.Color3(0.2, 0.2, 0.25);
    floorMat.specularColor = new BABYLON.Color3(0.1, 0.1, 0.1);
    floor.material = floorMat;

    // Zone markers
    createZoneMarker('Zone A\nPumping', -8, -6, scene);
    createZoneMarker('Zone B\nDrive & Material', 0, 3, scene);
    createZoneMarker('Zone C\nCompressors', 10, 0, scene);

    // Click handling
    scene.onPointerDown = function(evt, pickResult) {
        if (pickResult.hit && pickResult.pickedMesh && pickResult.pickedMesh.metadata) {
            const equipmentId = pickResult.pickedMesh.metadata.equipmentId;
            if (equipmentId) {
                selectEquipment(equipmentId);
            }
        }
    };

    // Render loop
    engine.runRenderLoop(function() {
        // Animate running equipment
        for (const id in equipmentMeshes) {
            const mesh = equipmentMeshes[id];
            if (mesh.metadata && mesh.metadata.mode === 'Running') {
                mesh.rotation.y += 0.02;
            }
        }
        scene.render();
    });

    // Handle window resize
    window.addEventListener('resize', function() {
        const parent = canvas.parentElement;
        canvas.width = parent.clientWidth || 800;
        canvas.height = 450;
        engine.resize();
    });

    // Initial resize after a short delay
    setTimeout(function() {
        const parent = canvas.parentElement;
        canvas.width = parent.clientWidth || 800;
        canvas.height = 450;
        engine.resize();
    }, 100);

    console.log('Babylon.js scene initialized, canvas size:', canvas.width, 'x', canvas.height);
    console.log('Engine running:', engine.getRenderingCanvas() !== null);
};

// Create a zone marker label
function createZoneMarker(text, x, z, scene) {
    const plane = BABYLON.MeshBuilder.CreatePlane('zone', { size: 4 }, scene);
    plane.position = new BABYLON.Vector3(x, 0.01, z);
    plane.rotation.x = Math.PI / 2;

    const mat = new BABYLON.StandardMaterial('zoneMat', scene);
    mat.diffuseColor = new BABYLON.Color3(0.3, 0.3, 0.35);
    mat.specularColor = new BABYLON.Color3(0, 0, 0);
    mat.alpha = 0.5;
    plane.material = mat;
}

// Update equipment meshes from data
window.updateEquipment = function(equipmentData) {
    console.log('updateEquipment called with', equipmentData ? equipmentData.length : 0, 'items');
    if (!scene) {
        console.log('Scene not initialized yet');
        return;
    }

    if (equipmentData.length > 0 && Object.keys(equipmentMeshes).length === 0) {
        console.log('First equipment batch, creating meshes...');
    }

    equipmentData.forEach(function(eq) {
        let mesh = equipmentMeshes[eq.id];

        if (!mesh) {
            // Create new mesh based on equipment type
            mesh = createEquipmentMesh(eq, scene);
            equipmentMeshes[eq.id] = mesh;
        }

        // Update position
        mesh.position.x = eq.x;
        mesh.position.y = eq.y + 0.5;
        mesh.position.z = eq.z;

        // Update color based on state
        const color = stateColors[eq.mode] || stateColors['Stopped'];
        if (mesh.material) {
            mesh.material.diffuseColor = color;
            mesh.material.emissiveColor = color.scale(0.2);
        }

        // Update metadata
        mesh.metadata = {
            equipmentId: eq.id,
            name: eq.name,
            type: eq.type,
            mode: eq.mode,
            temperature: eq.temperature,
            rpm: eq.rpm
        };

        // Highlight selected
        if (selectedMeshId === eq.id) {
            mesh.material.emissiveColor = new BABYLON.Color3(0.3, 0.3, 0.5);
        }
    });
};

// Create mesh for equipment type
function createEquipmentMesh(eq, scene) {
    let mesh;
    const matName = 'mat_' + eq.id;
    const mat = new BABYLON.StandardMaterial(matName, scene);
    mat.specularColor = new BABYLON.Color3(0.5, 0.5, 0.5);
    mat.diffuseColor = new BABYLON.Color3(0.5, 0.8, 0.5); // Start with visible green

    switch (eq.type) {
        case 'Pump':
            mesh = BABYLON.MeshBuilder.CreateCylinder(eq.id, { height: 1.5, diameter: 1.0 }, scene);
            break;
        case 'Motor':
            mesh = BABYLON.MeshBuilder.CreateBox(eq.id, { width: 1.2, height: 1.0, depth: 1.8 }, scene);
            break;
        case 'Conveyor':
            mesh = BABYLON.MeshBuilder.CreateBox(eq.id, { width: 4, height: 0.5, depth: 1.0 }, scene);
            break;
        case 'Compressor':
            mesh = BABYLON.MeshBuilder.CreateCylinder(eq.id, { height: 2.0, diameter: 1.5 }, scene);
            break;
        default:
            mesh = BABYLON.MeshBuilder.CreateSphere(eq.id, { diameter: 1.0 }, scene);
    }

    mesh.material = mat;
    console.log('Created mesh for', eq.id, 'type:', eq.type);
    return mesh;
}

// Select equipment and focus camera
window.selectEquipment = function(equipmentId) {
    // Reset previous selection
    if (selectedMeshId && equipmentMeshes[selectedMeshId]) {
        const prevMesh = equipmentMeshes[selectedMeshId];
        const prevColor = stateColors[prevMesh.metadata?.mode] || stateColors['Stopped'];
        prevMesh.material.emissiveColor = prevColor.scale(0.2);
    }

    selectedMeshId = equipmentId;
    const mesh = equipmentMeshes[equipmentId];

    if (mesh) {
        // Highlight selected
        mesh.material.emissiveColor = new BABYLON.Color3(0.3, 0.3, 0.5);

        // Animate camera to focus on equipment
        const targetPos = mesh.position.clone();
        BABYLON.Animation.CreateAndStartAnimation(
            'cameraMove',
            camera,
            'target',
            30,
            30,
            camera.target,
            targetPos,
            BABYLON.Animation.ANIMATIONLOOPMODE_CONSTANT
        );
    }
};

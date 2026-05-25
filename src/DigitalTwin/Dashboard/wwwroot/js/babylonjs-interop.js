// Digital Twin 3D Visualization with Babylon.js
window.digitalTwin3D = {
    engine: null,
    scene: null,
    camera: null,
    equipmentMeshes: {},
    highlightLayer: null,
    selectedEquipment: null,
    dotNetRef: null,

    initialize: function (canvasId, dotNetRef) {
        this.dotNetRef = dotNetRef;
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('Canvas not found:', canvasId);
            return;
        }

        this.engine = new BABYLON.Engine(canvas, true, { preserveDrawingBuffer: true, stencil: true });
        this.scene = this.createScene();

        // Handle resize
        window.addEventListener('resize', () => {
            this.engine.resize();
        });

        // Start render loop
        this.engine.runRenderLoop(() => {
            this.scene.render();
        });

        console.log('Digital Twin 3D initialized');
    },

    createScene: function () {
        const scene = new BABYLON.Scene(this.engine);
        scene.clearColor = new BABYLON.Color4(0.1, 0.1, 0.15, 1);

        // Camera
        this.camera = new BABYLON.ArcRotateCamera('camera', -Math.PI / 2, Math.PI / 3, 30, BABYLON.Vector3.Zero(), scene);
        this.camera.attachControl(this.engine.getRenderingCanvas(), true);
        this.camera.lowerRadiusLimit = 10;
        this.camera.upperRadiusLimit = 50;

        // Lights
        const hemi = new BABYLON.HemisphericLight('hemi', new BABYLON.Vector3(0, 1, 0), scene);
        hemi.intensity = 0.6;

        const dir = new BABYLON.DirectionalLight('dir', new BABYLON.Vector3(-1, -2, -1), scene);
        dir.intensity = 0.4;

        // Floor
        const floor = BABYLON.MeshBuilder.CreateGround('floor', { width: 40, height: 40 }, scene);
        const floorMat = new BABYLON.StandardMaterial('floorMat', scene);
        floorMat.diffuseColor = new BABYLON.Color3(0.2, 0.2, 0.25);
        floorMat.specularColor = new BABYLON.Color3(0.1, 0.1, 0.1);
        floor.material = floorMat;

        // Grid lines
        this.createGridLines(scene);

        // Zone labels
        this.createZoneLabels(scene);

        // Highlight layer for selection
        this.highlightLayer = new BABYLON.HighlightLayer('hl', scene);

        // Click handling
        scene.onPointerDown = (evt, pickResult) => {
            if (pickResult.hit && pickResult.pickedMesh) {
                const equipmentId = pickResult.pickedMesh.metadata?.equipmentId;
                if (equipmentId) {
                    this.selectEquipment(equipmentId);
                }
            }
        };

        return scene;
    },

    createGridLines: function (scene) {
        const gridLines = [];
        for (let i = -20; i <= 20; i += 5) {
            const points1 = [new BABYLON.Vector3(i, 0.01, -20), new BABYLON.Vector3(i, 0.01, 20)];
            const points2 = [new BABYLON.Vector3(-20, 0.01, i), new BABYLON.Vector3(20, 0.01, i)];
            gridLines.push(BABYLON.MeshBuilder.CreateLines('gridX' + i, { points: points1 }, scene));
            gridLines.push(BABYLON.MeshBuilder.CreateLines('gridZ' + i, { points: points2 }, scene));
        }
        gridLines.forEach(line => {
            line.color = new BABYLON.Color3(0.3, 0.3, 0.35);
        });
    },

    createZoneLabels: function (scene) {
        const zones = [
            { name: 'Zone A', position: new BABYLON.Vector3(-12, 0.1, 12) },
            { name: 'Zone B', position: new BABYLON.Vector3(0, 0.1, 12) },
            { name: 'Zone C', position: new BABYLON.Vector3(12, 0.1, 12) }
        ];

        zones.forEach(zone => {
            const plane = BABYLON.MeshBuilder.CreatePlane('label_' + zone.name, { width: 6, height: 1.5 }, scene);
            plane.position = zone.position;
            plane.rotation.x = Math.PI / 2;

            const advancedTexture = BABYLON.GUI.AdvancedDynamicTexture.CreateForMesh(plane);
            const text = new BABYLON.GUI.TextBlock();
            text.text = zone.name;
            text.color = '#888888';
            text.fontSize = 80;
            advancedTexture.addControl(text);
        });
    },

    createEquipment: function (equipment) {
        if (this.equipmentMeshes[equipment.equipmentId]) {
            return; // Already exists
        }

        const position = this.getEquipmentPosition(equipment.equipmentId, equipment.zone);
        let mesh;

        switch (equipment.type) {
            case 0: // Pump
                mesh = this.createPumpMesh(equipment.equipmentId, position);
                break;
            case 1: // Motor
                mesh = this.createMotorMesh(equipment.equipmentId, position);
                break;
            case 2: // Conveyor
                mesh = this.createConveyorMesh(equipment.equipmentId, position);
                break;
            case 3: // Compressor
                mesh = this.createCompressorMesh(equipment.equipmentId, position);
                break;
            default:
                mesh = BABYLON.MeshBuilder.CreateBox(equipment.equipmentId, { size: 1 }, this.scene);
                mesh.position = position;
        }

        mesh.metadata = { equipmentId: equipment.equipmentId };
        this.equipmentMeshes[equipment.equipmentId] = mesh;

        // Add label
        this.createEquipmentLabel(equipment.equipmentId, mesh);
    },

    createPumpMesh: function (id, position) {
        const parent = new BABYLON.TransformNode(id, this.scene);
        parent.position = position;

        // Main body (cylinder)
        const body = BABYLON.MeshBuilder.CreateCylinder(id + '_body', { height: 1.5, diameter: 1.2 }, this.scene);
        body.parent = parent;
        body.position.y = 0.75;

        // Inlet pipe
        const inlet = BABYLON.MeshBuilder.CreateCylinder(id + '_inlet', { height: 1, diameter: 0.3 }, this.scene);
        inlet.parent = parent;
        inlet.rotation.z = Math.PI / 2;
        inlet.position = new BABYLON.Vector3(-0.8, 0.5, 0);

        // Outlet pipe
        const outlet = BABYLON.MeshBuilder.CreateCylinder(id + '_outlet', { height: 1, diameter: 0.3 }, this.scene);
        outlet.parent = parent;
        outlet.rotation.z = Math.PI / 2;
        outlet.position = new BABYLON.Vector3(0.8, 1, 0);

        return parent;
    },

    createMotorMesh: function (id, position) {
        const parent = new BABYLON.TransformNode(id, this.scene);
        parent.position = position;

        // Main body (box)
        const body = BABYLON.MeshBuilder.CreateBox(id + '_body', { width: 1.5, height: 1, depth: 1 }, this.scene);
        body.parent = parent;
        body.position.y = 0.5;

        // Shaft
        const shaft = BABYLON.MeshBuilder.CreateCylinder(id + '_shaft', { height: 0.8, diameter: 0.2 }, this.scene);
        shaft.parent = parent;
        shaft.rotation.z = Math.PI / 2;
        shaft.position = new BABYLON.Vector3(1.1, 0.5, 0);

        return parent;
    },

    createConveyorMesh: function (id, position) {
        const parent = new BABYLON.TransformNode(id, this.scene);
        parent.position = position;

        // Belt (flat box)
        const belt = BABYLON.MeshBuilder.CreateBox(id + '_belt', { width: 3, height: 0.1, depth: 0.8 }, this.scene);
        belt.parent = parent;
        belt.position.y = 0.5;

        // Rollers
        for (let i = -1; i <= 1; i++) {
            const roller = BABYLON.MeshBuilder.CreateCylinder(id + '_roller' + i, { height: 0.9, diameter: 0.3 }, this.scene);
            roller.parent = parent;
            roller.rotation.x = Math.PI / 2;
            roller.position = new BABYLON.Vector3(i * 1.2, 0.3, 0);
        }

        // Supports
        const support1 = BABYLON.MeshBuilder.CreateBox(id + '_sup1', { width: 0.1, height: 0.5, depth: 0.8 }, this.scene);
        support1.parent = parent;
        support1.position = new BABYLON.Vector3(-1.4, 0.25, 0);

        const support2 = BABYLON.MeshBuilder.CreateBox(id + '_sup2', { width: 0.1, height: 0.5, depth: 0.8 }, this.scene);
        support2.parent = parent;
        support2.position = new BABYLON.Vector3(1.4, 0.25, 0);

        return parent;
    },

    createCompressorMesh: function (id, position) {
        const parent = new BABYLON.TransformNode(id, this.scene);
        parent.position = position;

        // Tank (large cylinder)
        const tank = BABYLON.MeshBuilder.CreateCylinder(id + '_tank', { height: 2, diameter: 1.5 }, this.scene);
        tank.parent = parent;
        tank.rotation.z = Math.PI / 2;
        tank.position.y = 0.8;

        // Motor housing
        const motor = BABYLON.MeshBuilder.CreateBox(id + '_motor', { width: 0.8, height: 1, depth: 0.8 }, this.scene);
        motor.parent = parent;
        motor.position = new BABYLON.Vector3(-1.3, 0.5, 0);

        // Pipes
        const pipe = BABYLON.MeshBuilder.CreateCylinder(id + '_pipe', { height: 0.5, diameter: 0.2 }, this.scene);
        pipe.parent = parent;
        pipe.position = new BABYLON.Vector3(1.2, 1.2, 0);

        return parent;
    },

    createEquipmentLabel: function (id, mesh) {
        const plane = BABYLON.MeshBuilder.CreatePlane('label_' + id, { width: 1.5, height: 0.5 }, this.scene);
        plane.parent = mesh;
        plane.position.y = 2.5;
        plane.billboardMode = BABYLON.Mesh.BILLBOARDMODE_ALL;

        const advancedTexture = BABYLON.GUI.AdvancedDynamicTexture.CreateForMesh(plane);
        const text = new BABYLON.GUI.TextBlock();
        text.text = id;
        text.color = 'white';
        text.fontSize = 60;
        advancedTexture.addControl(text);
    },

    getEquipmentPosition: function (id, zone) {
        // Position based on zone and equipment number
        const zoneX = { 'A': -12, 'B': 0, 'C': 12 }[zone] || 0;
        const num = parseInt(id.split('-')[1]) || 1;
        const row = Math.floor((num - 1) / 2);
        const col = (num - 1) % 2;

        return new BABYLON.Vector3(
            zoneX + (col * 4) - 2,
            0,
            8 - (row * 4)
        );
    },

    updateEquipmentState: function (equipmentId, mode, telemetry, anomalies) {
        const mesh = this.equipmentMeshes[equipmentId];
        if (!mesh) return;

        // Get color based on mode
        let color;
        switch (mode) {
            case 4: // Running
                color = new BABYLON.Color3(0.2, 0.8, 0.2); // Green
                break;
            case 5: // Faulted
                color = new BABYLON.Color3(0.9, 0.2, 0.2); // Red
                break;
            case 6: // Maintenance
                color = new BABYLON.Color3(0.2, 0.4, 0.9); // Blue
                break;
            case 0: // Off
                color = new BABYLON.Color3(0.4, 0.4, 0.4); // Gray
                break;
            default:
                color = new BABYLON.Color3(0.9, 0.7, 0.2); // Yellow (Starting/Stopping/Idle)
        }

        // If anomalies present, add red tint
        if (anomalies && anomalies.length > 0) {
            color = new BABYLON.Color3(
                Math.min(1, color.r + 0.3),
                color.g * 0.7,
                color.b * 0.7
            );
        }

        // Apply color to all child meshes
        this.applyColorToMesh(mesh, color);
    },

    applyColorToMesh: function (node, color) {
        if (node.material) {
            node.material.diffuseColor = color;
        }

        const children = node.getChildMeshes();
        children.forEach(child => {
            if (!child.material) {
                child.material = new BABYLON.StandardMaterial(child.name + '_mat', this.scene);
            }
            child.material.diffuseColor = color;
        });
    },

    selectEquipment: function (equipmentId) {
        // Clear previous selection
        if (this.selectedEquipment) {
            const prevMesh = this.equipmentMeshes[this.selectedEquipment];
            if (prevMesh) {
                prevMesh.getChildMeshes().forEach(m => this.highlightLayer.removeMesh(m));
            }
        }

        this.selectedEquipment = equipmentId;

        // Highlight new selection
        const mesh = this.equipmentMeshes[equipmentId];
        if (mesh) {
            mesh.getChildMeshes().forEach(m => {
                this.highlightLayer.addMesh(m, BABYLON.Color3.White());
            });

            // Move camera to focus
            this.focusEquipment(equipmentId);
        }

        // Notify Blazor
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnEquipmentSelected', equipmentId);
        }
    },

    focusEquipment: function (equipmentId) {
        const mesh = this.equipmentMeshes[equipmentId];
        if (!mesh) return;

        const position = mesh.position || mesh.getAbsolutePosition();
        this.camera.setTarget(position);
    },

    dispose: function () {
        if (this.engine) {
            this.engine.dispose();
        }
    }
};

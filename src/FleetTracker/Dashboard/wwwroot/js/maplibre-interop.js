// Fleet Tracker - MapLibre GL JS Interop
// Uses GeoJSON source/layer for smooth updates during map interactions

window.fleetMap = {
    map: null,
    isReady: false,
    pendingVehicles: null,
    vehicleData: {},

    // Initialize the map
    initialize: function(containerId, centerLat, centerLng, zoom) {
        const self = this;
        this.map = new maplibregl.Map({
            container: containerId,
            style: 'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json',
            center: [centerLng, centerLat],
            zoom: zoom
        });

        // Wait for map to be fully loaded
        this.map.on('load', function() {
            // Add vehicle source
            self.map.addSource('vehicles', {
                type: 'geojson',
                data: {
                    type: 'FeatureCollection',
                    features: []
                }
            });

            // Add vehicle circle layer (background)
            self.map.addLayer({
                id: 'vehicle-circles',
                type: 'circle',
                source: 'vehicles',
                paint: {
                    'circle-radius': 12,
                    'circle-color': [
                        'match',
                        ['get', 'status'],
                        'moving', '#4caf50',
                        'stopped', '#ff9800',
                        'idle', '#2196f3',
                        '#9e9e9e'
                    ],
                    'circle-stroke-width': 2,
                    'circle-stroke-color': '#ffffff'
                }
            });

            // Add vehicle symbol layer (icon/text)
            self.map.addLayer({
                id: 'vehicle-labels',
                type: 'symbol',
                source: 'vehicles',
                layout: {
                    'text-field': ['get', 'label'],
                    'text-size': 10,
                    'text-offset': [0, 2],
                    'text-anchor': 'top'
                },
                paint: {
                    'text-color': '#ffffff',
                    'text-halo-color': '#000000',
                    'text-halo-width': 1
                }
            });

            // Add click handler for popups
            self.map.on('click', 'vehicle-circles', function(e) {
                if (e.features.length > 0) {
                    const feature = e.features[0];
                    const coords = feature.geometry.coordinates.slice();
                    const props = feature.properties;

                    new maplibregl.Popup({ offset: 15 })
                        .setLngLat(coords)
                        .setHTML(self.createPopupContent(props))
                        .addTo(self.map);
                }
            });

            // Change cursor on hover
            self.map.on('mouseenter', 'vehicle-circles', function() {
                self.map.getCanvas().style.cursor = 'pointer';
            });
            self.map.on('mouseleave', 'vehicle-circles', function() {
                self.map.getCanvas().style.cursor = '';
            });

            self.isReady = true;
            console.log('Fleet Map ready');

            // Process any pending vehicles
            if (self.pendingVehicles) {
                console.log('Processing pending vehicles:', self.pendingVehicles.length);
                self.updateVehicles(self.pendingVehicles);
                self.pendingVehicles = null;
            }
        });

        // Add navigation controls
        this.map.addControl(new maplibregl.NavigationControl(), 'top-right');

        // Add scale
        this.map.addControl(new maplibregl.ScaleControl({
            maxWidth: 200,
            unit: 'metric'
        }), 'bottom-left');

        console.log('Fleet Map initializing...');
    },

    // Update all vehicle positions using GeoJSON
    updateVehicles: function(vehicles) {
        if (!this.map) {
            console.log('updateVehicles: map not initialized');
            return;
        }

        // Queue updates if map isn't ready yet
        if (!this.isReady) {
            console.log('updateVehicles: map not ready, queuing', vehicles.length, 'vehicles');
            this.pendingVehicles = vehicles;
            return;
        }

        // Reduce logging - only log occasionally
        if (Math.random() < 0.01) {
            console.log('updateVehicles: processing', vehicles.length, 'vehicles');
        }

        // Convert vehicles to GeoJSON features
        const features = vehicles.map(vehicle => {
            // Handle both PascalCase (C#) and camelCase property names
            const id = vehicle.VehicleId || vehicle.vehicleId;
            const lat = vehicle.Latitude || vehicle.latitude;
            const lng = vehicle.Longitude || vehicle.longitude;
            const heading = vehicle.Heading || vehicle.heading || 0;
            const status = vehicle.Status || vehicle.status || 'moving';
            const speed = vehicle.Speed || vehicle.speed || 0;
            const driverName = vehicle.DriverName || vehicle.driverName || 'Unknown';

            // Store full data for popups
            this.vehicleData[id] = vehicle;

            return {
                type: 'Feature',
                geometry: {
                    type: 'Point',
                    coordinates: [lng, lat]
                },
                properties: {
                    id: id,
                    status: status,
                    speed: speed,
                    heading: heading,
                    driverName: driverName,
                    label: id.replace('vehicle-', 'V')
                }
            };
        });

        // Update the source data
        const source = this.map.getSource('vehicles');
        if (source) {
            source.setData({
                type: 'FeatureCollection',
                features: features
            });
        }
    },

    // Focus on a specific vehicle
    focusVehicle: function(vehicleId, lat, lng) {
        if (!this.map || !this.isReady) return;

        this.map.flyTo({
            center: [lng, lat],
            zoom: 14,
            duration: 1000
        });
    },

    // Create popup HTML content
    createPopupContent: function(props) {
        const statusColor = props.status === 'moving' ? '#4caf50' :
                           props.status === 'stopped' ? '#ff9800' : '#2196f3';

        return `
            <div style="font-family: Roboto, sans-serif; padding: 8px; min-width: 180px;">
                <div style="font-weight: 500; font-size: 14px; margin-bottom: 8px;">
                    ${props.id}
                </div>
                <div style="font-size: 12px; color: #888; margin-bottom: 8px;">
                    ${props.driverName}
                </div>
                <div style="display: flex; align-items: center; margin-bottom: 4px;">
                    <span style="background: ${statusColor}; color: white; padding: 2px 8px; border-radius: 12px; font-size: 11px; text-transform: uppercase;">
                        ${props.status}
                    </span>
                </div>
                <table style="font-size: 12px; width: 100%;">
                    <tr><td style="color: #888;">Speed</td><td style="text-align: right;">${parseFloat(props.speed).toFixed(1)} km/h</td></tr>
                    <tr><td style="color: #888;">Heading</td><td style="text-align: right;">${parseFloat(props.heading).toFixed(0)}°</td></tr>
                </table>
            </div>
        `;
    }
};

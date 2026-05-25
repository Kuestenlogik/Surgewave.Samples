// MassFleetTracker - Heatmap visualization for 100k vehicles
// Uses MapLibre GL JS with heatmap layer for efficient rendering

window.fleetHeatmap = {
    map: null,
    isReady: false,

    initialize: function(containerId, centerLat, centerLng, zoom) {
        const self = this;
        console.log('Initializing map in container:', containerId, 'center:', centerLat, centerLng);

        if (typeof maplibregl === 'undefined') {
            console.error('MapLibre GL JS is not loaded!');
            return Promise.reject('MapLibre not loaded');
        }

        this.map = new maplibregl.Map({
            container: containerId,
            style: 'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json',
            center: [centerLng, centerLat],
            zoom: zoom
        });

        // Add controls immediately
        this.map.addControl(new maplibregl.NavigationControl(), 'top-right');
        this.map.addControl(new maplibregl.ScaleControl({ maxWidth: 200, unit: 'metric' }), 'bottom-left');

        return new Promise((resolve) => {
            this.map.on('load', function() {
                // === HEATMAP SOURCE (for aggregated cells) ===
                self.map.addSource('fleet-heatmap', {
                    type: 'geojson',
                    data: { type: 'FeatureCollection', features: [] }
                });

                // === VEHICLES SOURCE (for individual vehicles) ===
                self.map.addSource('fleet-vehicles', {
                    type: 'geojson',
                    data: { type: 'FeatureCollection', features: [] }
                });

                // === HEATMAP LAYER (low zoom) ===
                self.map.addLayer({
                    id: 'fleet-heat',
                    type: 'heatmap',
                    source: 'fleet-heatmap',
                    maxzoom: 14,
                    paint: {
                        'heatmap-weight': [
                            'interpolate', ['linear'], ['get', 'count'],
                            0, 0, 1, 0.1, 10, 0.3, 50, 0.6, 100, 1
                        ],
                        'heatmap-intensity': [
                            'interpolate', ['linear'], ['zoom'],
                            8, 0.5, 12, 1, 15, 2
                        ],
                        'heatmap-color': [
                            'interpolate', ['linear'], ['heatmap-density'],
                            0, 'rgba(0, 255, 0, 0)',
                            0.2, 'rgba(0, 255, 0, 0.5)',
                            0.4, 'rgba(255, 255, 0, 0.7)',
                            0.6, 'rgba(255, 136, 0, 0.8)',
                            0.8, 'rgba(255, 0, 0, 0.9)',
                            1, 'rgba(255, 0, 0, 1)'
                        ],
                        'heatmap-radius': [
                            'interpolate', ['linear'], ['zoom'],
                            8, 15, 12, 25, 15, 40
                        ],
                        'heatmap-opacity': [
                            'interpolate', ['linear'], ['zoom'],
                            12, 1, 14, 0
                        ]
                    }
                });

                // === CLUSTER LAYER (medium zoom) ===
                self.map.addLayer({
                    id: 'fleet-clusters',
                    type: 'circle',
                    source: 'fleet-heatmap',
                    minzoom: 12,
                    maxzoom: 15,
                    paint: {
                        'circle-radius': [
                            'interpolate', ['linear'], ['get', 'count'],
                            1, 8, 10, 12, 50, 18, 100, 24
                        ],
                        'circle-color': [
                            'interpolate', ['linear'], ['get', 'avgSpeed'],
                            0, '#ff0000', 30, '#ff8800', 50, '#ffff00', 70, '#00ff00'
                        ],
                        'circle-stroke-width': 2,
                        'circle-stroke-color': '#ffffff',
                        'circle-opacity': [
                            'interpolate', ['linear'], ['zoom'],
                            12, 0, 13, 0.8, 14, 0.8, 15, 0
                        ]
                    }
                });

                // === CLUSTER LABELS ===
                self.map.addLayer({
                    id: 'fleet-cluster-labels',
                    type: 'symbol',
                    source: 'fleet-heatmap',
                    minzoom: 13,
                    maxzoom: 15,
                    layout: {
                        'text-field': ['get', 'count'],
                        'text-size': 11,
                        'text-font': ['Open Sans Bold']
                    },
                    paint: {
                        'text-color': '#ffffff'
                    }
                });

                // === INDIVIDUAL VEHICLES LAYER (high zoom) ===
                self.map.addLayer({
                    id: 'fleet-vehicles',
                    type: 'circle',
                    source: 'fleet-vehicles',
                    minzoom: 14,
                    paint: {
                        'circle-radius': [
                            'interpolate', ['linear'], ['zoom'],
                            14, 4, 16, 6, 18, 10
                        ],
                        'circle-color': [
                            'match', ['get', 'status'],
                            0, '#00ff00',  // Moving = green
                            1, '#ff8800',  // Stopped = orange
                            2, '#ffff00',  // Idling = yellow
                            '#888888'      // default/offline = gray
                        ],
                        'circle-stroke-width': 1,
                        'circle-stroke-color': '#ffffff',
                        'circle-opacity': [
                            'interpolate', ['linear'], ['zoom'],
                            14, 0, 15, 0.9
                        ]
                    }
                });

                // === VEHICLE LABELS (very high zoom) ===
                self.map.addLayer({
                    id: 'fleet-vehicle-labels',
                    type: 'symbol',
                    source: 'fleet-vehicles',
                    minzoom: 16,
                    layout: {
                        'text-field': ['get', 'id'],
                        'text-size': 10,
                        'text-offset': [0, 1.5],
                        'text-font': ['Open Sans Regular']
                    },
                    paint: {
                        'text-color': '#ffffff',
                        'text-halo-color': '#000000',
                        'text-halo-width': 1
                    }
                });

                // Click handler for vehicles
                self.map.on('click', 'fleet-vehicles', function(e) {
                    if (e.features.length > 0) {
                        const feature = e.features[0];
                        const props = feature.properties;
                        const coords = feature.geometry.coordinates.slice();

                        const statusNames = ['Moving', 'Stopped', 'Idling', 'Offline'];
                        const statusColors = ['#00ff00', '#ff8800', '#ffff00', '#888888'];

                        new maplibregl.Popup({ offset: 15 })
                            .setLngLat(coords)
                            .setHTML(`
                                <div style="font-family: Roboto, sans-serif; padding: 12px; min-width: 150px; background: #1e1e1e; color: white;">
                                    <div style="font-weight: 500; font-size: 14px; margin-bottom: 8px;">${props.id}</div>
                                    <table style="font-size: 12px; width: 100%;">
                                        <tr>
                                            <td style="color: #888;">Status</td>
                                            <td style="text-align: right; color: ${statusColors[props.status]}; font-weight: bold;">
                                                ${statusNames[props.status]}
                                            </td>
                                        </tr>
                                        <tr>
                                            <td style="color: #888;">Speed</td>
                                            <td style="text-align: right; font-weight: bold;">${parseFloat(props.speed).toFixed(1)} km/h</td>
                                        </tr>
                                    </table>
                                </div>
                            `)
                            .addTo(self.map);
                    }
                });

                self.map.on('mouseenter', 'fleet-vehicles', function() {
                    self.map.getCanvas().style.cursor = 'pointer';
                });
                self.map.on('mouseleave', 'fleet-vehicles', function() {
                    self.map.getCanvas().style.cursor = '';
                });

                // Click handler for clusters
                self.map.on('click', 'fleet-clusters', function(e) {
                    if (e.features.length > 0) {
                        const feature = e.features[0];
                        const props = feature.properties;
                        const coords = feature.geometry.coordinates.slice();

                        new maplibregl.Popup({ offset: 15 })
                            .setLngLat(coords)
                            .setHTML(self.createPopupContent(props))
                            .addTo(self.map);
                    }
                });

                self.map.on('mouseenter', 'fleet-clusters', function() {
                    self.map.getCanvas().style.cursor = 'pointer';
                });
                self.map.on('mouseleave', 'fleet-clusters', function() {
                    self.map.getCanvas().style.cursor = '';
                });

                self.isReady = true;
                console.log('MassFleetTracker heatmap ready');
                resolve();
            });
        });
    },

    updateAllFromJson: function(jsonCells, jsonVehicles) {
        const cells = JSON.parse(jsonCells);
        const vehicles = JSON.parse(jsonVehicles);
        this.updateHeatmap(cells);
        this.updateVehicles(vehicles);
    },

    updateHeatmapFromJson: function(jsonData) {
        const data = JSON.parse(jsonData);
        this.updateHeatmap(data);
    },

    updateHeatmap: function(data) {
        if (!this.map || !this.isReady) return;

        let dataArray = data;
        if (data && !Array.isArray(data)) {
            dataArray = Object.values(data);
        }

        if (!dataArray || dataArray.length === 0) return;

        const features = dataArray.map(cell => ({
            type: 'Feature',
            geometry: {
                type: 'Point',
                coordinates: [cell.lon, cell.lat]
            },
            properties: {
                count: cell.count,
                avgSpeed: cell.avgSpeed,
                row: cell.row,
                col: cell.col
            }
        }));

        const source = this.map.getSource('fleet-heatmap');
        if (source) {
            source.setData({
                type: 'FeatureCollection',
                features: features
            });
        }
    },

    updateVehicles: function(data) {
        if (!this.map || !this.isReady) return;

        let dataArray = data;
        if (data && !Array.isArray(data)) {
            dataArray = Object.values(data);
        }

        if (!dataArray || dataArray.length === 0) return;

        const features = dataArray.map(v => ({
            type: 'Feature',
            geometry: {
                type: 'Point',
                coordinates: [v.lon, v.lat]
            },
            properties: {
                id: v.id,
                speed: v.speed,
                status: v.status
            }
        }));

        const source = this.map.getSource('fleet-vehicles');
        if (source) {
            source.setData({
                type: 'FeatureCollection',
                features: features
            });
        }
    },

    createPopupContent: function(props) {
        const speedColor = props.avgSpeed < 20 ? '#ff0000' :
                          props.avgSpeed < 40 ? '#ff8800' :
                          props.avgSpeed < 60 ? '#ffff00' : '#00ff00';

        return `
            <div style="font-family: Roboto, sans-serif; padding: 12px; min-width: 150px; background: #1e1e1e; color: white;">
                <div style="font-weight: 500; font-size: 14px; margin-bottom: 8px;">
                    Cell [${props.row}, ${props.col}]
                </div>
                <table style="font-size: 12px; width: 100%;">
                    <tr>
                        <td style="color: #888;">Vehicles</td>
                        <td style="text-align: right; font-weight: bold;">${props.count}</td>
                    </tr>
                    <tr>
                        <td style="color: #888;">Avg Speed</td>
                        <td style="text-align: right;">
                            <span style="color: ${speedColor}; font-weight: bold;">
                                ${parseFloat(props.avgSpeed).toFixed(1)} km/h
                            </span>
                        </td>
                    </tr>
                </table>
            </div>
        `;
    }
};

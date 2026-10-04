/* =========================================================================
 * PADDOCK PRINCIPAL · 2D INTERACTIVE TRACK MAP (PP-052 / Issue #153)
 * -------------------------------------------------------------------------
 * Top-down 2D canvas track map with:
 * - Smooth pan (mouse drag) and zoom (wheel + controls)
 * - Auto-fit view to circuit bounding box
 * - Follow-selected-car camera mode
 * - Track geometry rendering (road ribbon, curbs, pit lane, S/F line)
 * - Swappable car renderer (dot vs sprite)
 * ========================================================================= */

(() => {
  'use strict';

  // Corner names for Brands Hatch (authentic 1976 GP circuit)
  const CORNER_NAMES = [
    { s: 0.08, name: 'Paddock Hill Bend' },
    { s: 0.17, name: 'Druids' },
    { s: 0.28, name: 'Graham Hill Bend' },
    { s: 0.38, name: 'Surtees' },
    { s: 0.52, name: 'Hawthorns' },
    { s: 0.65, name: 'Westfield' },
    { s: 0.78, name: 'Dingle Dell' },
    { s: 0.88, name: 'Stirling\'s' },
    { s: 0.96, name: 'Clearways' }
  ];

  class RaceMapCanvas {
    constructor(canvasEl, trackSpline, onCarSelect) {
      this.canvas = canvasEl;
      this.ctx = canvasEl.getContext('2d');
      this.spline = trackSpline;
      this.onCarSelect = onCarSelect;

      // Transform state
      this.panX = 0;
      this.panY = 0;
      this.zoom = 1.0;
      this.minZoom = 0.4;
      this.maxZoom = 12.0;

      // Interaction state
      this.isDragging = false;
      this.dragStartX = 0;
      this.dragStartY = 0;
      this.hasDragged = false;
      this.hoveredCarId = null;

      // Follow camera
      this.followMode = false;
      this.selectedCarId = 3; // Default to Jody Scheckter (#3 Elf Tyrrell)

      // Animation / latest frame data
      this.lastFrame = null;
      this.carTrails = new Map(); // carId -> [{x, y, alpha}]
      this.animTime = 0;

      this._initCanvasSize();
      this._bindEvents();
      this.resetView();
    }

    _initCanvasSize() {
      const rect = this.canvas.parentElement.getBoundingClientRect();
      const dpr = window.devicePixelRatio || 1;
      this.width = rect.width;
      this.height = rect.height;
      this.canvas.width = Math.round(rect.width * dpr);
      this.canvas.height = Math.round(rect.height * dpr);
      this.canvas.style.width = rect.width + 'px';
      this.canvas.style.height = rect.height + 'px';
      this.ctx.scale(dpr, dpr);
    }

    handleResize() {
      if (!this.canvas || !this.canvas.parentElement) return;
      const rect = this.canvas.parentElement.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) return;
      const dpr = window.devicePixelRatio || 1;
      this.width = rect.width;
      this.height = rect.height;
      this.canvas.width = Math.round(rect.width * dpr);
      this.canvas.height = Math.round(rect.height * dpr);
      this.canvas.style.width = rect.width + 'px';
      this.canvas.style.height = rect.height + 'px';
      this.ctx.setTransform(1, 0, 0, 1, 0, 0);
      this.ctx.scale(dpr, dpr);
      if (this.lastFrame) this.render(this.lastFrame);
    }

    resetView() {
      const b = this.spline.bounds;
      const padding = 60;
      const availW = Math.max(100, this.width - padding * 2);
      const availH = Math.max(100, this.height - padding * 2);

      const scaleX = availW / b.width;
      const scaleY = availH / b.height;
      this.zoom = Math.min(scaleX, scaleY);

      // Center track in canvas
      const trackCenterX = b.minX + b.width / 2;
      const trackCenterY = b.minY + b.height / 2;
      this.panX = this.width / 2 - trackCenterX * this.zoom;
      this.panY = this.height / 2 - trackCenterY * this.zoom;
    }

    zoomBy(factor, centerX = this.width / 2, centerY = this.height / 2) {
      const oldZoom = this.zoom;
      const newZoom = Math.max(this.minZoom, Math.min(this.maxZoom, oldZoom * factor));
      if (newZoom === oldZoom) return;

      const worldX = (centerX - this.panX) / oldZoom;
      const worldY = (centerY - this.panY) / oldZoom;

      this.zoom = newZoom;
      this.panX = centerX - worldX * newZoom;
      this.panY = centerY - worldY * newZoom;

      if (this.lastFrame) this.render(this.lastFrame);
    }

    setFollowMode(enabled) {
      this.followMode = enabled;
      if (this.lastFrame) this.render(this.lastFrame);
    }

    setSelectedCar(carId) {
      this.selectedCarId = carId;
      if (this.lastFrame) this.render(this.lastFrame);
    }

    _bindEvents() {
      const c = this.canvas;

      // Mouse Drag to Pan
      c.addEventListener('mousedown', (e) => {
        if (e.button !== 0) return;
        this.isDragging = true;
        this.hasDragged = false;
        this.dragStartX = e.clientX;
        this.dragStartY = e.clientY;
      });

      window.addEventListener('mousemove', (e) => {
        const rect = c.getBoundingClientRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;

        if (this.isDragging) {
          const dx = e.clientX - this.dragStartX;
          const dy = e.clientY - this.dragStartY;
          if (Math.hypot(dx, dy) > 4) {
            this.hasDragged = true;
            // Dragging releases follow camera
            this.followMode = false;
            if (this.onFollowChange) this.onFollowChange(false);
          }
          this.panX += dx;
          this.panY += dy;
          this.dragStartX = e.clientX;
          this.dragStartY = e.clientY;
          if (this.lastFrame) this.render(this.lastFrame);
          return;
        }

        // Hover detection on cars
        if (mouseX >= 0 && mouseX <= this.width && mouseY >= 0 && mouseY <= this.height) {
          this._checkCarHover(mouseX, mouseY);
        }
      });

      window.addEventListener('mouseup', (e) => {
        if (!this.isDragging) return;
        this.isDragging = false;
        if (!this.hasDragged) {
          // It was a click: test if user clicked a car
          const rect = c.getBoundingClientRect();
          const clickX = e.clientX - rect.left;
          const clickY = e.clientY - rect.top;
          this._handleCanvasClick(clickX, clickY);
        }
      });

      // Mouse Wheel to Zoom
      c.addEventListener('wheel', (e) => {
        e.preventDefault();
        const rect = c.getBoundingClientRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;
        const factor = e.deltaY < 0 ? 1.18 : 1 / 1.18;
        this.zoomBy(factor, mouseX, mouseY);
      }, { passive: false });

      // Double click to focus
      c.addEventListener('dblclick', (e) => {
        const rect = c.getBoundingClientRect();
        const clickX = e.clientX - rect.left;
        const clickY = e.clientY - rect.top;
        const clickedCar = this._findCarAt(clickX, clickY);
        if (clickedCar) {
          this.selectedCarId = clickedCar.carId;
          this.followMode = true;
          if (this.onFollowChange) this.onFollowChange(true);
          if (this.onCarSelect) this.onCarSelect(clickedCar.carId);
        } else {
          this.resetView();
        }
      });
    }

    _checkCarHover(mouseX, mouseY) {
      const car = this._findCarAt(mouseX, mouseY);
      const newHover = car ? car.carId : null;
      if (this.hoveredCarId !== newHover) {
        this.hoveredCarId = newHover;
        this.canvas.style.cursor = newHover ? 'pointer' : (this.isDragging ? 'grabbing' : 'grab');
        if (this.lastFrame) this.render(this.lastFrame);
      }
    }

    _handleCanvasClick(clickX, clickY) {
      const car = this._findCarAt(clickX, clickY);
      if (car) {
        this.selectedCarId = car.carId;
        this.followMode = true;
        if (this.onFollowChange) this.onFollowChange(true);
        if (this.onCarSelect) this.onCarSelect(car.carId);
      }
    }

    _findCarAt(screenX, screenY) {
      if (!this.lastFrame || !this.lastFrame.cars) return null;
      const hitRadius = Math.max(16, 10 * this.zoom);

      for (const car of this.lastFrame.cars) {
        const pt = this._getCarWorldPos(car);
        const sx = pt.x * this.zoom + this.panX;
        const sy = pt.y * this.zoom + this.panY;
        if (Math.hypot(screenX - sx, screenY - sy) <= hitRadius) {
          return car;
        }
      }
      return null;
    }

    _getCarWorldPos(car) {
      const pt = this.spline.getPointAt(car.distanceNorm);
      // Lane lateral offset in world space
      const offsetScale = 1.6;
      let effectiveOffset = car.laneOffset * offsetScale;

      // Pit lane offset
      if (car.pitState === 'pitting' || car.pitState === 'in_box' || car.pitState === 'exiting') {
        effectiveOffset = -3.2; // shifted inward into pit lane
      }

      return {
        x: pt.x + pt.nx * effectiveOffset,
        y: pt.y + pt.ny * effectiveOffset,
        angle: pt.angle,
        speedKmh: car.speedKmh
      };
    }

    /**
     * Main Render Loop
     */
    render(frame) {
      this.lastFrame = frame;
      this.animTime += 0.03;
      const ctx = this.ctx;
      const W = this.width;
      const H = this.height;

      // Update follow camera
      if (this.followMode && this.selectedCarId) {
        const followed = frame.cars.find(c => c.carId === this.selectedCarId);
        if (followed) {
          const pt = this._getCarWorldPos(followed);
          const targetPanX = W / 2 - pt.x * this.zoom;
          const targetPanY = H / 2 - pt.y * this.zoom;
          // Smooth spring damping towards target
          this.panX += (targetPanX - this.panX) * 0.16;
          this.panY += (targetPanY - this.panY) * 0.16;
        }
      }

      // Clear Canvas
      ctx.save();
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      const dpr = window.devicePixelRatio || 1;
      ctx.scale(dpr, dpr);
      ctx.fillStyle = '#14171d'; // dark telemetry backdrop
      ctx.fillRect(0, 0, W, H);

      // Subtle background grid
      this._drawBackgroundGrid(ctx, W, H);

      // Apply World Camera Transform
      ctx.translate(this.panX, this.panY);
      ctx.scale(this.zoom, this.zoom);

      // 1. Draw Track Runoff & Infield
      this._drawTrackInfield(ctx);

      // 2. Draw Pit Lane
      this._drawPitLane(ctx);

      // 3. Draw Track Ribbon & Curbs
      this._drawTrackRibbon(ctx);

      // 4. Draw Start / Finish Line & Corners
      this._drawTrackDecorations(ctx);

      // 5. Draw Car Trails
      this._drawCarTrails(ctx, frame.cars);

      // 6. Draw Cars (via Swappable Renderer)
      this._drawCars(ctx, frame.cars);

      ctx.restore();

      // Draw Screen Space HUD Overlays (Scale, Corner Tag, Legend)
      this._drawScreenOverlays(ctx, W, H, frame);
    }

    _drawBackgroundGrid(ctx, W, H) {
      const gridSize = 40;
      ctx.save();
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.03)';
      ctx.lineWidth = 1;
      ctx.beginPath();
      for (let x = 0; x < W; x += gridSize) {
        ctx.moveTo(x, 0); ctx.lineTo(x, H);
      }
      for (let y = 0; y < H; y += gridSize) {
        ctx.moveTo(0, y); ctx.lineTo(W, y);
      }
      ctx.stroke();
      ctx.restore();
    }

    _drawTrackInfield(ctx) {
      const samples = this.spline.samples;
      if (!samples || samples.length < 3) return;

      ctx.save();
      ctx.beginPath();
      ctx.moveTo(samples[0].x, samples[0].y);
      for (let i = 1; i < samples.length; i++) {
        ctx.lineTo(samples[i].x, samples[i].y);
      }
      ctx.closePath();
      // Very subtle green/dark infield tint
      ctx.fillStyle = 'rgba(32, 44, 38, 0.22)';
      ctx.fill();
      ctx.restore();
    }

    _drawTrackRibbon(ctx) {
      const samples = this.spline.samples;
      const roadWidth = 6.4;

      ctx.save();
      ctx.beginPath();
      ctx.moveTo(samples[0].x, samples[0].y);
      for (let i = 1; i < samples.length; i++) ctx.lineTo(samples[i].x, samples[i].y);
      ctx.closePath();
      // Red & white alternating dashed curb edges
      ctx.strokeStyle = '#e03a3e';
      ctx.lineWidth = roadWidth + 1.8;
      ctx.setLineDash([2.2, 2.2]);
      ctx.lineCap = 'butt';
      ctx.stroke();

      ctx.strokeStyle = '#ffffff';
      ctx.lineDashOffset = 2.2;
      ctx.stroke();
      ctx.restore();

      // 2. Main Asphalt Surface
      ctx.save();
      ctx.beginPath();
      ctx.moveTo(samples[0].x, samples[0].y);
      for (let i = 1; i < samples.length; i++) {
        ctx.lineTo(samples[i].x, samples[i].y);
      }
      ctx.closePath();
      ctx.strokeStyle = '#282b33'; // Asphalt surface
      ctx.lineWidth = roadWidth;
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.stroke();

      // 3. Track Edge Boundary Lines
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.35)';
      ctx.lineWidth = 0.45;
      ctx.stroke();

      // 4. Center Dashed Guide Line
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.12)';
      ctx.lineWidth = 0.3;
      ctx.setLineDash([1.5, 2.5]);
      ctx.stroke();
      ctx.restore();
    }

    _drawPitLane(ctx) {
      // Pit lane runs along main straight
      const sStart = 0.93;
      const sEnd = 0.07;
      const pitRoadWidth = 3.6;

      ctx.save();
      ctx.beginPath();
      let started = false;

      for (let s = sStart; s <= 1.0; s += 0.005) {
        const pt = this.spline.getPointAt(s);
        const px = pt.x - pt.nx * 3.4;
        const py = pt.y - pt.ny * 3.4;
        if (!started) { ctx.moveTo(px, py); started = true; }
        else ctx.lineTo(px, py);
      }
      for (let s = 0.0; s <= sEnd; s += 0.005) {
        const pt = this.spline.getPointAt(s);
        const px = pt.x - pt.nx * 3.4;
        const py = pt.y - pt.ny * 3.4;
        ctx.lineTo(px, py);
      }

      ctx.strokeStyle = '#22252c';
      ctx.lineWidth = pitRoadWidth;
      ctx.lineCap = 'round';
      ctx.stroke();

      // Pit lane edge line (yellow caution line)
      ctx.strokeStyle = 'rgba(230, 184, 0, 0.55)';
      ctx.lineWidth = 0.35;
      ctx.setLineDash([]);
      ctx.stroke();

      // Pit stalls marker
      const midPt = this.spline.getPointAt(0.99);
      ctx.fillStyle = 'rgba(255, 255, 255, 0.18)';
      ctx.font = 'bold 1.8px sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText('PITS', midPt.x - midPt.nx * 3.4, midPt.y - midPt.ny * 3.4);
      ctx.restore();
    }

    _drawTrackDecorations(ctx) {
      // Start / Finish Line
      const sf = this.spline.samples[0];
      const sfLen = 3.8;

      ctx.save();
      ctx.beginPath();
      ctx.moveTo(sf.x - sf.nx * sfLen, sf.y - sf.ny * sfLen);
      ctx.lineTo(sf.x + sf.nx * sfLen, sf.y + sf.ny * sfLen);
      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 0.9;
      ctx.stroke();

      // Gold S/F accent tick
      ctx.strokeStyle = '#f5c518';
      ctx.lineWidth = 0.35;
      ctx.stroke();

      // Start / Finish Label
      if (this.zoom > 1.2) {
        ctx.fillStyle = '#f5c518';
        ctx.font = 'bold 1.6px Archivo, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('START / FINISH', sf.x + sf.nx * (sfLen + 2.5), sf.y + sf.ny * (sfLen + 2.5));
      }

      // Corner Name Tags (visible at higher zoom levels)
      if (this.zoom > 1.8) {
        ctx.fillStyle = 'rgba(255, 255, 255, 0.45)';
        ctx.font = '500 1.5px Archivo, sans-serif';
        ctx.textAlign = 'center';
        for (const c of CORNER_NAMES) {
          const pt = this.spline.getPointAt(c.s);
          const tx = pt.x + pt.nx * 5.2;
          const ty = pt.y + pt.ny * 5.2;
          ctx.fillText(c.name, tx, ty);
        }
      }
      ctx.restore();
    }

    _drawCarTrails(ctx, cars) {
      // Fading motion trails
      ctx.save();
      for (const car of cars) {
        if (car.status === 'retired' || car.speedKmh < 10) continue;
        const pt = this._getCarWorldPos(car);

        // Store trail history
        if (!this.carTrails.has(car.carId)) this.carTrails.set(car.carId, []);
        const trail = this.carTrails.get(car.carId);
        trail.push({ x: pt.x, y: pt.y, time: this.animTime });
        if (trail.length > 5) trail.shift();

        // Draw trail dots
        for (let i = 0; i < trail.length - 1; i++) {
          const t = trail[i];
          const alpha = (i + 1) / (trail.length * 4);
          ctx.fillStyle = car.livery.primary;
          ctx.globalAlpha = alpha;
          ctx.beginPath();
          ctx.arc(t.x, t.y, 0.8, 0, Math.PI * 2);
          ctx.fill();
        }
      }
      ctx.restore();
    }

    _drawCars(ctx, cars) {
      // Draw in back-to-front order (leader & selected drawn last on top)
      const sorted = [...cars].sort((a, b) => {
        if (a.carId === this.selectedCarId) return 1;
        if (b.carId === this.selectedCarId) return -1;
        if (a.isPlayer) return 1;
        if (b.isPlayer) return -1;
        return a.position - b.position;
      });

      for (const car of sorted) {
        const pt = this._getCarWorldPos(car);
        const isSelected = (car.carId === this.selectedCarId);
        const isHovered = (car.carId === this.hoveredCarId);
        const isPlayer = !!car.isPlayer;

        this.renderCar(ctx, car, pt.x, pt.y, pt.angle, this.zoom, isSelected, isHovered, isPlayer);
      }
    }

    /* =========================================================================
     * RENDERER SWAP POINT (PP-052 / Issue #153)
     * -------------------------------------------------------------------------
     * This function renders a single car onto the 2D track map.
     *
     * CURRENT MVP IMPLEMENTATION:
     * Renders car as a high-contrast dot with authentic 1976 team livery colors,
     * crisp border, driver number when zoomed in, and glowing halo rings for
     * the player's team (Elf Tyrrell) and currently selected/followed driver.
     *
     * FUTURE SWAP POINT (R-FRAMES / Post-MVP continuous simulation):
     * To swap to top-down car sprites, 2D vector chassis, or 3D meshes:
     * Replace the circle drawing block below with sprite/chassis rendering
     * oriented using headingAngle. The input contract:
     *   (ctx, car, worldX, worldY, headingAngle, zoom, isSelected, isHovered, isPlayer)
     * is already complete, deterministic, and fully decoupled from the engine.
     * ========================================================================= */
    renderCar(ctx, car, worldX, worldY, headingAngle, zoom, isSelected, isHovered, isPlayer) {
      ctx.save();
      ctx.translate(worldX, worldY);

      // Dynamic dot radius based on zoom level
      const baseRadius = 1.35;
      const r = Math.max(0.9, Math.min(2.8, baseRadius + (zoom - 1) * 0.18));

      // 1. Player Team (Tyrrell #3 / #4) Ambient Halo
      if (isPlayer) {
        ctx.beginPath();
        ctx.arc(0, 0, r + 1.4, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(31, 79, 154, 0.35)'; // Tyrrell Elf Blue glow
        ctx.fill();
        ctx.strokeStyle = '#e03a3e'; // Tyrrell red accent ring
        ctx.lineWidth = 0.35;
        ctx.stroke();
      }

      // 2. Selected Car Animated Target Reticle / Halo
      if (isSelected) {
        const pulse = 1 + Math.sin(this.animTime * 6) * 0.15;
        ctx.beginPath();
        ctx.arc(0, 0, (r + 1.8) * pulse, 0, Math.PI * 2);
        ctx.strokeStyle = '#f5c518'; // High-contrast gold tracking ring
        ctx.lineWidth = 0.6;
        ctx.stroke();

        // Crosshair ticks
        const tLen = r + 2.8;
        ctx.beginPath();
        ctx.moveTo(0, -tLen); ctx.lineTo(0, -r - 1.2);
        ctx.moveTo(0, tLen); ctx.lineTo(0, r + 1.2);
        ctx.moveTo(-tLen, 0); ctx.lineTo(-r - 1.2, 0);
        ctx.moveTo(tLen, 0); ctx.lineTo(r + 1.2, 0);
        ctx.strokeStyle = '#f5c518';
        ctx.lineWidth = 0.45;
        ctx.stroke();
      }

      // 3. Hover Highlight Ring
      if (isHovered && !isSelected) {
        ctx.beginPath();
        ctx.arc(0, 0, r + 1.1, 0, Math.PI * 2);
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.75)';
        ctx.lineWidth = 0.4;
        ctx.stroke();
      }

      // 4. Car Body Dot (Team Livery Primary Color)
      ctx.beginPath();
      ctx.arc(0, 0, r, 0, Math.PI * 2);
      ctx.fillStyle = car.livery.primary;
      ctx.fill();

      // 5. High-Contrast Border
      ctx.lineWidth = isPlayer ? 0.55 : 0.4;
      ctx.strokeStyle = car.livery.secondary || '#ffffff';
      ctx.stroke();

      // 6. Heading Pointer (subtle nose indicator pointing forward)
      ctx.save();
      ctx.rotate(headingAngle);
      ctx.fillStyle = car.livery.secondary || '#ffffff';
      ctx.beginPath();
      ctx.arc(r - 0.2, 0, 0.45, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();

      // 7. Driver Number (Rendered inside or next to dot when zoomed in)
      if (zoom >= 1.35 || isSelected || isHovered) {
        ctx.fillStyle = car.livery.text || '#ffffff';
        ctx.font = `bold ${Math.max(1.2, r * 1.1)}px Archivo, sans-serif`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(String(car.carId), 0, 0.05);
      }

      // 8. Floating Telemetry Badge for Selected / Hovered Car
      if (isSelected || isHovered) {
        const badgeY = -(r + 3.0);
        const nameText = `#${car.carId} ${car.driverName.split(' ').pop()}`;
        const speedText = `${Math.round(car.speedKmh)} km/h`;

        ctx.font = 'bold 1.4px Archivo, sans-serif';
        const w1 = ctx.measureText(nameText).width;
        ctx.font = '1.2px Archivo, sans-serif';
        const w2 = ctx.measureText(speedText).width;
        const boxW = Math.max(w1, w2) + 2.4;
        const boxH = 4.2;

        ctx.fillStyle = 'rgba(18, 20, 26, 0.9)';
        ctx.strokeStyle = isSelected ? '#f5c518' : 'rgba(255, 255, 255, 0.3)';
        ctx.lineWidth = 0.3;
        ctx.beginPath();
        ctx.roundRect(-boxW / 2, badgeY - boxH / 2, boxW, boxH, 0.8);
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = '#ffffff';
        ctx.font = 'bold 1.35px Archivo, sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'top';
        ctx.fillText(nameText, 0, badgeY - boxH / 2 + 0.6);

        ctx.fillStyle = '#a0aab8';
        ctx.font = '1.15px Archivo, sans-serif';
        ctx.fillText(speedText, 0, badgeY - boxH / 2 + 2.3);
      }

      ctx.restore();
    }
    /* =================== END RENDERER SWAP POINT =================== */

    _drawScreenOverlays(ctx, W, H, frame) {
      // Draw screen space HUD overlays (Zoom scale indicator, followed driver badge)
      ctx.save();

      // Bottom-Left: Selected / Followed Car HUD pill
      if (this.selectedCarId) {
        const sel = frame.cars.find(c => c.carId === this.selectedCarId);
        if (sel) {
          const pillX = 16;
          const pillY = H - 42;
          const pillW = 270;
          ctx.fillStyle = 'rgba(20, 23, 29, 0.88)';
          ctx.strokeStyle = sel.isPlayer ? '#e03a3e' : '#f5c518';
          ctx.lineWidth = 1.5;
          ctx.beginPath();
          ctx.roundRect(pillX, pillY, pillW, 28, 8);
          ctx.fill();
          ctx.stroke();

          // Dot
          ctx.fillStyle = sel.livery.primary;
          ctx.beginPath();
          ctx.arc(pillX + 16, pillY + 14, 6, 0, Math.PI * 2);
          ctx.fill();
          ctx.strokeStyle = '#fff';
          ctx.lineWidth = 1;
          ctx.stroke();

          // Driver label
          ctx.fillStyle = '#ffffff';
          ctx.font = 'bold 12px Archivo, sans-serif';
          ctx.textAlign = 'left';
          ctx.textBaseline = 'middle';
          ctx.fillText(`P${sel.position} · #${sel.carId} ${sel.driverName}`, pillX + 28, pillY + 14);

          // Follow status tag
          ctx.fillStyle = this.followMode ? '#48bb78' : '#a0aec0';
          ctx.font = '10px Archivo, sans-serif';
          ctx.textAlign = 'right';
          ctx.fillText(this.followMode ? 'ŚLEDZENIE' : 'MANUALNY', pillX + pillW - 12, pillY + 14);
        }
      }

      // Bottom-Right: Map Zoom Level Indicator
      ctx.fillStyle = 'rgba(255, 255, 255, 0.35)';
      ctx.font = '11px Archivo, sans-serif';
      ctx.textAlign = 'right';
      ctx.textBaseline = 'bottom';
      ctx.fillText(`Zoom: ${this.zoom.toFixed(1)}x`, W - 16, H - 14);

      ctx.restore();
    }
  }

  window.RaceMapCanvas = RaceMapCanvas;
})();

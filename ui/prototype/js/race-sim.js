/* =========================================================================
 * PADDOCK PRINCIPAL · MOCK RACE SIMULATION ENGINE (PP-052 / Issue #153)
 * -------------------------------------------------------------------------
 * Standalone mock physics & position frame generator.
 * Zero game logic in UI: this module generates discrete position frames
 * conforming to the planned CarFrame stream contract (TECH §3).
 * ========================================================================= */

(() => {
  'use strict';

  // 1976 authentic liveries
  const TEAM_LIVERIES = {
    tyrrell:    { primary: '#1f4f9a', secondary: '#ffffff', accent: '#e03a3e', text: '#ffffff', isPlayer: true },
    ferrari:    { primary: '#c4161c', secondary: '#ffffff', accent: '#f5c518', text: '#ffffff' },
    lotus:      { primary: '#16130e', secondary: '#c9a24a', accent: '#f0dba0', text: '#c9a24a' },
    mclaren:    { primary: '#e03a3e', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    brabham:    { primary: '#c22026', secondary: '#1a2b4c', accent: '#ffffff', text: '#ffffff' },
    march:      { primary: '#f07d20', secondary: '#111111', accent: '#ffffff', text: '#ffffff' },
    shadow:     { primary: '#1f1f1f', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    surtees:    { primary: '#2b59a2', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    williams:   { primary: '#112233', secondary: '#c8aa44', accent: '#c8aa44', text: '#ffffff' },
    ensign:     { primary: '#1a5030', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    ligier:     { primary: '#1d64b4', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    penske:     { primary: '#c82020', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' },
    copersucar: { primary: '#e6b800', secondary: '#006633', accent: '#006633', text: '#000000' }
  };

  /**
   * Catmull-Rom to Cubic Bezier spline sampler.
   * Discretizes the track into equidistant world points with tangent, normal,
   * arc length, and curvature metrics.
   */
  class TrackSpline {
    constructor(controlPoints, circuitLengthKm = 4.206) {
      this.controlPoints = controlPoints;
      this.circuitLengthKm = circuitLengthKm;
      this.circuitLengthM = circuitLengthKm * 1000;
      this.samples = [];
      this.totalArcLength = 0;
      this.bounds = { minX: Infinity, minY: Infinity, maxX: -Infinity, maxY: -Infinity, width: 0, height: 0 };
      this._build();
    }

    _build() {
      const p = this.controlPoints;
      const n = p.length;
      const rawPoints = [];

      // Catmull-Rom to cubic Bezier conversion matching trackSvg
      for (let i = 0; i < n; i++) {
        const a = p[(i - 1 + n) % n];
        const b = p[i];
        const c = p[(i + 1) % n];
        const e = p[(i + 2) % n];

        const b0 = { x: b[0], y: b[1] };
        const b1 = { x: b[0] + (c[0] - a[0]) / 6, y: b[1] + (c[1] - a[1]) / 6 };
        const b2 = { x: c[0] - (e[0] - b[0]) / 6, y: c[1] - (e[1] - b[1]) / 6 };
        const b3 = { x: c[0], y: c[1] };

        // Subdivide each segment into 40 fine steps
        const steps = 40;
        for (let j = 0; j < steps; j++) {
          const u = j / steps;
          const u2 = u * u;
          const u3 = u2 * u;
          const inv = 1 - u;
          const inv2 = inv * inv;
          const inv3 = inv2 * inv;

          // Position
          const x = inv3 * b0.x + 3 * inv2 * u * b1.x + 3 * inv * u2 * b2.x + u3 * b3.x;
          const y = inv3 * b0.y + 3 * inv2 * u * b1.y + 3 * inv * u2 * b2.y + u3 * b3.y;

          // 1st Derivative (tangent)
          const dx = 3 * inv2 * (b1.x - b0.x) + 6 * inv * u * (b2.x - b1.x) + 3 * u2 * (b3.x - b2.x);
          const dy = 3 * inv2 * (b1.y - b0.y) + 6 * inv * u * (b2.y - b1.y) + 3 * u2 * (b3.y - b2.y);

          // 2nd Derivative (for curvature)
          const ddx = 6 * inv * (b2.x - 2 * b1.x + b0.x) + 6 * u * (b3.x - 2 * b2.x + b1.x);
          const ddy = 6 * inv * (b2.y - 2 * b1.y + b0.y) + 6 * u * (b3.y - 2 * b2.y + b1.y);

          const len = Math.hypot(dx, dy) || 1e-6;
          const curvature = Math.abs(dx * ddy - dy * ddx) / Math.pow(len, 3);

          rawPoints.push({
            x, y,
            tx: dx / len,
            ty: dy / len,
            nx: -dy / len,
            ny: dx / len,
            angle: Math.atan2(dy, dx),
            curvature
          });
        }
      }

      // Compute cumulative arc length
      let cumDist = 0;
      this.samples.push({
        ...rawPoints[0],
        cumDist: 0,
        distNorm: 0,
        safeSpeedKmh: this._calcSafeSpeed(rawPoints[0].curvature)
      });

      for (let i = 1; i < rawPoints.length; i++) {
        const prev = rawPoints[i - 1];
        const cur = rawPoints[i];
        cumDist += Math.hypot(cur.x - prev.x, cur.y - prev.y);
        this.samples.push({
          ...cur,
          cumDist,
          safeSpeedKmh: this._calcSafeSpeed(cur.curvature)
        });
      }

      // Add closing segment to form continuous loop
      const last = rawPoints[rawPoints.length - 1];
      const first = rawPoints[0];
      cumDist += Math.hypot(first.x - last.x, first.y - last.y);
      this.totalArcLength = cumDist;

      // Normalize distances and compute bounds
      for (const pt of this.samples) {
        pt.distNorm = pt.cumDist / this.totalArcLength;
        if (pt.x < this.bounds.minX) this.bounds.minX = pt.x;
        if (pt.x > this.bounds.maxX) this.bounds.maxX = pt.x;
        if (pt.y < this.bounds.minY) this.bounds.minY = pt.y;
        if (pt.y > this.bounds.maxY) this.bounds.maxY = pt.y;
      }
      this.bounds.width = this.bounds.maxX - this.bounds.minX;
      this.bounds.height = this.bounds.maxY - this.bounds.minY;
    }

    _calcSafeSpeed(curvature) {
      // High curvature = sharp corner (85-130 km/h), Low curvature = straight (280-310 km/h)
      const k = Math.min(0.25, curvature);
      return Math.round(90 + 215 / (1 + 45 * k));
    }

    /**
     * Get track point at normalized distance s in [0, 1)
     */
    getPointAt(distNorm) {
      const s = ((distNorm % 1) + 1) % 1;
      const targetDist = s * this.totalArcLength;

      // Binary search
      let low = 0, high = this.samples.length - 1;
      while (low <= high) {
        const mid = (low + high) >> 1;
        if (this.samples[mid].cumDist < targetDist) low = mid + 1;
        else high = mid - 1;
      }

      const idx0 = Math.max(0, Math.min(this.samples.length - 1, low - 1));
      const idx1 = Math.min(this.samples.length - 1, idx0 + 1);
      const p0 = this.samples[idx0];
      const p1 = this.samples[idx1];

      const segLen = p1.cumDist - p0.cumDist || 1e-6;
      const t = Math.max(0, Math.min(1, (targetDist - p0.cumDist) / segLen));

      const x = p0.x + (p1.x - p0.x) * t;
      const y = p0.y + (p1.y - p0.y) * t;
      const tx = p0.tx + (p1.tx - p0.tx) * t;
      const ty = p0.ty + (p1.ty - p0.ty) * t;
      const nx = p0.nx + (p1.nx - p0.nx) * t;
      const ny = p0.ny + (p1.ny - p0.ny) * t;
      const angle = p0.angle + (p1.angle - p0.angle) * t;
      const safeSpeedKmh = p0.safeSpeedKmh + (p1.safeSpeedKmh - p0.safeSpeedKmh) * t;

      return { x, y, tx, ty, nx, ny, angle, safeSpeedKmh, distNorm: s };
    }
  }

  /**
   * Mock Simulation Engine.
   * Generates continuous position frames with realistic racing dynamics.
   */
  class RaceSimEngine {
    constructor(circuitKey = 'brands_hatch') {
      const trackData = DB.tracks[circuitKey] || DB.tracks.brands_hatch;
      this.circuitKey = circuitKey;
      this.circuitName = trackData.name;
      this.circuitLenKm = trackData.len;
      this.totalLaps = 76; // Brands Hatch GP 1976 total laps
      this.spline = new TrackSpline(trackData.map, trackData.len);

      this.timeMs = 0;
      this.leaderLap = 1;
      this.flag = 'green'; // 'green' | 'yellow' | 'chequered'
      this.yellowTimerMs = 0;
      this.eventLog = [];
      this.listeners = [];
      this.fastestLap = { carId: 1, driverName: 'Niki Lauda', timeMs: 81420 };

      this.cars = [];
      this._initGrid();
    }

    _initGrid() {
      // Sort grid according to starting performance/qualifying
      const gridData = [...DB.grid].sort((a, b) => b[6] - a[6]);

      this.cars = gridData.map((g, index) => {
        const [no, id, name, nat, team, age, rating] = g;
        const livery = TEAM_LIVERIES[team] || { primary: '#888888', secondary: '#ffffff', accent: '#ffffff', text: '#ffffff' };
        const isPlayer = !!livery.isPlayer;

        // Grid starting stagger: 10-15m apart behind S/F line
        const gridOffsetM = -(index * 14 + 18);
        const startDistNorm = (1.0 + (gridOffsetM / this.spline.circuitLengthM)) % 1.0;

        return {
          carId: no,
          driverId: id,
          driverName: name,
          nat,
          teamId: team,
          teamName: DB.teams[team]?.name || team,
          livery,
          isPlayer,
          rating,
          // Dynamics
          distanceNorm: startDistNorm,
          lap: 1,
          totalDistanceM: gridOffsetM,
          speedKmh: 120 + (rating * 0.5),
          targetSpeedKmh: 240,
          laneOffset: (index % 2 === 0 ? -0.4 : 0.4), // staggered left/right on grid
          targetLaneOffset: 0,
          // Strategy & Condition
          tyreCompound: 'M',
          tyreWearPct: 98,
          pitState: 'on_track', // 'on_track' | 'pitting' | 'in_box' | 'exiting'
          pitStopCount: 0,
          pitTimerSec: 0,
          scheduledPitLap: isPlayer ? (index === 0 ? 14 : 18) : (12 + (index % 12)),
          // Stats
          position: index + 1,
          prevPosition: index + 1,
          gapLeaderSec: 0,
          gapAheadSec: 0,
          lastLapMs: null,
          bestLapMs: null,
          currentLapStartMs: 0,
          status: 'racing', // 'racing' | 'pitted' | 'retired'
          retireReason: null
        };
      });
    }

    onFrame(callback) {
      this.listeners.push(callback);
    }

    /**
     * Advance simulation by delta real ms multiplied by playback speed
     */
    tick(simDeltaMs = 0) {
      if (this.flag === 'chequered') return null;

      const newEvents = [];
      if (simDeltaMs > 0) {
        this.timeMs += simDeltaMs;
        const dtSec = simDeltaMs / 1000;

        // Check Yellow flag timer
        if (this.yellowTimerMs > 0) {
          this.yellowTimerMs -= simDeltaMs;
          if (this.yellowTimerMs <= 0) {
            this.flag = 'green';
            newEvents.push({
              id: 'ev_' + this.timeMs,
              timeMs: this.timeMs,
              lap: this.leaderLap,
              type: 'green_flag',
              textPl: 'Koniec neutralizacji · Zielona flaga',
              textEn: 'Caution clear · Green flag'
            });
          }
        }

      // Update each car
      for (let i = 0; i < this.cars.length; i++) {
        const car = this.cars[i];
        if (car.status === 'retired') continue;

        // Pit box state countdown
        if (car.pitState === 'in_box') {
          car.speedKmh = 0;
          car.pitTimerSec -= dtSec;
          if (car.pitTimerSec <= 0) {
            car.pitState = 'exiting';
            car.tyreWearPct = 100;
            car.pitStopCount++;
            newEvents.push({
              id: 'ev_' + this.timeMs + '_' + car.carId,
              timeMs: this.timeMs,
              lap: car.lap,
              type: 'pit_out',
              carId: car.carId,
              driverName: car.driverName,
              teamId: car.teamId,
              textPl: `#${car.carId} ${car.driverName} opuszcza aleję serwisową (${car.pitStopCount}. stop)`,
              textEn: `#${car.carId} ${car.driverName} leaves the pits (${car.pitStopCount} pit stop)`
            });
          }
          continue;
        }

        // Current track profile
        const pt = this.spline.getPointAt(car.distanceNorm);

        // Target speed calculation
        let safeSpeed = pt.safeSpeedKmh;
        // Top speed modifier based on rating
        const ratingBonus = (car.rating - 75) * 1.2;
        // Tyre wear penalty
        const tyrePenalty = Math.max(0, (70 - car.tyreWearPct) * 0.4);

        if (car.pitState === 'pitting' || car.pitState === 'exiting') {
          safeSpeed = 75; // pit speed limiter
        } else if (this.flag === 'yellow') {
          safeSpeed = Math.min(safeSpeed, 140); // yellow flag caution speed
        } else {
          safeSpeed = Math.min(305, safeSpeed + ratingBonus - tyrePenalty);
        }

        // Slipstream bonus if trailing car ahead on straight
        const carAhead = this.cars[i - 1];
        if (carAhead && car.pitState === 'on_track' && safeSpeed > 220) {
          const distBehind = (carAhead.totalDistanceM - car.totalDistanceM);
          if (distBehind > 5 && distBehind < 35) {
            safeSpeed += 14; // slipstream draft
            // Prepare to overtake on straights
            if (distBehind < 18 && Math.abs(car.laneOffset - carAhead.laneOffset) < 0.3) {
              car.targetLaneOffset = carAhead.laneOffset > 0 ? -0.7 : 0.7;
            }
          }
        }

        // Smooth acceleration/braking
        const accelRate = (safeSpeed > car.speedKmh ? 32 : 68); // braking is faster than acceleration
        car.speedKmh += (safeSpeed - car.speedKmh) * Math.min(1, dtSec * (accelRate / 10));

        // Lateral lane adjustment
        car.laneOffset += (car.targetLaneOffset - car.laneOffset) * Math.min(1, dtSec * 1.5);

        // Advance distance
        const speedMps = (car.speedKmh * 1000) / 3600;
        const deltaMeters = speedMps * dtSec;
        car.totalDistanceM += deltaMeters;

        const prevDistNorm = car.distanceNorm;
        car.distanceNorm = (car.distanceNorm + (deltaMeters / this.spline.circuitLengthM));

        // Tyre wear degradation
        car.tyreWearPct = Math.max(10, car.tyreWearPct - (dtSec * 0.022));

        // Pit entry detection (between 0.94 and 0.96)
        if (car.pitState === 'on_track' && car.lap >= car.scheduledPitLap && prevDistNorm < 0.94 && car.distanceNorm >= 0.94) {
          car.pitState = 'pitting';
          car.targetLaneOffset = -1.8; // move into pit lane
          newEvents.push({
            id: 'ev_' + this.timeMs + '_' + car.carId,
            timeMs: this.timeMs,
            lap: car.lap,
            type: 'pit_in',
            carId: car.carId,
            driverName: car.driverName,
            teamId: car.teamId,
            textPl: `#${car.carId} ${car.driverName} zjeżdża do alei serwisowej`,
            textEn: `#${car.carId} ${car.driverName} enters the pit lane`
          });
        }

        // Reaching pit box (around 0.99)
        if (car.pitState === 'pitting' && car.distanceNorm >= 0.99) {
          car.pitState = 'in_box';
          car.pitTimerSec = 5.5 + (Math.random() * 2.5); // 5.5 - 8.0s pit stop
          car.speedKmh = 0;
          car.scheduledPitLap += 24; // next stop in 24 laps
        }

        // Pit exit completion (around 0.06)
        if (car.pitState === 'exiting' && car.distanceNorm >= 0.06 && car.distanceNorm < 0.2) {
          car.pitState = 'on_track';
          car.targetLaneOffset = 0;
        }

        // Lap completed
        if (car.distanceNorm >= 1.0) {
          car.distanceNorm -= 1.0;
          const lapTimeMs = this.timeMs - car.currentLapStartMs;
          car.currentLapStartMs = this.timeMs;
          car.lastLapMs = lapTimeMs;

          if (!car.bestLapMs || lapTimeMs < car.bestLapMs) {
            car.bestLapMs = lapTimeMs;
          }

          // Check overall fastest lap (if lap was clean and completed)
          if (car.lap > 1 && lapTimeMs > 60000 && lapTimeMs < this.fastestLap.timeMs) {
            this.fastestLap = { carId: car.carId, driverName: car.driverName, timeMs: lapTimeMs };
            newEvents.push({
              id: 'ev_fl_' + this.timeMs,
              timeMs: this.timeMs,
              lap: car.lap,
              type: 'fastest_lap',
              carId: car.carId,
              driverName: car.driverName,
              teamId: car.teamId,
              textPl: `Najszybsze okrążenie: #${car.carId} ${car.driverName} (${this._formatLapTime(lapTimeMs)})`,
              textEn: `Fastest lap: #${car.carId} ${car.driverName} (${this._formatLapTime(lapTimeMs)})`
            });
          }

          car.lap++;
          if (car.position === 1) {
            this.leaderLap = car.lap;
            if (this.leaderLap > this.totalLaps) {
              this.flag = 'chequered';
              newEvents.push({
                id: 'ev_finish_' + this.timeMs,
                timeMs: this.timeMs,
                lap: this.totalLaps,
                type: 'chequered_flag',
                textPl: `Koniec wyścigu! Wygrywa #${car.carId} ${car.driverName}!`,
                textEn: `Chequered flag! Winner is #${car.carId} ${car.driverName}!`
              });
            }
          }
        }
      }
      } // end if (simDeltaMs > 0)

      // Re-sort standings by total distance traveled
      this.cars.sort((a, b) => b.totalDistanceM - a.totalDistanceM);

      // Update positions, gaps, and detect overtakes
      const leader = this.cars[0];
      const avgLeaderSpeedMps = Math.max(40, (leader.speedKmh * 1000) / 3600);

      for (let pos = 0; pos < this.cars.length; pos++) {
        const car = this.cars[pos];
        const newPos = pos + 1;

        // Overtake detection
        if (car.prevPosition > newPos && car.lap > 1 && car.status === 'racing') {
          const overtakenCar = this.cars[pos + 1];
          if (overtakenCar && overtakenCar.pitState === 'on_track') {
            newEvents.push({
              id: 'ev_ov_' + this.timeMs + '_' + car.carId,
              timeMs: this.timeMs,
              lap: car.lap,
              type: 'overtake',
              carId: car.carId,
              driverName: car.driverName,
              teamId: car.teamId,
              targetDriverName: overtakenCar.driverName,
              textPl: `P${newPos}: #${car.carId} ${car.driverName} wyprzedza ${overtakenCar.driverName}`,
              textEn: `P${newPos}: #${car.carId} ${car.driverName} overtakes ${overtakenCar.driverName}`
            });
          }
        }
        car.prevPosition = newPos;
        car.position = newPos;

        // Gaps in seconds
        car.gapLeaderSec = Math.max(0, (leader.totalDistanceM - car.totalDistanceM) / avgLeaderSpeedMps);
        if (pos === 0) {
          car.gapAheadSec = 0;
        } else {
          const ahead = this.cars[pos - 1];
          car.gapAheadSec = Math.max(0, (ahead.totalDistanceM - car.totalDistanceM) / avgLeaderSpeedMps);
        }
      }

      // Record events
      if (newEvents.length > 0) {
        this.eventLog.push(...newEvents);
        if (this.eventLog.length > 100) this.eventLog.splice(0, this.eventLog.length - 100);
      }

      // Construct PositionFrame conforming to PP-052
      const frame = {
        timeMs: this.timeMs,
        lap: this.leaderLap,
        totalLaps: this.totalLaps,
        flag: this.flag,
        fastestLap: this.fastestLap,
        cars: this.cars.map(c => ({
          carId: c.carId,
          driverId: c.driverId,
          driverName: c.driverName,
          nat: c.nat,
          teamId: c.teamId,
          teamName: c.teamName,
          livery: c.livery,
          isPlayer: c.isPlayer,
          lap: c.lap,
          distanceNorm: c.distanceNorm,
          laneOffset: c.laneOffset,
          speedKmh: Math.round(c.speedKmh),
          position: c.position,
          gapLeaderSec: c.gapLeaderSec,
          gapAheadSec: c.gapAheadSec,
          lastLapMs: c.lastLapMs,
          bestLapMs: c.bestLapMs,
          pitState: c.pitState,
          pitStopCount: c.pitStopCount,
          tyreCompound: c.tyreCompound,
          tyreWearPct: Math.round(c.tyreWearPct),
          status: c.status
        })),
        events: newEvents
      };

      // Notify listeners
      for (const listener of this.listeners) {
        listener(frame);
      }

      return frame;
    }

    _formatLapTime(ms) {
      if (!ms) return '—';
      const m = Math.floor(ms / 60000);
      const s = ((ms % 60000) / 1000).toFixed(2);
      return `${m}:${s.padStart(5, '0')}`;
    }
  }

  // Export to global scope
  window.RaceSimEngine = RaceSimEngine;
  window.TrackSpline = TrackSpline;
  window.TEAM_LIVERIES = TEAM_LIVERIES;
})();

import './HorseBodyMap.css';

const SEVERITY_CONFIG = {
  0: { color: '#22c55e', label: 'Healthy', textClass: 'zone-label-dark' },
  1: { color: '#eab308', label: 'Minor', textClass: 'zone-label-dark' },
  2: { color: '#f97316', label: 'Moderate', textClass: 'zone-label-dark' },
  3: { color: '#ef4444', label: 'Severe', textClass: 'zone-label-light' },
  4: { color: '#991b1b', label: 'Critical', textClass: 'zone-label-light' },
};

const BODY_PARTS = {
  Head: {
    path: 'M 388,118 C 398,100 415,95 428,105 C 442,115 452,135 455,155 C 456,168 448,178 438,180 C 428,182 418,178 408,172 C 398,165 392,155 388,142 C 384,130 384,120 388,118 Z',
    labelX: 425,
    labelY: 142,
    displayLabel: 'Head',
  },
  Neck: {
    path: 'M 388,118 C 384,120 384,130 388,142 C 392,155 396,168 384,178 C 370,188 350,190 330,184 C 310,178 296,166 286,152 C 280,142 280,128 290,118 C 302,108 320,102 342,105 C 362,108 378,112 388,118 Z',
    labelX: 345,
    labelY: 148,
    displayLabel: 'Neck',
  },
  Shoulder: {
    path: 'M 290,118 C 280,128 280,142 286,152 C 292,160 296,182 296,210 C 296,238 292,258 282,266 C 272,274 262,270 260,260 C 258,250 260,222 264,195 C 268,168 274,140 284,124 C 288,118 290,116 290,118 Z',
    labelX: 282,
    labelY: 195,
    displayLabel: 'Shoulder',
  },
  Back: {
    path: 'M 284,124 C 274,140 268,168 264,195 C 260,222 256,252 250,262 C 242,274 232,276 218,276 C 205,276 195,272 188,264 C 182,256 180,240 178,215 C 176,190 178,162 184,138 C 188,122 200,115 218,112 C 238,110 260,112 284,124 Z',
    labelX: 235,
    labelY: 195,
    displayLabel: 'Back',
  },
  Hip: {
    path: 'M 184,138 C 178,162 176,190 178,215 C 180,240 180,262 170,272 C 160,282 146,284 134,278 C 124,272 118,262 114,246 C 110,232 108,215 114,195 C 120,172 128,148 140,132 C 152,116 170,110 184,112 C 188,115 186,125 184,138 Z',
    labelX: 160,
    labelY: 195,
    displayLabel: 'Hip',
  },
  Tail: {
    path: 'M 114,195 C 108,215 110,232 114,246 C 106,252 96,262 86,274 C 74,288 62,300 56,292 C 50,284 53,270 63,254 C 73,238 86,224 96,212 C 106,202 112,198 114,195 Z',
    labelX: 85,
    labelY: 258,
    displayLabel: 'Tail',
  },
  'FrontLeg-Right': {
    path: 'M 282,266 C 286,280 288,300 288,320 C 288,340 286,356 280,368 C 276,376 272,378 268,376 C 264,374 264,368 266,358 C 268,344 272,328 274,308 C 276,288 278,270 280,264 Z',
    labelX: 278,
    labelY: 325,
    displayLabel: 'Front R',
  },
  'FrontLeg-Left': {
    path: 'M 296,262 C 300,276 302,296 302,316 C 302,336 300,350 294,362 C 290,370 286,372 282,370 C 278,368 278,362 280,352 C 282,338 286,322 288,302 C 290,282 292,266 294,260 Z',
    labelX: 296,
    labelY: 318,
    displayLabel: 'Front L',
  },
  'HindLeg-Right': {
    path: 'M 134,278 C 138,292 140,312 140,332 C 140,352 138,368 132,380 C 128,388 124,390 120,388 C 116,386 116,380 118,370 C 120,356 124,340 126,320 C 128,300 130,282 132,276 Z',
    labelX: 132,
    labelY: 338,
    displayLabel: 'Hind R',
  },
  'HindLeg-Left': {
    path: 'M 152,274 C 156,288 158,308 158,328 C 158,348 156,362 150,374 C 146,382 142,384 138,382 C 134,380 134,374 136,364 C 138,350 142,334 144,314 C 146,294 148,278 150,272 Z',
    labelX: 152,
    labelY: 330,
    displayLabel: 'Hind L',
  },
  'Fetlock-Right': {
    path: 'M 280,360 C 284,362 286,366 286,370 C 286,376 284,380 280,382 C 276,384 272,382 270,378 C 268,374 268,368 270,364 C 272,360 276,358 280,360 Z M 132,372 C 136,374 138,378 138,382 C 138,388 136,392 132,394 C 128,396 124,394 122,390 C 120,386 120,380 122,376 C 124,372 128,370 132,372 Z',
    labelX: 310,
    labelY: 378,
    displayLabel: 'Fetlock R',
  },
  'Fetlock-Left': {
    path: 'M 294,354 C 298,356 300,360 300,364 C 300,370 298,374 294,376 C 290,378 286,376 284,372 C 282,368 282,362 284,358 C 286,354 290,352 294,354 Z M 150,366 C 154,368 156,372 156,376 C 156,382 154,386 150,388 C 146,390 142,388 140,384 C 138,380 138,374 140,370 C 142,366 146,364 150,366 Z',
    labelX: 324,
    labelY: 370,
    displayLabel: 'Fetlock L',
  },
};

function HorseBodyMap({ injuredParts = {}, onPartClick = () => {}, selectedPart = null }) {
  const getSeverity = (partName) => {
    const sev = injuredParts[partName];
    return sev !== undefined && sev !== null ? sev : 0;
  };

  return (
    <div className="horse-body-map-container">
      <div className="horse-body-map-header">
        <h3 className="horse-body-map-title">Horse Body Map</h3>
        <p className="horse-body-map-subtitle">Click a body part to log or review an injury</p>
      </div>

      <div className="horse-body-map-svg-wrapper">
        <svg
          viewBox="0 0 520 430"
          className="horse-body-map-svg"
          xmlns="http://www.w3.org/2000/svg"
          role="img"
          aria-label="Horse body map with clickable injury zones"
        >

          {Object.entries(BODY_PARTS).map(([partName, config]) => {
            const severity = getSeverity(partName);
            const sevConfig = SEVERITY_CONFIG[severity] || SEVERITY_CONFIG[0];
            const isSelected = selectedPart === partName;

            return (
              <g key={partName} className="horse-zone-group">
                <path
                  d={config.path}
                  className={`horse-zone${isSelected ? ' selected' : ''}`}
                  fill={sevConfig.color}
                  data-part={partName}
                  data-severity={severity}
                  onClick={() => onPartClick(partName)}
                  role="button"
                  tabIndex={0}
                  aria-label={`${config.displayLabel} - ${sevConfig.label}`}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      onPartClick(partName);
                    }
                  }}
                />
                <text
                  x={config.labelX}
                  y={config.labelY}
                  className={`zone-label ${sevConfig.textClass}`}
                >
                  {config.displayLabel}
                </text>
              </g>
            );
          })}
        </svg>
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

export default HorseBodyMap;

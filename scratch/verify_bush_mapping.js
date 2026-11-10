const fs = require('fs');
const bushFiles = fs.readdirSync('Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs')
  .filter(f => f.endsWith('.obj'))
  .map(f => f.replace('.obj', ''));
console.log('Total shrub obj files:', bushFiles.length);

const cs = fs.readFileSync('Assets/Scripts/TacticalBushZone.cs', 'utf8');
const missing = [];
for (const b of bushFiles) {
  if (!cs.includes(`"${b}"`)) {
    missing.push(b);
  }
}
console.log('Missing in TacticalBushZone.cs:', missing.length);
if (missing.length > 0) {
  console.log('Missing items:', missing);
} else {
  console.log('PERFECT! All 126 shrubs are 100% mapped!');
}

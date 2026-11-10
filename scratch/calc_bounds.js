const fs = require('fs');
const lines = fs.readFileSync('Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Subtropical/Sbt_Grass0_1.obj', 'utf8').split('\n');
let minX = 1e9, maxX = -1e9, minY = 1e9, maxY = -1e9, minZ = 1e9, maxZ = -1e9;
for (const line of lines) {
    if (line.startsWith('v ')) {
        const parts = line.trim().split(/\s+/).slice(1).map(Number);
        minX = Math.min(minX, parts[0]); maxX = Math.max(maxX, parts[0]);
        minY = Math.min(minY, parts[1]); maxY = Math.max(maxY, parts[1]);
        minZ = Math.min(minZ, parts[2]); maxZ = Math.max(maxZ, parts[2]);
    }
}
console.log(`Width X: ${(maxX - minX).toFixed(2)}m`);
console.log(`Height Y: ${(maxY - minY).toFixed(2)}m`);
console.log(`Length Z: ${(maxZ - minZ).toFixed(2)}m`);

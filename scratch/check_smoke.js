const fs = require('fs');
const buf = fs.readFileSync('Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/wispy_smoke_diffuse.png');
console.log('PNG magic:', buf.slice(0, 8).toString('hex'));
// IHDR chunk: width(4), height(4), bitDepth(1), colorType(1), compression(1), filter(1), interlace(1)
const width = buf.readUInt32BE(16);
const height = buf.readUInt32BE(20);
const bitDepth = buf.readUInt8(24);
const colorType = buf.readUInt8(25); // 0=grayscale, 2=RGB, 3=palette, 4=grayscale+alpha, 6=RGBA
console.log(`Width: ${width}, Height: ${height}, BitDepth: ${bitDepth}, ColorType: ${colorType} (${colorType === 6 ? 'RGBA' : colorType === 2 ? 'RGB (No Alpha!)' : colorType})`);

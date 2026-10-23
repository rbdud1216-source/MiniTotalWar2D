const fs = require('fs');

const ddsPath = 'c:\\unityProject\\MiniTotalWar2D\\Assets\\TotalWar_ThreeKingdoms_Extracted\\Vegetation\\Textures\\plum_trunk_base_colour.dds';
const buf = fs.readFileSync(ddsPath);

const height = buf.readUInt32LE(12);
const width = buf.readUInt32LE(16);
const fourCC = buf.toString('ascii', 84, 88);
console.log(`plum_trunk: ${width}x${height}, format: ${fourCC}`);

const pineTrunkPath = 'c:\\unityProject\\MiniTotalWar2D\\Assets\\TotalWar_ThreeKingdoms_Extracted\\Vegetation\\Textures\\pine_trunk_base_colour.dds';
const pBuf = fs.readFileSync(pineTrunkPath);
console.log(`pine_trunk: ${pBuf.readUInt32LE(16)}x${pBuf.readUInt32LE(12)}, format: ${pBuf.toString('ascii', 84, 88)}`);

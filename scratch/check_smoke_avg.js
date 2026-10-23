const fs = require("fs");
const zlib = require("zlib");
const file = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/totalwar_smoke_particle.png";
const buf = fs.readFileSync(file);
let pos = 8;
let idatChunks = [];
while (pos < buf.length) {
    let len = buf.readUInt32BE(pos);
    let type = buf.toString("ascii", pos + 4, pos + 8);
    if (type === "IDAT") idatChunks.push(buf.slice(pos + 8, pos + 8 + len));
    pos += 12 + len;
}
let raw = zlib.inflateSync(Buffer.concat(idatChunks));
let width = buf.readUInt32BE(16);
let height = buf.readUInt32BE(20);
let avgR = 0, avgG = 0, avgB = 0, avgA = 0, maxA = 0;
let count = 0;
for (let i = 0; i < width * height; i++) {
    let row = Math.floor(i / width);
    let col = i % width;
    let byteIndex = row * (1 + width * 4) + 1 + col * 4;
    let r = raw[byteIndex], g = raw[byteIndex+1], b = raw[byteIndex+2], a = raw[byteIndex+3];
    if (a > 0) {
        avgR += r; avgG += g; avgB += b; avgA += a;
        if (a > maxA) maxA = a;
        count++;
    }
}
console.log("Smoke particle: count=" + count + ", maxA=" + maxA);
console.log("Avg R=" + (avgR/count).toFixed(1) + " G=" + (avgG/count).toFixed(1) + " B=" + (avgB/count).toFixed(1) + " A=" + (avgA/count).toFixed(1));

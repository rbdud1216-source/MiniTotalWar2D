const fs = require("fs");
const zlib = require("zlib");
const file = "Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/totalwar_smoke_particle.png";
const buf = fs.readFileSync(file);
let pos = 8;
let idatChunks = [];
while (pos < buf.length) {
    let len = buf.readUInt32BE(pos);
    let type = buf.toString("ascii", pos + 4, pos + 8);
    if (type === "IDAT") {
        idatChunks.push(buf.slice(pos + 8, pos + 8 + len));
    }
    pos += 12 + len;
}
let idat = Buffer.concat(idatChunks);
let raw = zlib.inflateSync(idat);

let maxA = 0;
let minA = 255;
let nonZeroA = 0;
// header info
let width = buf.readUInt32BE(16);
let height = buf.readUInt32BE(20);
console.log(file + ": " + width + "x" + height);
for (let i = 0; i < width * height; i++) {
    let row = Math.floor(i / width);
    let col = i % width;
    let byteIndex = row * (1 + width * 4) + 1 + col * 4;
    let a = raw[byteIndex + 3];
    if (a > maxA) maxA = a;
    if (a < minA) minA = a;
    if (a > 0) nonZeroA++;
}
console.log("Alpha stats: min=" + minA + ", max=" + maxA + ", nonZeroCount=" + nonZeroA + " / " + (width*height));

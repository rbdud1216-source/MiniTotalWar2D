const fs = require("fs");
const zlib = require("zlib");
const buf = fs.readFileSync("Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/wispy_smoke_diffuse.png");
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
for (let i = 0; i < 512 * 512; i++) {
    // each row has 1 filter byte + 512*4 bytes
    let row = Math.floor(i / 512);
    let col = i % 512;
    let byteIndex = row * (1 + 512 * 4) + 1 + col * 4;
    let a = raw[byteIndex + 3];
    if (a > maxA) maxA = a;
    if (a < minA) minA = a;
    if (a > 0) nonZeroA++;
}
console.log("Alpha stats: min=" + minA + ", max=" + maxA + ", nonZeroCount=" + nonZeroA + " / " + (512*512));

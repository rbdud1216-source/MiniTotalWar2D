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
console.log("Raw length: " + raw.length);
let row = 256;
let rowStart = row * (1 + 512 * 4) + 1;
for (let c = 250; c < 260; c++) {
    let p = rowStart + c * 4;
    console.log("Pixel " + c + ": R=" + raw[p] + " G=" + raw[p+1] + " B=" + raw[p+2] + " A=" + raw[p+3]);
}

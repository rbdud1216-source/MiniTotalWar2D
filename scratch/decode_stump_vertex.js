const fs = require('fs');

const packPath = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Total War THREE KINGDOMS\\data\\vegetation.pack';
const fd = fs.openSync(packPath, 'r');

const headerBuf = Buffer.alloc(28);
fs.readSync(fd, headerBuf, 0, 28, 0);
const fileCount = headerBuf.readUInt32LE(16);
const indexSize = headerBuf.readUInt32LE(20);
const indexBuf = Buffer.alloc(indexSize);
fs.readSync(fd, indexBuf, 0, indexSize, 28);
let cursor = 0;
let currentOffset = 28 + indexSize;

let stumpABuf = null;
for (let i = 0; i < fileCount; i++) {
    const size = indexBuf.readUInt32LE(cursor);
    cursor += 5;
    let end = cursor;
    while (indexBuf[end] !== 0) end++;
    const p = indexBuf.toString('utf8', cursor, end).replace(/\\/g, '/');
    cursor = end + 1;
    if (p === 'battleterrain/vegetation/trees/stump/stump_a.rigid_model_v2') {
        stumpABuf = Buffer.alloc(size);
        fs.readSync(fd, stumpABuf, 0, size, currentOffset);
        break;
    }
    currentOffset += size;
}

function decodeFloat16(binary) {
    const exponent = (binary & 0x7C00) >> 10;
    const fraction = binary & 0x03FF;
    const sign = (binary & 0x8000) > 0 ? -1 : 1;
    if (exponent === 0) return sign * Math.pow(2, -14) * (fraction / 1024);
    else if (exponent === 0x1F) return fraction ? NaN : sign * Infinity;
    return sign * Math.pow(2, exponent - 15) * (1 + fraction / 1024);
}

const cur = 140 + 28 * 2; // lodCount = 2
const vOffset = stumpABuf.readUInt32LE(cur + 8);
const vCount = stumpABuf.readUInt32LE(cur + 12);
const iOffset = stumpABuf.readUInt32LE(cur + 16);
const vSize = (iOffset - vOffset) / vCount;
const gVStart = cur + vOffset;

console.log(`vCount=${vCount}, vSize=${vSize}`);

for (let i = 0; i < 5; i++) {
    const ptr = gVStart + i * vSize;
    console.log(`\n--- Vertex ${i} ---`);
    for (let o = 0; o < vSize; o += 2) {
        const u16 = stumpABuf.readUInt16LE(ptr + o);
        const f16 = decodeFloat16(u16);
        const u8_0 = stumpABuf[ptr + o];
        const u8_1 = stumpABuf[ptr + o + 1];
        let f32 = 0;
        if (o + 4 <= vSize) {
            f32 = stumpABuf.readFloatLE(ptr + o);
        }
        console.log(`  Offset ${o.toString().padStart(2, ' ')}: u16=0x${u16.toString(16).padStart(4, '0')}, f16=${f16.toFixed(4).padStart(8, ' ')}, f32=${f32.toFixed(4).padStart(10, ' ')} | bytes=${u8_0.toString(16).padStart(2,'0')} ${u8_1.toString(16).padStart(2,'0')}`);
    }
}

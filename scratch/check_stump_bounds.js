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

const packEntries = [];
for (let i = 0; i < fileCount; i++) {
    const size = indexBuf.readUInt32LE(cursor);
    cursor += 5;
    let end = cursor;
    while (indexBuf[end] !== 0) end++;
    const p = indexBuf.toString('utf8', cursor, end).replace(/\\/g, '/');
    cursor = end + 1;
    if (p.startsWith('battleterrain/vegetation/trees/stump/stump_') && p.endsWith('.rigid_model_v2')) {
        packEntries.push({ path: p, size, offset: currentOffset });
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

for (const entry of packEntries) {
    const buf = Buffer.alloc(entry.size);
    fs.readSync(fd, buf, 0, entry.size, entry.offset);
    const lodCount = buf.readUInt16LE(8);
    const meshCount = buf.readUInt32LE(140);
    console.log(`\n=== ${entry.path} ===`);
    let cur = 140 + 28 * lodCount;

    for (let m = 0; m < meshCount; m++) {
        const matType = buf.readUInt16LE(cur);
        const sectionSize = buf.readUInt32LE(cur + 4);
        const vOffset = buf.readUInt32LE(cur + 8);
        const vCount = buf.readUInt32LE(cur + 12);
        const iOffset = buf.readUInt32LE(cur + 16);
        const vSize = (iOffset - vOffset) / vCount;
        const gVStart = cur + vOffset;

        let minX = Infinity, minY = Infinity, minZ = Infinity;
        let maxX = -Infinity, maxY = -Infinity, maxZ = -Infinity;

        for (let i = 0; i < vCount; i++) {
            const ptr = gVStart + i * vSize;
            let x = decodeFloat16(buf.readUInt16LE(ptr + 8));
            let y = decodeFloat16(buf.readUInt16LE(ptr + 10));
            let z = decodeFloat16(buf.readUInt16LE(ptr + 12));

            if (x === 0 && y === 0 && z === 0) {
                const x0 = decodeFloat16(buf.readUInt16LE(ptr + 0));
                const y0 = decodeFloat16(buf.readUInt16LE(ptr + 2));
                const z0 = decodeFloat16(buf.readUInt16LE(ptr + 4));
                if (x0 !== 0 || y0 !== 0 || z0 !== 0) {
                    x = x0; y = y0; z = z0;
                }
            }

            minX = Math.min(minX, x); maxX = Math.max(maxX, x);
            minY = Math.min(minY, y); maxY = Math.max(maxY, y);
            minZ = Math.min(minZ, z); maxZ = Math.max(maxZ, z);
        }

        console.log(`  Submesh ${m} (matType=${matType}, vCount=${vCount}):`);
        console.log(`    X: [${minX.toFixed(3)}, ${maxX.toFixed(3)}] (size: ${(maxX - minX).toFixed(3)})`);
        console.log(`    Y: [${minY.toFixed(3)}, ${maxY.toFixed(3)}] (size: ${(maxY - minY).toFixed(3)})`);
        console.log(`    Z: [${minZ.toFixed(3)}, ${maxZ.toFixed(3)}] (size: ${(maxZ - minZ).toFixed(3)})`);

        cur += sectionSize;
    }
}

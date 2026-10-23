const fs = require('fs');
const path = require('path');

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
    packEntries.push({ path: p, size, offset: currentOffset });
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

const checkList = [
    'battleterrain/vegetation/trees/stump/stump_a.rigid_model_v2',
    'battleterrain/vegetation/trees/stump/stump_b.rigid_model_v2',
    'battleterrain/vegetation/trees/stump/stump_c.rigid_model_v2',
    'battleterrain/vegetation/trees/stump/stump_d.rigid_model_v2',
    'battleterrain/vegetation/trees/stump/stump_e.rigid_model_v2',
    'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_a.rigid_model_v2',
    'battleterrain/vegetation/trees/shrub_branch/tree_shrub_brokenbranch_a.rigid_model_v2',
    'battleterrain/vegetation/trees/pine/pine_m_dead.rigid_model_v2',
    'battleterrain/vegetation/trees/pine/pine_h_broken.rigid_model_v2',
    'battleterrain/vegetation/trees/scholar/tree_scholar_broken_a.rigid_model_v2',
    'battleterrain/vegetation/trees/castanopsis/castanopsis_a_dead.rigid_model_v2',
    'battleterrain/vegetation/trees/castanopsis_jugle/castanopsis_jugle_dead_a.rigid_model_v2',
    'battleterrain/vegetation/trees/pine/pine_a.rigid_model_v2',
    'battleterrain/vegetation/trees/bamboo/bamboo_a.rigid_model_v2'
];

for (const targetPath of checkList) {
    const entry = packEntries.find(e => e.path === targetPath);
    if (!entry) {
        console.log(`Not found: ${targetPath}`);
        continue;
    }
    const buf = Buffer.alloc(entry.size);
    fs.readSync(fd, buf, 0, entry.size, entry.offset);
    const lodCount = buf.readUInt16LE(8);
    const meshCount = buf.readUInt32LE(140);
    const cur = 140 + 28 * lodCount;
    const vOffset = buf.readUInt32LE(cur + 8);
    const vCount = buf.readUInt32LE(cur + 12);
    const iOffset = buf.readUInt32LE(cur + 16);
    const vSize = (iOffset - vOffset) / vCount;
    const gVStart = cur + vOffset;

    // Check first 3 vertices
    let pos0_nonzero = 0;
    let pos8_nonzero = 0;
    for (let i = 0; i < Math.min(10, vCount); i++) {
        const ptr = gVStart + i * vSize;
        const x0 = decodeFloat16(buf.readUInt16LE(ptr + 0));
        const y0 = decodeFloat16(buf.readUInt16LE(ptr + 2));
        const z0 = decodeFloat16(buf.readUInt16LE(ptr + 4));
        if (x0 !== 0 || y0 !== 0 || z0 !== 0) pos0_nonzero++;

        const x8 = decodeFloat16(buf.readUInt16LE(ptr + 8));
        const y8 = decodeFloat16(buf.readUInt16LE(ptr + 10));
        const z8 = decodeFloat16(buf.readUInt16LE(ptr + 12));
        if (x8 !== 0 || y8 !== 0 || z8 !== 0) pos8_nonzero++;
    }

    const firstPtr = gVStart;
    const x0 = decodeFloat16(buf.readUInt16LE(firstPtr + 0));
    const y0 = decodeFloat16(buf.readUInt16LE(firstPtr + 2));
    const z0 = decodeFloat16(buf.readUInt16LE(firstPtr + 4));
    const x8 = decodeFloat16(buf.readUInt16LE(firstPtr + 8));
    const y8 = decodeFloat16(buf.readUInt16LE(firstPtr + 10));
    const z8 = decodeFloat16(buf.readUInt16LE(firstPtr + 12));

    console.log(`\nModel: ${path.basename(targetPath)}`);
    console.log(`  vSize: ${vSize}, vCount: ${vCount}`);
    console.log(`  pos0 non-zero: ${pos0_nonzero}/10 -> [${x0.toFixed(3)}, ${y0.toFixed(3)}, ${z0.toFixed(3)}]`);
    console.log(`  pos8 non-zero: ${pos8_nonzero}/10 -> [${x8.toFixed(3)}, ${y8.toFixed(3)}, ${z8.toFixed(3)}]`);
}

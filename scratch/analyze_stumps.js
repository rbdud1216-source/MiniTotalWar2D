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
    if (p.includes('stump') && p.endsWith('.rigid_model_v2')) {
        packEntries.push({ path: p, size, offset: currentOffset });
    }
    currentOffset += size;
}

for (const entry of packEntries) {
    const buf = Buffer.alloc(entry.size);
    fs.readSync(fd, buf, 0, entry.size, entry.offset);
    const lodCount = buf.readUInt16LE(8);
    const meshCount = buf.readUInt32LE(140);
    console.log(`=== ${entry.path} === size: ${entry.size}, lods: ${lodCount}, meshes: ${meshCount}`);
    let cur = 140 + 28 * lodCount;
    for (let m = 0; m < meshCount; m++) {
        const matType = buf.readUInt16LE(cur);
        const sectionSize = buf.readUInt32LE(cur + 4);
        const vOffset = buf.readUInt32LE(cur + 8);
        const vCount = buf.readUInt32LE(cur + 12);
        const iOffset = buf.readUInt32LE(cur + 16);
        const iCount = buf.readUInt32LE(cur + 20);
        const vFormat = buf.readUInt32LE(cur + 24);
        const vSize = (iOffset - vOffset) / vCount;
        console.log(`  Mesh ${m}: matType=${matType}, vFormat=${vFormat}, vCount=${vCount}, iCount=${iCount}, vSize=${vSize}`);

        // 버텍스 첫 3개 덤프
        const gVStart = cur + vOffset;
        for (let vi = 0; vi < Math.min(3, vCount); vi++) {
            const ptr = gVStart + vi * vSize;
            const bytes = [];
            for (let b = 0; b < Math.min(vSize, 32); b++) {
                bytes.push(buf[ptr + b].toString(16).padStart(2, '0'));
            }
            console.log(`    V${vi} (hex): ${bytes.join(' ')}`);
        }

        cur += sectionSize;
    }
}

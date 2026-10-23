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

for (let i = 0; i < fileCount; i++) {
    const size = indexBuf.readUInt32LE(cursor);
    cursor += 5;
    let end = cursor;
    while (indexBuf[end] !== 0) end++;
    const p = indexBuf.toString('utf8', cursor, end).replace(/\\/g, '/');
    cursor = end + 1;
    if (p.startsWith('battleterrain/vegetation/trees/stump/stump_') && p.endsWith('.rigid_model_v2')) {
        const buf = Buffer.alloc(size);
        fs.readSync(fd, buf, 0, size, currentOffset);
        const str = buf.toString('ascii');
        const matches = str.match(/[a-zA-Z0-9_\/]+\.dds/gi) || [];
        console.log(`\n=== ${p} ===`);
        console.log('Textures in RMV2:', matches);
    }
    currentOffset += size;
}

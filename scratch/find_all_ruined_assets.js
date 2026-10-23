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

const allFiles = [];
for (let i = 0; i < fileCount; i++) {
    const size = indexBuf.readUInt32LE(cursor);
    cursor += 5;
    let end = cursor;
    while (indexBuf[end] !== 0) end++;
    const p = indexBuf.toString('utf8', cursor, end).replace(/\\/g, '/');
    cursor = end + 1;
    allFiles.push(p);
}

console.log(`Total files: ${allFiles.length}`);

console.log('\n--- Files with "stump" ---');
allFiles.filter(f => f.toLowerCase().includes('stump')).forEach(f => console.log(f));

console.log('\n--- Files with "dead" ---');
allFiles.filter(f => f.toLowerCase().includes('dead') && f.endsWith('.rigid_model_v2')).forEach(f => console.log(f));

console.log('\n--- Files with "broken" ---');
allFiles.filter(f => f.toLowerCase().includes('broken') && f.endsWith('.rigid_model_v2')).forEach(f => console.log(f));

console.log('\n--- Files with "trunk" and ".rigid_model_v2" ---');
allFiles.filter(f => f.toLowerCase().includes('trunk') && f.endsWith('.rigid_model_v2')).forEach(f => console.log(f));

console.log('\n--- Directories in battleterrain/vegetation/trees/ ---');
const dirs = new Set();
allFiles.forEach(f => {
    if (f.startsWith('battleterrain/vegetation/trees/')) {
        const parts = f.split('/');
        if (parts.length >= 4) dirs.add(parts[3]);
    }
});
console.log(Array.from(dirs).join(', '));

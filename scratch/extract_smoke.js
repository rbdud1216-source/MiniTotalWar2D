const fs = require('fs');
const path = require('path');

const packPath = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Total War THREE KINGDOMS\\data\\data.pack';
if (!fs.existsSync(packPath)) {
    console.log('Not found:', packPath);
    process.exit(1);
}

const fd = fs.openSync(packPath, 'r');
const header = Buffer.alloc(28);
fs.readSync(fd, header, 0, 28, 0);

const fileCount = header.readInt32LE(16);
const indexSize = header.readInt32LE(20);
console.log('Files:', fileCount, 'IndexSize:', indexSize);

const indexBuffer = Buffer.alloc(indexSize);
fs.readSync(fd, indexBuffer, 0, indexSize, 28);

let offset = 0;
const entries = [];
for (let i = 0; i < fileCount; i++) {
    const size = indexBuffer.readInt32LE(offset);
    const comp = indexBuffer.readUInt8(offset + 4);
    offset += 5;
    let end = offset;
    while (indexBuffer[end] !== 0) end++;
    const filePath = indexBuffer.toString('utf8', offset, end);
    offset = end + 1;
    entries.push({ path: filePath, size, comp });
}

let dataOffset = 28 + indexSize;
for (const e of entries) {
    e.offset = dataOffset;
    dataOffset += e.size;
}

const outDir = 'c:\\unityProject\\MiniTotalWar2D\\scratch\\extracted_tw_vfx';
if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

for (const e of entries) {
    const lower = e.path.toLowerCase();
    if (lower.includes('3k_anim_smoke') || lower.includes('wispy_smoke') || lower.includes('smoke_plume') || lower.includes('smoke_a_diffuse')) {
        console.log('Found:', e.path, 'Size:', e.size);
        const buf = Buffer.alloc(e.size);
        fs.readSync(fd, buf, 0, e.size, e.offset);
        const fileName = path.basename(e.path);
        const outFile = path.join(outDir, fileName);
        fs.writeFileSync(outFile, buf);
        console.log('  -> Wrote to:', outFile);
    }
}
fs.closeSync(fd);
